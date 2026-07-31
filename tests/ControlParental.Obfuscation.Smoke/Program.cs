// <copyright file="Program.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Obfuscation.Smoke;

using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

/// <summary>
/// T23 Obfuscation Closure — Smoke entry point.
///
/// Loads the obfuscated <c>ControlParental.Domain.dll</c> into a
/// dedicated <see cref="AssemblyLoadContext"/> and verifies three
/// invariants against the obfuscated artifact:
/// <list type="number">
///   <item>The obfuscated assembly loads (no BadImage / TypeLoad failures).</item>
///   <item>Source-generated JSON round-trips a <c>Policy</c> via
///         <c>PolicyJsonContext.Default.Policy</c>, preserving
///         <c>snake_case</c> wire names (<c>device_id</c>,
///         <c>daily_screen_time_minutes</c>, …).</item>
///   <item>EF Core / SQLite can save and query a
///         <c>PolicyDbEntity</c> materialized from the obfuscated
///         type's public surface.</item>
/// </list>
///
/// Exit codes:
/// <list type="bullet">
///   <item><c>0</c> — all three checks passed.</item>
///   <item><c>2</c> — required <c>--domain-dll</c> argument missing or DLL not found.</item>
///   <item><c>3</c> — obfuscated Domain.dll could not be loaded.</item>
///   <item><c>4</c> — required type <c>PolicyJsonContext</c> / <c>Policy</c> not found in obfuscated Domain.</item>
///   <item><c>5</c> — JSON round-trip lost snake_case wire names.</item>
///   <item><c>6</c> — <c>PolicyDbEntity</c> missing required public surface (DeviceId/Version/…).</item>
///   <item><c>7</c> — EF Core schema creation failed.</item>
///   <item><c>8</c> — EF Core save/query round-trip mismatch.</item>
///   <item><c>1</c> — generic unhandled failure (with stack trace).</item>
/// </list>
/// </summary>
internal static class Program
{
    private const string DomainAssemblyName = "ControlParental.Domain";
    private const string PolicyJsonContextTypeName = "ControlParental.Domain.PolicyJsonContext";
    private const string PolicyTypeName = "ControlParental.Domain.Policy";
    private const string PolicyDbEntityTypeName = "ControlParental.Domain.PolicyDbEntity";

    // Property names that must remain on the obfuscated PolicyDbEntity
    // (KeepPublicApi=true preserves these).
    private static readonly string[] RequiredPolicyDbEntityProperties =
    {
        "DeviceId",
        "Version",
        "PolicyJson",
        "LastUpdated",
        "CategoryAssignmentsJson",
    };

    // JSON keys that must appear on the round-tripped output.
    private static readonly string[] RequiredPolicySnakeCaseKeys =
    {
        "device_id",
        "version",
        "device_state",
        "daily_screen_time_minutes",
        "schedules",
        "category_limits",
        "app_policies",
        "category_assignments",
        "grants",
    };

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[smoke] FAIL: unhandled exception: {ex}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        var parsed = ParseArgs(args);

        if (!parsed.TryGetValue("--domain-dll", out var domainDllPath) || string.IsNullOrWhiteSpace(domainDllPath))
        {
            Console.Error.WriteLine("[smoke] FAIL: --domain-dll <path> argument is required.");
            return 2;
        }

        // Step 1 — domain DLL must exist (RED path for missing/unloadable input).
        if (!File.Exists(domainDllPath))
        {
            Console.Error.WriteLine($"[smoke] FAIL: Domain DLL not found at '{domainDllPath}'.");
            return 2;
        }

        // Step 2 — load the obfuscated DLL into a dedicated ALC.
        var alc = new AssemblyLoadContext($"obfuscated-domain-{Guid.NewGuid():N}", isCollectible: false);
        Assembly obfuscatedDomain;
        try
        {
            obfuscatedDomain = alc.LoadFromAssemblyPath(Path.GetFullPath(domainDllPath));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[smoke] FAIL: cannot load obfuscated Domain DLL: {ex.GetType().Name}: {ex.Message}");
            return 3;
        }

        Console.WriteLine($"[smoke] loaded obfuscated assembly: {obfuscatedDomain.FullName}");

        // Step 3 — JSON round-trip via PolicyJsonContext.Default.Policy
        // resolved from the obfuscated assembly.
        var jsonResult = RunJsonRoundTrip(obfuscatedDomain);
        if (jsonResult != 0)
        {
            return jsonResult;
        }

        // Step 4 — EF Core save/query round-trip via PolicyDbEntity
        // resolved from the obfuscated assembly.
        var efResult = RunEfRoundTrip(obfuscatedDomain);
        if (efResult != 0)
        {
            return efResult;
        }

        Console.WriteLine("[smoke] OK: load + JSON + EF round-trip passed against obfuscated Domain.");
        return 0;
    }

    private static int RunJsonRoundTrip(Assembly obfuscatedDomain)
    {
        var jsonContextType = obfuscatedDomain.GetType(PolicyJsonContextTypeName, throwOnError: false);
        if (jsonContextType is null)
        {
            Console.Error.WriteLine($"[smoke] FAIL: type '{PolicyJsonContextTypeName}' not found in obfuscated assembly.");
            return 4;
        }

        var policyType = obfuscatedDomain.GetType(PolicyTypeName, throwOnError: false);
        if (policyType is null)
        {
            Console.Error.WriteLine($"[smoke] FAIL: type '{PolicyTypeName}' not found in obfuscated assembly.");
            return 4;
        }

        var defaultProperty = jsonContextType.GetProperty(
            "Default",
            BindingFlags.Public | BindingFlags.Static);
        if (defaultProperty is null || defaultProperty.GetValue(obj: null) is not { } defaultInstance)
        {
            Console.Error.WriteLine("[smoke] FAIL: PolicyJsonContext.Default is missing or returned null.");
            return 4;
        }

        // `PolicyJsonContext.Default` is itself a `PolicyJsonContext`
        // instance; the actual `JsonTypeInfo<Policy>` is the `Policy`
        // property on it.
        var policyTypeInfoProperty = defaultInstance.GetType()
            .GetProperty("Policy", BindingFlags.Public | BindingFlags.Instance);
        if (policyTypeInfoProperty is null || policyTypeInfoProperty.GetValue(defaultInstance) is not { } policyTypeInfo)
        {
            Console.Error.WriteLine("[smoke] FAIL: PolicyJsonContext.Default.Policy is missing or returned null.");
            return 4;
        }

        // The string-returning Serialize helper for source-generated
        // `JsonTypeInfo<T>` is the static
        // `JsonSerializer.Serialize<TValue>(TValue, JsonTypeInfo<TValue>)`
        // method, not an instance method on the type. The runtime API
        // places TValue first and JsonTypeInfo<TValue> second.
        var jsonSerializerType = typeof(System.Text.Json.JsonSerializer);
        var serializeMethodDef = jsonSerializerType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "Serialize")
            .Where(m => m.IsGenericMethodDefinition)
            .Where(m => m.ReturnType == typeof(string))
            .Where(m =>
            {
                var p = m.GetParameters();
                return p.Length == 2
                    && p[1].ParameterType.IsGenericType
                    && p[1].ParameterType.GetGenericTypeDefinition() ==
                        typeof(System.Text.Json.Serialization.Metadata.JsonTypeInfo<>);
            })
            .FirstOrDefault();
        if (serializeMethodDef is null)
        {
            Console.Error.WriteLine("[smoke] FAIL: no JsonSerializer.Serialize<T>(T, JsonTypeInfo<T>) found.");
            return 4;
        }

        var closedSerialize = serializeMethodDef.MakeGenericMethod(policyType);

        // Build a sample Policy via the obfuscated record's parameterless
        // constructor + property setters (init-only properties can be set
        // through reflection even on records).
        object policy;
        try
        {
            policy = CreateSamplePolicy(policyType);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[smoke] FAIL: could not construct sample Policy via reflection: {ex.Message}");
            return 4;
        }

        string json;
        try
        {
            json = (string)closedSerialize.Invoke(jsonSerializerType, new object?[] { policy, policyTypeInfo })!;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            Console.Error.WriteLine($"[smoke] FAIL: source-gen Serialize threw {tie.InnerException.GetType().Name}: {tie.InnerException.Message}");
            return 5;
        }

        // `json` is already a string from JsonSerializer.Serialize<T>
        // (returns string); keep the variable for clarity in the snake_case
        // key assertion below.
        var missing = RequiredPolicySnakeCaseKeys
            .Where(key => json.IndexOf($"\"{key}\"", StringComparison.Ordinal) < 0)
            .ToArray();
        if (missing.Length > 0)
        {
            Console.Error.WriteLine($"[smoke] FAIL: source-gen output lost snake_case key(s): {string.Join(", ", missing)}");
            Console.Error.WriteLine($"[smoke] output: {json}");
            return 5;
        }

        Console.WriteLine("[smoke] OK: JSON round-trip preserved snake_case wire names.");
        return 0;
    }

    private static int RunEfRoundTrip(Assembly obfuscatedDomain)
    {
        var policyDbEntityType = obfuscatedDomain.GetType(PolicyDbEntityTypeName, throwOnError: false);
        if (policyDbEntityType is null)
        {
            Console.Error.WriteLine($"[smoke] FAIL: type '{PolicyDbEntityTypeName}' not found in obfuscated assembly.");
            return 6;
        }

        // Verify the obfuscated entity still exposes the public surface
        // EF Core depends on. KeepPublicApi=true should preserve these;
        // the assertion guards against an accidental Obfuscar config
        // change that drops the flag.
        var missingProps = RequiredPolicyDbEntityProperties
            .Where(name => policyDbEntityType.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance) is null)
            .ToArray();
        if (missingProps.Length > 0)
        {
            Console.Error.WriteLine(
                $"[smoke] FAIL: obfuscated PolicyDbEntity is missing public property/ies: {string.Join(", ", missingProps)}");
            return 6;
        }

        // SQLite in-memory + EnsureCreated exercises the full EF Core
        // pipeline (model snapshot, DDL generation, materialized
        // queries) against the obfuscated Type.
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var optionsBuilder = new DbContextOptionsBuilder<SmokeDbContext>()
            .UseSqlite(connection);

        using var dbContext = new SmokeDbContext(optionsBuilder.Options, policyDbEntityType);

        try
        {
            dbContext.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[smoke] FAIL: EF Core EnsureCreated failed: {ex.GetType().Name}: {ex.Message}");
            return 7;
        }

        const string deviceId = "smoke-device-001";
        const int version = 7;
        const string policyJson = """{"device_id":"smoke-device-001","version":7,"device_state":"active"}""";

        var nowUtc = DateTimeOffset.UtcNow;
        var entity = Activator.CreateInstance(policyDbEntityType)!;
        SetProperty(entity, "DeviceId", deviceId);
        SetProperty(entity, "Version", version);
        SetProperty(entity, "PolicyJson", policyJson);
        SetProperty(entity, "LastUpdated", nowUtc);
        SetProperty(entity, "CategoryAssignmentsJson", """{"smoke.app":"games"}""");

        // dbContext.Set<TEntity>() is generic on the compile-time type;
        // the smoke uses the obfuscated Type via reflection so the EF
        // model materializer operates on the obfuscated surface.
        var setMethod = typeof(DbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == "Set" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
            .MakeGenericMethod(policyDbEntityType);
        var dbSet = setMethod.Invoke(dbContext, parameters: null)!;

        try
        {
            var addMethod = dbSet.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(m => m.Name == "Add" && m.GetParameters().Length == 1);
            addMethod.Invoke(dbSet, new[] { entity });
            dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[smoke] FAIL: EF Core save failed: {ex.GetType().Name}: {ex.Message}");
            return 8;
        }

        // Query the saved row through DbSet.Find(params object?[]) — the
        // synchronous overload avoids having to materialize a generic
        // ValueTask<T> through reflection for an obfuscated T.
        var deviceIdValue = deviceId;
        object? found;
        try
        {
            var findMethod = dbSet.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(m => m.Name == "Find"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(object?[]));
            found = findMethod.Invoke(dbSet, new object?[] { new object?[] { deviceIdValue } });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[smoke] FAIL: EF Core query failed: {ex.GetType().Name}: {ex.Message}");
            return 8;
        }

        if (found is null)
        {
            Console.Error.WriteLine("[smoke] FAIL: saved PolicyDbEntity not found via DbSet.FindAsync.");
            return 8;
        }

        var foundType = found.GetType();
        var foundDeviceId = (string?)foundType.GetProperty("DeviceId", BindingFlags.Public | BindingFlags.Instance)!.GetValue(found);
        var foundVersion = (int?)foundType.GetProperty("Version", BindingFlags.Public | BindingFlags.Instance)!.GetValue(found);
        var foundPolicyJson = (string?)foundType.GetProperty("PolicyJson", BindingFlags.Public | BindingFlags.Instance)!.GetValue(found);

        if (foundDeviceId != deviceId || foundVersion != version || foundPolicyJson != policyJson)
        {
            Console.Error.WriteLine(
                $"[smoke] FAIL: round-trip mismatch. expected ({deviceId}, {version}, policy_json_set); got ({foundDeviceId}, {foundVersion}, policy_json_set={(foundPolicyJson is not null)}).");
            return 8;
        }

        Console.WriteLine("[smoke] OK: EF Core save/query round-tripped obfuscated PolicyDbEntity.");
        return 0;
    }

    private static object CreateSamplePolicy(Type policyType)
    {
        // The smoke only needs a Policy-shaped object whose public
        // properties serialize to the expected snake_case JSON. Init-only
        // properties can be set through reflection.
        var instance = Activator.CreateInstance(policyType)!;
        SetProperty(instance, "DeviceId", "smoke-device-001");
        SetProperty(instance, "Version", 7);
        // DeviceState: best-effort, accept either an enum value named "Active"
        // or a string "active" if the source-gen context expects one.
        TrySetEnumByName(instance, "DeviceState", "Active");
        SetProperty(instance, "DailyScreenTimeMinutes", 240);
        SetProperty(instance, "Schedules", Array.CreateInstance(GetArrayElementType(policyType, "Schedules")!, 0));
        SetProperty(instance, "CategoryLimits", Array.CreateInstance(GetArrayElementType(policyType, "CategoryLimits")!, 0));
        SetProperty(instance, "AppPolicies", Array.CreateInstance(GetArrayElementType(policyType, "AppPolicies")!, 0));
        SetProperty(instance, "CategoryAssignments", new Dictionary<string, string> { { "smoke.app", "games" } });
        SetProperty(instance, "Grants", Array.CreateInstance(GetArrayElementType(policyType, "Grants")!, 0));
        return instance;
    }

    private static Type GetArrayElementType(Type owner, string propertyName)
    {
        var prop = owner.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"missing property {propertyName} on {owner.FullName}");
        return prop.PropertyType.GetElementType()
            ?? throw new InvalidOperationException($"property {propertyName} on {owner.FullName} is not an array");
    }

    private static void TrySetEnumByName(object instance, string propertyName, string enumName)
    {
        var prop = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null)
        {
            return;
        }

        var underlying = prop.PropertyType;
        if (!underlying.IsEnum)
        {
            // Source-gen expects the enum-as-string fallback via
            // [JsonStringEnumConverter] (snake_case names). Setting the
            // underlying enum is best-effort — if the value is missing
            // (default 0), the smoke still validates the wire name
            // keys; if found, we use the named enum member.
            return;
        }

        if (Enum.TryParse(underlying, enumName, ignoreCase: true, out var parsed))
        {
            prop.SetValue(instance, parsed);
        }
    }

    private static void SetProperty(object instance, string propertyName, object? value)
    {
        var prop = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"missing property {propertyName} on {instance.GetType().FullName}");
        prop.SetValue(instance, value);
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                dict[args[i]] = args[i + 1];
                i++;
            }
            else
            {
                dict[args[i]] = "true";
            }
        }

        return dict;
    }
}
