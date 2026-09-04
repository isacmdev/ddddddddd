// <copyright file="WnsRegistrationE2ETests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.E2E.Tests;

using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;

/// <summary>
/// Real WinUI WNS registration test. This project is intentionally opt-in:
/// missing prerequisites are blockers, not skipped or passing tests.
/// </summary>
public sealed class WnsRegistrationE2ETests
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan SessionStartTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan InitialUiReadinessTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AppiumStatusTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ServicePipeTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PollSlice = TimeSpan.FromMilliseconds(250);
    private const string WnsRegistrationNavigationAutomationId = "WnsRegistrationNavigationButton";
    private static readonly HashSet<string> SafeStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Not registered",
        "Registration accepted",
        "Registration pending",
        "Registration denied",
        "Registration can be retried",
        "Registration unavailable",
    };
    private static readonly string[] ForbiddenFieldNames =
    [
        "channeluri", "operationid", "correlationid", "backend", "credential", "password",
        "clientsecret", "client_secret", "accesstoken", "access_token", "refreshtoken", "refresh_token",
        "apikey", "api_key", "publishablekey", "publishable_key", "secretkey", "secret_key", "supabase",
    ];
    private static readonly System.Text.RegularExpressions.Regex SafePasswordMetadataPattern = new(
        @"(?<![A-Za-z0-9_.:-])IsPassword\s*=\s*(?<quote>[""'])False\k<quote>(?![A-Za-z0-9_.:-])",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly System.Text.RegularExpressions.Regex SensitiveAssignmentPattern = new(
        @"(?ix)(?<![A-Za-z0-9_])(?:
            supabase(?:[-_ ]?(?:url|anon[-_ ]?key))? |
            (?:access|refresh|id)[-_ ]?token |
            authorization | apikey | api[-_ ]?key | password | secret |
            client[-_ ]?secret | publishable[-_ ]?key | secret[-_ ]?key |
            channel[-_ ]?uri | push[-_ ]?handle | raw[-_ ]?body |
            backend[-_ ]?(?:url|body) | operation[-_ ]?id | correlation[-_ ]?id |
            credential(?:s)? | report[-_ ]?hash | binary[-_ ]?hash | executable[-_ ]?path |
            payload
        )\s*[:=]\s*(?:""[^""\r\n]*""|'[^'\r\n]*'|[^\s,;]*)",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly System.Text.RegularExpressions.Regex SensitiveFieldNamePattern = new(
        @"(?ix)(?<![A-Za-z0-9_])(?:
            supabase(?:[-_ ]?(?:url|anon[-_ ]?key))? |
            (?:access|refresh|id)[-_ ]?token | authorization | apikey | api[-_ ]?key |
            password | secret | client[-_ ]?secret | publishable[-_ ]?key | secret[-_ ]?key |
            channel[-_ ]?uri | push[-_ ]?handle | raw[-_ ]?body | backend[-_ ]?(?:url|body) |
            operation[-_ ]?id | correlation[-_ ]?id | credential(?:s)? | report[-_ ]?hash |
            binary[-_ ]?hash | executable[-_ ]?path | payload
        )(?![A-Za-z0-9_])",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    [Fact]
    public void EvidenceSafety_AllowsStandardFalsePasswordMetadataAndRejectsSecrets()
    {
        var assertSafeText = typeof(WnsRegistrationE2ETests).GetMethod(
            "AssertSafeText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var redact = typeof(WnsRegistrationE2ETests).GetMethod(
            "Redact",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        const string safeMetadata = "<Edit AutomationId=\"Username\" IsPassword=\"False\" />";

        assertSafeText.Invoke(null, [safeMetadata, "standard UIA metadata"]);
        Assert.Equal(safeMetadata, (string)redact.Invoke(null, [safeMetadata])!);
        Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            assertSafeText.Invoke(null, ["<Edit IsPassword=\"True\" />", "password metadata"]));
        Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            assertSafeText.Invoke(null, ["AutomationId=\"Password\"", "password identifier"]));
        Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            assertSafeText.Invoke(null, ["password=secret-value", "password assignment"]));
    }

    [Fact]
    public void W3cSourceEnvelope_IsExtractedBeforeEvidenceSafetyAndWrite()
    {
        var extract = typeof(WnsRegistrationE2ETests).GetMethod(
            "ExtractW3cSourceXml",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(extract);

        var xml = "<Window IsPassword=\"False\" />";
        var envelope = JsonSerializer.Serialize(new { value = xml });
        var extracted = (string)extract!.Invoke(null, [envelope])!;
        Assert.Equal(xml, extracted);

        var assertSafeText = typeof(WnsRegistrationE2ETests).GetMethod(
            "AssertSafeText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var redact = typeof(WnsRegistrationE2ETests).GetMethod(
            "Redact",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        assertSafeText.Invoke(null, [extracted, "W3C page source"]);
        Assert.Equal(xml, (string)redact.Invoke(null, [extracted])!);

        foreach (var malformed in new[] { "{", "{}", "{\"value\":null}", "{\"value\":42}" })
        {
            var failure = Assert.Throws<System.Reflection.TargetInvocationException>(() => extract.Invoke(null, [malformed]));
            Assert.DoesNotContain(malformed, failure.InnerException?.Message ?? failure.Message, StringComparison.Ordinal);
        }

        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "ControlParental.sln")))
        {
            repository = repository.Parent;
        }

        var source = File.ReadAllText(Path.Combine(
            repository?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found."),
            "tests",
            "ControlParental.App.UI.E2E.Tests",
            "E2E",
            "WnsRegistrationE2ETests.cs"));
        var capture = source.Substring(source.IndexOf("private static async Task CaptureEvidenceAsync", StringComparison.Ordinal));
        Assert.True(capture.IndexOf("ExtractW3cSourceXml", StringComparison.Ordinal) < capture.IndexOf("AssertSafeText(source", StringComparison.Ordinal));
        Assert.True(capture.IndexOf("AssertSafeText(source", StringComparison.Ordinal) < capture.IndexOf("WriteAllTextAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplicationLogEvidence_RedactsBeforeValidationAndWrite_WithoutRelaxingPageSourceFailClosed()
    {
        var redact = typeof(WnsRegistrationE2ETests).GetMethod(
            "Redact",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var assertSafeText = typeof(WnsRegistrationE2ETests).GetMethod(
            "AssertSafeText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        const string benignRawLog = "push handler skipped: SUPABASE_URL or SUPABASE_ANON_KEY is missing";
        var benignRedactedLog = (string)redact.Invoke(null, [benignRawLog])!;
        Assert.DoesNotContain("supabase", benignRedactedLog, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SUPABASE_URL", benignRedactedLog, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SUPABASE_ANON_KEY", benignRedactedLog, StringComparison.OrdinalIgnoreCase);
        assertSafeText.Invoke(null, [benignRedactedLog, "redacted benign application log"]);

        const string dangerousRawLog = "SUPABASE_ANON_KEY=super-secret-value-12345";
        var dangerousRedactedLog = (string)redact.Invoke(null, [dangerousRawLog])!;
        Assert.DoesNotContain("SUPABASE_ANON_KEY", dangerousRedactedLog, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret-value-12345", dangerousRedactedLog, StringComparison.Ordinal);
        Assert.Equal("[REDACTED]", dangerousRedactedLog.Trim());
        assertSafeText.Invoke(null, [dangerousRedactedLog, "redacted dangerous application log"]);

        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "ControlParental.sln")))
        {
            repository = repository.Parent;
        }

        var source = File.ReadAllText(Path.Combine(
            repository?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found."),
            "tests",
            "ControlParental.App.UI.E2E.Tests",
            "E2E",
            "WnsRegistrationE2ETests.cs"));
        var capture = source.Substring(source.IndexOf("private static async Task CaptureEvidenceAsync", StringComparison.Ordinal));
        var pageSourceBranch = capture[..capture.IndexOf("var log =", StringComparison.Ordinal)];
        var applicationLogBranch = capture[capture.IndexOf("var log =", StringComparison.Ordinal)..];

        Assert.True(pageSourceBranch.IndexOf("ExtractW3cSourceXml", StringComparison.Ordinal) < pageSourceBranch.IndexOf("AssertSafeText(source", StringComparison.Ordinal));
        Assert.DoesNotContain("Redact(source)", pageSourceBranch, StringComparison.Ordinal);
        Assert.True(applicationLogBranch.IndexOf("var redactedLog = Redact(rawLog);", StringComparison.Ordinal) >= 0);
        Assert.True(applicationLogBranch.IndexOf("AssertSafeText(redactedLog", StringComparison.Ordinal) > applicationLogBranch.IndexOf("var redactedLog = Redact(rawLog);", StringComparison.Ordinal));
        Assert.Contains("WriteAllTextAsync(destination, redactedLog)", applicationLogBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("AssertSafeText(" + "rawLog", applicationLogBranch, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000)]
    [Trait("Category", "E2E")]
    public async Task Registration_surface_exposes_only_safe_typed_state()
    {
        var prerequisite = await PrerequisiteFailureAsync();
        if (prerequisite is not null)
        {
            throw new XunitException($"E2E BLOCKED: {prerequisite}");
        }

        using var http = new HttpClient { BaseAddress = new Uri(ServerUrl()), Timeout = StepTimeout };
        string? session = null;
        var evidence = EvidenceDirectory();
        Directory.CreateDirectory(evidence);

        try
        {
            session = await CreateSessionAsync(http);
            var navigationElement = await WaitForElementAsync(
                () => FindAsync(http, session, WnsRegistrationNavigationAutomationId),
                WnsRegistrationNavigationAutomationId,
                InitialUiReadinessTimeout,
                delay: static delay => Task.Delay(delay),
                elapsed: static stopwatch => stopwatch.Elapsed);
            await ClickElementAsync(http, session, navigationElement);
            await WaitForElementAsync(http, session, "WnsRegistrationButton");
            await WaitForElementAsync(http, session, "WnsRegistrationStatus");
            await WaitForElementAsync(http, session, "WnsRegistrationProgress");

            var status = await AttributeAsync(http, session, "WnsRegistrationStatus", "Name");
            var initialButtonEnabled = bool.Parse(
                await AttributeAsync(http, session, "WnsRegistrationButton", "IsEnabled"));
            Assert.False(string.IsNullOrWhiteSpace(status));
            status = NormalizeState(status);
            Assert.Contains(status, SafeStates);
            Assert.True(initialButtonEnabled);

            await ClickAsync(http, session, "WnsRegistrationButton");
            var observedBusy = false;
            await PollAsync(async () =>
            {
                var buttonEnabled = bool.Parse(
                    await AttributeAsync(http, session, "WnsRegistrationButton", "IsEnabled"));
                observedBusy |= !buttonEnabled;
                var current = await AttributeAsync(http, session, "WnsRegistrationStatus", "Name");
                current = NormalizeState(current);
                return observedBusy || SafeStates.Contains(current) && !string.Equals(current, status, StringComparison.OrdinalIgnoreCase);
            });
            var finalStatus = NormalizeState(await AttributeAsync(http, session, "WnsRegistrationStatus", "Name"));
            var finalButtonEnabled = bool.Parse(
                await AttributeAsync(http, session, "WnsRegistrationButton", "IsEnabled"));
            Assert.True(observedBusy || !string.Equals(finalStatus, status, StringComparison.OrdinalIgnoreCase), "No observable registration lifecycle transition was seen.");
            Assert.True(finalButtonEnabled);
            Assert.Contains(finalStatus, SafeStates);

            await CaptureEvidenceAsync(http, session, evidence);
            await CaptureScreenshotAsync(http, session, evidence);
            AssertSafeEvidence(evidence);
        }
        finally
        {
            if (session is not null)
            {
                await DeleteSessionAsync(http, session);
            }
        }
    }

    private static async Task<string?> PrerequisiteFailureAsync()
    {
        if (Environment.GetEnvironmentVariable("E2E_REQUIRED") != "1") return "E2E_REQUIRED=1 is required; invoke the Windows E2E runbook.";
        if (!OperatingSystem.IsWindows()) return "Windows interactive desktop is required.";
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONTROL_PARENTAL_APP_PATH"))) return "CONTROL_PARENTAL_APP_PATH is not set.";
        if (!Uri.TryCreate(ServerUrl(), UriKind.Absolute, out var appiumUri) || appiumUri is null ||
            !string.Equals(appiumUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(appiumUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return "CONTROL_PARENTAL_APPIUM_URL must be an absolute HTTP(S) URL.";
        }

        using var probe = new HttpClient { Timeout = AppiumStatusTimeout };
        try
        {
            using var response = await probe.GetAsync(new Uri(appiumUri, "status"));
            if (!response.IsSuccessStatusCode) return $"External Appium is not ready at {appiumUri}.";
        }
        catch (HttpRequestException) { return $"External Appium is not reachable at {appiumUri}."; }

        try
        {
            using var pipe = new NamedPipeClientStream(".", "ControlParental.UI", PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync((int)ServicePipeTimeout.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            return "ControlParental.Service IPC is unavailable: named pipe ControlParental.UI is not ready.";
        }

        return null;
    }

    private static string ServerUrl() => Environment.GetEnvironmentVariable("CONTROL_PARENTAL_APPIUM_URL") ?? string.Empty;
    private static string EvidenceDirectory() => Environment.GetEnvironmentVariable("CONTROL_PARENTAL_E2E_EVIDENCE") ?? Path.Combine(Path.GetTempPath(), "ControlParental", "e2e-evidence");

    private static async Task<string> CreateSessionAsync(HttpClient http)
    {
        var capabilities = SessionCapabilities(Environment.GetEnvironmentVariable("CONTROL_PARENTAL_APP_PATH")!);
        capabilities["appium:deviceName"] = "WindowsPC";
        using var sessionHttp = new HttpClient
        {
            BaseAddress = http.BaseAddress,
            Timeout = SessionStartTimeout,
        };
        using var response = await sessionHttp.PostAsJsonAsync("/session", new { capabilities = new { alwaysMatch = capabilities } });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("value").GetProperty("sessionId").GetString() ?? throw new InvalidOperationException("Appium returned no session id.");
    }

    internal static string AutomationName() =>
        Environment.GetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME") switch
        {
            "NovaWindows2" => "NovaWindows2",
            _ => "Windows",
        };

    internal static Dictionary<string, object?> SessionCapabilities(string appPath)
    {
        var topLevelWindow = Environment.GetEnvironmentVariable("CONTROL_PARENTAL_E2E_TOP_LEVEL_WINDOW");
        if (!string.IsNullOrWhiteSpace(topLevelWindow))
        {
            var normalized = topLevelWindow.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? topLevelWindow[2..] : topLevelWindow;
            if (!long.TryParse(topLevelWindow, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var decimalHandle) &&
                !long.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out decimalHandle) ||
                decimalHandle <= 0)
            {
                throw new ArgumentException("CONTROL_PARENTAL_E2E_TOP_LEVEL_WINDOW must be a positive decimal or hexadecimal HWND.", nameof(topLevelWindow));
            }

            return new Dictionary<string, object?>
            {
                ["platformName"] = "Windows",
                ["appium:automationName"] = AutomationName(),
                ["appium:appTopLevelWindow"] = topLevelWindow,
                ["appium:shouldTerminateApp"] = false,
            };
        }

        return new Dictionary<string, object?>
        {
            ["platformName"] = "Windows",
            ["appium:automationName"] = AutomationName(),
            ["appium:app"] = appPath,
            ["appium:shouldTerminateApp"] = true,
        };
    }

    private static async Task ClickAsync(HttpClient http, string session, string value)
    {
        var element = await FindAsync(http, session, value);
        await ClickElementAsync(http, session, element);
    }

    private static async Task ClickElementAsync(HttpClient http, string session, string element)
    {
        if (string.Equals(AutomationName(), "NovaWindows2", StringComparison.OrdinalIgnoreCase))
        {
            var elementReference = new Dictionary<string, string>
            {
                ["element-6066-11e4-a52e-4f735466cecf"] = element,
            };
            using var invokeResponse = await http.PostAsJsonAsync(
                $"/session/{session}/execute/sync",
                new
                {
                    script = "windows: invoke",
                    args = new[] { elementReference },
                });
            invokeResponse.EnsureSuccessStatusCode();
            return;
        }

        using var clickResponse = await http.PostAsync($"/session/{session}/element/{element}/click", null);
        clickResponse.EnsureSuccessStatusCode();
    }

    public static async Task<string> WaitForElementAsync(
        Func<Task<string>> lookup,
        string automationId,
        TimeSpan deadline,
        Func<TimeSpan, Task> delay,
        Func<Stopwatch, TimeSpan> elapsed)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentNullException.ThrowIfNull(delay);
        ArgumentNullException.ThrowIfNull(elapsed);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deadline, TimeSpan.Zero);

        var stopwatch = Stopwatch.StartNew();
        Exception? last = null;
        while (elapsed(stopwatch) < deadline)
        {
            try
            {
                return await lookup().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                last = ex;
            }

            await delay(PollSlice).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Timed out waiting for AutomationId '{automationId}' after {deadline.TotalSeconds:F0}s.",
            last);
    }

    private static async Task WaitForElementAsync(HttpClient http, string session, string value)
        => _ = await WaitForElementAsync(
            () => FindAsync(http, session, value),
            value,
            StepTimeout,
            delay: static delay => Task.Delay(delay),
            elapsed: static stopwatch => stopwatch.Elapsed);

    private static async Task<string> FindAsync(HttpClient http, string session, string value)
    {
        using var response = await http.PostAsJsonAsync($"/session/{session}/element", new { @using = "accessibility id", value });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var element = json.RootElement.GetProperty("value");
        return element.TryGetProperty("element-6066-11e4-a52e-4f735466cecf", out var w3c) ? w3c.GetString()! : element.GetProperty("ELEMENT").GetString()!;
    }

    private static async Task<string> AttributeAsync(HttpClient http, string session, string automationId, string attribute)
    {
        var element = await FindAsync(http, session, automationId);
        using var response = await http.GetAsync($"/session/{session}/element/{element}/attribute/{attribute}");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("value").GetString() ?? string.Empty;
    }

    private static async Task PollAsync(Func<Task<bool>> probe)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(StepTimeout.TotalSeconds * Stopwatch.Frequency);
        Exception? last = null;
        while (Stopwatch.GetTimestamp() < deadline)
        {
            try { if (await probe()) return; } catch (Exception ex) { last = ex; }
            await Task.Delay(PollSlice);
        }
        throw new TimeoutException("E2E polling step timed out.", last);
    }

    private static async Task DeleteSessionAsync(HttpClient http, string session)
    {
        // DELETE with shouldTerminateApp=true closes the App.UI instance launched by this W3C session.
        try { await http.DeleteAsync($"/session/{session}"); } catch { /* cleanup is best effort; the runbook verifies the server */ }
    }

    private static string ExtractW3cSourceXml(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("value", out var value) ||
                value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException();
            }

            var xml = value.GetString();
            if (string.IsNullOrWhiteSpace(xml))
            {
                throw new InvalidDataException();
            }

            return xml;
        }
        catch (JsonException)
        {
            throw new InvalidDataException("W3C source response must contain a non-empty string value.");
        }
        catch (InvalidDataException)
        {
            throw new InvalidDataException("W3C source response must contain a non-empty string value.");
        }
    }

    private static async Task CaptureEvidenceAsync(HttpClient http, string session, string directory)
    {
        var source = ExtractW3cSourceXml(await http.GetStringAsync($"/session/{session}/source"));
        AssertSafeText(source, "live page source");
        await File.WriteAllTextAsync(Path.Combine(directory, "page-source.xml"), Redact(source));

        var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControlParental", "app_startup.log");
        var destination = Path.Combine(directory, "app-startup.log");
        var rawLog = File.Exists(log) ? await File.ReadAllTextAsync(log) : "[log unavailable]";
        var redactedLog = Redact(rawLog);
        AssertSafeText(redactedLog, "application log");
        await File.WriteAllTextAsync(destination, redactedLog);
    }

    private static async Task CaptureScreenshotAsync(HttpClient http, string session, string directory)
    {
        using var json = JsonDocument.Parse(await http.GetStringAsync($"/session/{session}/screenshot"));
        await File.WriteAllBytesAsync(Path.Combine(directory, "registration.png"), Convert.FromBase64String(json.RootElement.GetProperty("value").GetString()!));
    }

    private static void AssertSafeEvidence(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(static path =>
                string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".log", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase)))
        {
            var text = File.ReadAllText(file);
            AssertSafeText(text, file);
        }
    }

    private static void AssertSafeText(string text, string source)
    {
        text = SafePasswordMetadataPattern.Replace(text, string.Empty);
        foreach (var forbidden in ForbiddenFieldNames)
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotMatch(@"(?i)\bbearer\s+[A-Za-z0-9._~+/=-]{12,}", text);
        Assert.DoesNotMatch("(?i)\\b(?:channel[-_ ]?uri|backend[-_ ]?(?:url|body)|(?:api|publishable|secret)[-_ ]?key)\\s*[:=]\\s*[^\\s<>\" ]+", text);
        Assert.DoesNotMatch("(?i)\\bhttps?://[^\\s<>\" ]+", text);
    }

    private static string Redact(string value)
    {
        value = ProtectSafePasswordMetadata(value, out var safeMetadata);
        value = SensitiveAssignmentPattern.Replace(value, "[REDACTED]");
        value = System.Text.RegularExpressions.Regex.Replace(value, @"(?i)\bbearer\s+[A-Za-z0-9._~+/=-]{12,}", "bearer [REDACTED]");
        value = System.Text.RegularExpressions.Regex.Replace(value, "(?i)\\bhttps?://[^\\s<>\" ]+", "[REDACTED-URI]");
        value = SensitiveFieldNamePattern.Replace(value, "[REDACTED]");
        foreach (var forbidden in ForbiddenFieldNames) value = value.Replace(forbidden, "[REDACTED]", StringComparison.OrdinalIgnoreCase);
        return RestoreSafePasswordMetadata(value, safeMetadata);
    }

    private static string ProtectSafePasswordMetadata(string value, out List<string> safeMetadata)
    {
        var protectedMetadata = new List<string>();
        safeMetadata = protectedMetadata;
        return SafePasswordMetadataPattern.Replace(value, match =>
        {
            var token = $"__SAFE_UIA_FALSE_{protectedMetadata.Count}__";
            protectedMetadata.Add(match.Value);
            return token;
        });
    }

    private static string RestoreSafePasswordMetadata(string value, IReadOnlyList<string> safeMetadata)
    {
        for (var index = 0; index < safeMetadata.Count; index++)
        {
            value = value.Replace($"__SAFE_UIA_FALSE_{index}__", safeMetadata[index], StringComparison.Ordinal);
        }

        return value;
    }

    private static string NormalizeState(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
