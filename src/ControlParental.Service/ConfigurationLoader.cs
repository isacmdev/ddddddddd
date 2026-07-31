// <copyright file="ConfigurationLoader.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

/// <summary>
/// Loads configuration from .env file.
/// Searches in this order:
/// 1. Repo root (next to .sln) — for development
/// 2. ProgramData\ControlParental\ — for production Windows Service
/// Supports local dev overrides via SUPABASE_URL_LOCAL / SUPABASE_ANON_KEY_LOCAL.
/// </summary>
internal static class ConfigurationLoader
{
    /// <summary>
    /// Searches for .env starting at the compiled output directory,
    /// going up to the repo root, then falling back to ProgramData.
    /// </summary>
    public static string EnvFilePath
    {
        get
        {
            // Try repo root first (next to .sln), going up from bin/
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (var i = 0; i < 12; i++)
            {
                var candidate = Path.Combine(dir, ".env");
                Console.Error.WriteLine(
                    $"[ConfigurationLoader] Checking .env at: {candidate} (exists: {File.Exists(candidate)})");
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

            // Fallback: ProgramData (production)
            Console.Error.WriteLine(
                "[ConfigurationLoader] .env not found walking up — using ProgramData fallback.");
            return Path.Combine(Program.DataFolderPath, ".env");
        }
    }

    /// <summary>
    /// Loads Supabase configuration from .env.
    /// Returns true if values were loaded, false if .env was not found.
    /// </summary>
    public static bool TryLoad(out SupabaseConfig config)
    {
        config = default;

        if (!File.Exists(EnvFilePath))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ConfigurationLoader] .env not found at {EnvFilePath}. " +
                "Copy .env.example to that location and configure your Supabase credentials.");
            return false;
        }

        // DotNetEnv.Env.Load(string path) — idempotent, safe to call multiple times.
        DotNetEnv.Env.Load(EnvFilePath);

        // Local dev overrides take precedence; otherwise fall back to production values.
        var url = Environment.GetEnvironmentVariable("SUPABASE_URL_LOCAL")
                  ?? Environment.GetEnvironmentVariable("SUPABASE_URL");

        var anonKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY_LOCAL")
                      ?? Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(anonKey))
        {
            System.Diagnostics.Debug.WriteLine(
                "[ConfigurationLoader] SUPABASE_URL or SUPABASE_ANON_KEY is not set in .env.");
            return false;
        }

        config = new SupabaseConfig(url.Trim(), anonKey.Trim());
        System.Diagnostics.Debug.WriteLine($"[ConfigurationLoader] Loaded Supabase config from {EnvFilePath}.");
        return true;
    }
}

/// <summary>
/// Supabase connection configuration.
/// </summary>
/// <param name="Url">Supabase project URL.</param>
/// <param name="AnonKey">Supabase anonymous (publishable) key.</param>
internal record SupabaseConfig(string Url, string AnonKey);
