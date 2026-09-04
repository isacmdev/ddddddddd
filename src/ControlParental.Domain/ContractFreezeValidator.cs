// <copyright file="ContractFreezeValidator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

using System.Text.Json;


/// <summary>Read-only validation of the shared-contract fixture manifest.</summary>
public sealed record ContractFreezeValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>Contract pre-lane boundary. It has no persistence or side effects.</summary>
public static class ContractFreezeValidator
{
    private const string PolicyHash = "8bb4e3bee2a84c0ea9a3ce8d62a73a23e1fbbf711dd0cf26ea9ddd0661fd267a";
    private static readonly string[] RequiredPolicyMembers =
    ["device_id", "version", "device_state", "daily_screen_time_minutes", "schedules", "category_limits", "app_policies", "category_assignments", "grants", "snapshot_hash"];
    private static readonly HashSet<string> RequiredEnvelope = ["contract", "version", "message_type", "correlation_id", "payload"];

    /// <summary>Validates one decoded message before any caller can apply it.</summary>
    public static IReadOnlyList<string> ValidateMessage(JsonElement message, int wireBytes)
    {
        var errors = new List<string>();
        if (message.ValueKind != JsonValueKind.Object) return ["envelope must be an object"];
        var names = message.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        errors.AddRange(RequiredEnvelope.Except(names).Select(n => $"missing envelope member: {n}"));
        errors.AddRange(names.Except(RequiredEnvelope.Append("extensions"), StringComparer.Ordinal).Select(n => $"unknown envelope member: {n}"));
        if (message.TryGetProperty("contract", out var contract) && contract.GetString() != "control-parental.windows") errors.Add("unsupported contract");
        if (message.TryGetProperty("version", out var version) && (!version.TryGetInt32(out var v) || v != 1)) errors.Add("unsupported major version");
        if (message.TryGetProperty("correlation_id", out var correlation) && (!Guid.TryParse(correlation.GetString(), out var id) || id == Guid.Empty)) errors.Add("invalid correlation_id");
        if (wireBytes > 65_536) errors.Add("envelope exceeds 65536 UTF-8 bytes");
        var maxDepth = MaxDepth(message, 1);
        if (maxDepth > 16) errors.Add("JSON nesting exceeds 16 levels");
        foreach (var value in Walk(message))
        {
            if (value.ValueKind == JsonValueKind.String && (value.GetString() ?? string.Empty).Length > 4096) errors.Add("string exceeds 4096 UTF-8 bytes");
            if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 256) errors.Add("array exceeds 256 items");
        }
        if (message.TryGetProperty("extensions", out var envelopeExtensions)) ValidateExtensions(envelopeExtensions, errors);
        if (message.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
        {
            var type = message.TryGetProperty("message_type", out var mt) ? mt.GetString() : null;
            if (payload.TryGetProperty("required_by_newer_peer", out _)) errors.Add("unknown payload member");
            if (type == "set_protected_account.request")
            {
                foreach (var required in new[] { "operation_id", "username", "sid", "requested_at" })
                    if (!payload.TryGetProperty(required, out _)) errors.Add($"missing payload member: {required}");
            }
            if (type is "wns.hint" or "realtime.hint")
            {
                var keys = payload.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                if (!keys.IsSubsetOf(new[] { "hint_type", "correlation_hint", "extensions" }) || keys.Count == 0) errors.Add("hint payload is not signal-only");
                if (wireBytes > 1_024) errors.Add("hint exceeds 1024 UTF-8 bytes");
            }
            if (type == "create_time_request")
            {
                if (!payload.TryGetProperty("minutes", out var minutes) || !minutes.TryGetInt32(out var m) || m is < 1 or > 180) errors.Add("minutes is outside 1..180");
                if (payload.TryGetProperty("origin", out var origin) && origin.GetString() is not ("status_page" or "overlay")) errors.Add("invalid origin");
                if (payload.TryGetProperty("reason", out var reason) && System.Text.Encoding.UTF8.GetByteCount(reason.GetString() ?? string.Empty) > 256) errors.Add("reason exceeds 256 bytes");
                if (payload.TryGetProperty("created_at", out var created) && DateTimeOffset.TryParse(created.GetString(), out var timestamp) && timestamp > DateTimeOffset.Parse("2026-08-28T12:05:00Z", System.Globalization.CultureInfo.InvariantCulture)) errors.Add("timestamp exceeds future skew");
            }
            if (payload.TryGetProperty("extensions", out var payloadExtensions)) ValidateExtensions(payloadExtensions, errors);
        }
        return errors;
    }

    private static void ValidateExtensions(JsonElement value, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() > 8) { errors.Add("invalid extensions"); return; }
        foreach (var property in value.EnumerateObject()) if (!System.Text.RegularExpressions.Regex.IsMatch(property.Name, "^x-[a-z0-9][a-z0-9._-]{0,63}$")) errors.Add("invalid extension key");
    }

    private static int MaxDepth(JsonElement value, int depth)
    {
        if (value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)) return depth;
        return Children(value).Select(child => MaxDepth(child, depth + 1)).DefaultIfEmpty(depth).Max();
    }

    private static IEnumerable<JsonElement> Children(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) foreach (var property in value.EnumerateObject()) yield return property.Value;
        if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) yield return item;
    }

    private static IEnumerable<JsonElement> Walk(JsonElement value)
    {
        yield return value;
        if (value.ValueKind == JsonValueKind.Object) foreach (var p in value.EnumerateObject()) foreach (var x in Walk(p.Value)) yield return x;
        if (value.ValueKind == JsonValueKind.Array) foreach (var x in value.EnumerateArray()) foreach (var child in Walk(x)) yield return child;
    }

    public static ContractFreezeValidationResult ValidateFixtureSet(string fixtureDirectory)
    {
        var errors = new List<string>();
        var root = new DirectoryInfo(fixtureDirectory);
        if (!root.Exists)
        {
            return new(["fixture directory does not exist"]);
        }

        var files = root.GetFiles("*.json").Select(f => f.Name).Order(StringComparer.Ordinal).ToArray();
        var manifestFile = root.GetFiles("fixture-manifest.json").SingleOrDefault();
        if (manifestFile is null)
        {
            errors.Add("fixture-manifest.json is missing");
            return new(errors);
        }

        JsonDocument manifest;
        try { manifest = JsonDocument.Parse(File.ReadAllBytes(manifestFile.FullName)); }
        catch (JsonException) { return new(["fixture manifest is not valid JSON"]); }
        using (manifest)
        {
            var listed = manifest.RootElement.GetProperty("fixtures").EnumerateObject()
                .SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString()!)).ToArray();
            var listedSet = listed.ToHashSet(StringComparer.Ordinal);
            var actualSet = files.Where(f => f != manifestFile.Name).ToHashSet(StringComparer.Ordinal);
            if (listedSet.Count != 27 || !listedSet.SetEquals(actualSet))
            {
                errors.Add($"manifest fixture set mismatch: listed={listedSet.Count}, actual={actualSet.Count}");
            }

            var negatives = manifest.RootElement.GetProperty("negative_one_cause").EnumerateObject().ToArray();
            var negativeFiles = actualSet.Where(f => f.StartsWith("invalid-", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
            if (negatives.Length != negativeFiles.Count || !negatives.Select(p => p.Name).ToHashSet().SetEquals(negativeFiles) || negatives.Any(p => string.IsNullOrWhiteSpace(p.Value.GetString())))
            {
                errors.Add("negative manifest is not exhaustive or has an empty cause");
            }
        }

        foreach (var file in files)
        {
            try { using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root.FullName, file))); }
            catch (JsonException) { errors.Add($"{file}: invalid JSON"); }
        }

        var policyPath = Path.Combine(root.FullName, "valid-policy-snapshot.json");
        if (File.Exists(policyPath))
        {
            using var policy = JsonDocument.Parse(File.ReadAllBytes(policyPath));
            var payload = policy.RootElement.GetProperty("payload");
            if (!RequiredPolicyMembers.All(name => payload.TryGetProperty(name, out _))) errors.Add("policy payload member set is incomplete");
            var grants = payload.GetProperty("grants");
            foreach (var grant in grants.EnumerateArray())
            {
                if (!grant.TryGetProperty("granted_at", out var granted) || !grant.TryGetProperty("expires_at", out var expires) ||
                    !DateTimeOffset.TryParse(granted.GetString(), out var start) || !DateTimeOffset.TryParse(expires.GetString(), out var end) || end <= start)
                    errors.Add("grant timestamps are invalid");
            }
            if (payload.GetProperty("snapshot_hash").GetString() != PolicyHash) errors.Add("policy snapshot hash mismatch");
        }

        return new(errors);
    }

    /// <summary>Canonical policy bytes: recursively ordinal-sorted maps, ordered arrays, UTF-8 JSON without whitespace.</summary>
    public static byte[] Canonicalize(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = false }))
        {
            WriteCanonical(writer, value);
        }
        return stream.ToArray();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.Number: writer.WriteRawValue(value.GetRawText(), true); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new JsonException("undefined JSON value");
        }
    }
}
