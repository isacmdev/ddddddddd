// <copyright file="ConfigurationLoader.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Loads the service-owned Supabase configuration without exposing bearer keys.
/// </summary>
internal static class ConfigurationLoader
{
    private const string UrlName = "SUPABASE_URL";
    private const string KeyName = "SUPABASE_ANON_KEY";
    private const string CertPinsName = "SUPABASE_CERT_PINS";
    private const string LegacyCertPinName = "SUPABASE_CERT_PIN";

    /// <summary>
    /// Gets the production configuration path. Repository .env is retained only for local development.
    /// </summary>
    public static string EnvFilePath
    {
        get
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (var i = 0; i < 12; i++)
            {
                var candidate = Path.Combine(dir, ".env");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                var parent = Directory.GetParent(dir);
                if (parent == null)
                {
                    break;
                }

                dir = parent.FullName;
            }

            return Path.Combine(Program.DataFolderPath, ".env");
        }
    }

    /// <summary>
    /// Parses configuration text. Diagnostics are deliberately key-free.
    /// </summary>
    public static ConfigurationParseResult ParseFile(string contents)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var lineNumber = 0;
        foreach (var rawLine in contents.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                return ConfigurationParseResult.Invalid($"invalid configuration line {lineNumber}");
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"');
            if (!string.Equals(name, UrlName, StringComparison.Ordinal) &&
                !string.Equals(name, KeyName, StringComparison.Ordinal) &&
                !string.Equals(name, CertPinsName, StringComparison.Ordinal) &&
                !string.Equals(name, LegacyCertPinName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!values.TryAdd(name, value))
            {
                return ConfigurationParseResult.Invalid($"duplicate configuration key {name}");
            }
        }

        values.TryGetValue(UrlName, out var url);
        values.TryGetValue(KeyName, out var anonKey);
        values.TryGetValue(CertPinsName, out var certPins);
        if (values.TryGetValue(LegacyCertPinName, out var legacyPin))
        {
            certPins ??= legacyPin;
        }

        return Validate(url, anonKey, certPins);
    }

    /// <summary>
    /// Loads configuration from the selected file and applies only local-development overrides.
    /// </summary>
    public static bool TryLoad(out SupabaseConfig config)
    {
        config = default!;
        var path = EnvFilePath;
        if (!File.Exists(path))
        {
            return false;
        }

        ConfigurationParseResult parsed;
        try
        {
            parsed = ParseFile(File.ReadAllText(path));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        if (!parsed.IsValid || parsed.Config is null)
        {
            return false;
        }

        var localUrl = Environment.GetEnvironmentVariable("SUPABASE_URL_LOCAL");
        var localKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY_LOCAL");
        var effective = Validate(
            string.IsNullOrWhiteSpace(localUrl) ? parsed.Config.Url : localUrl,
            string.IsNullOrWhiteSpace(localKey) ? parsed.Config.AnonKey : localKey,
            parsed.Config.CertPins);
        if (!effective.IsValid || effective.Config is null)
        {
            return false;
        }

        config = effective.Config;
        if (!string.IsNullOrWhiteSpace(config.CertPins))
        {
            Environment.SetEnvironmentVariable(CertPinsName, config.CertPins);
            Environment.SetEnvironmentVariable(LegacyCertPinName, config.CertPins);
        }
        return true;
    }

    private static ConfigurationParseResult Validate(string? url, string? anonKey, string? certPins)
    {
        url = url?.Trim();
        anonKey = anonKey?.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl) ||
            !string.Equals(parsedUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parsedUrl.Host))
        {
            return ConfigurationParseResult.Invalid("SUPABASE_URL must be an HTTPS URL");
        }

        if (!IsPublishableKey(anonKey))
        {
            return ConfigurationParseResult.Invalid("SUPABASE_ANON_KEY is missing or not a publishable key");
        }

        if (!string.IsNullOrWhiteSpace(certPins) && !IsValidPins(certPins))
        {
            return ConfigurationParseResult.Invalid("SUPABASE_CERT_PINS is invalid");
        }

        return new ConfigurationParseResult(true, new SupabaseConfig(url, anonKey!, certPins), null, "configuration accepted");
    }

    private static bool IsPublishableKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("YOUR-", StringComparison.OrdinalIgnoreCase) || value.StartsWith("sb_secret_", StringComparison.Ordinal))
        {
            return false;
        }

        if (value.StartsWith("sb_publishable_", StringComparison.Ordinal) || value.StartsWith("sb_anon_", StringComparison.Ordinal))
        {
            return value.Length >= 20;
        }

        var parts = value.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
            return json.RootElement.TryGetProperty("role", out var role) &&
                role.ValueKind == JsonValueKind.String &&
                string.Equals(role.GetString(), "anon", StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsValidPins(string pins)
    {
        foreach (var pin in pins.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!pin.StartsWith("sha256/", StringComparison.Ordinal) ||
                !Convert.TryFromBase64String(pin[7..], new byte[32], out var written) || written != 32)
            {
                return false;
            }
        }

        return pins.Contains("sha256/", StringComparison.Ordinal);
    }
}

/// <summary>Safe result of parsing service configuration.</summary>
internal sealed record ConfigurationParseResult(
    bool IsValid,
    SupabaseConfig? Config,
    string? Error,
    string Diagnostic)
{
    public static ConfigurationParseResult Invalid(string error) =>
        new(false, null, error, "configuration rejected");
}

/// <summary>Supabase connection configuration.</summary>
public sealed record SupabaseConfig(string Url, string AnonKey, string? CertPins = null);
