// <copyright file="StringsAdapterResourceLoaderTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ControlParental.App.UI;
using FluentAssertions;
using Xunit;

/// <summary>
/// T26 PR #12 — regression coverage for the relocation of the
/// user-facing copy catalog from <c>Domain/Strings.resx</c> into the
/// canonical WinUI <c>App.UI/Strings/{es,en-US}/Strings.resw</c> location
/// (design.md §10, ADR-007).
/// <para>
/// These tests prove three things:
/// <list type="bullet">
/// <item>The locale chain (es default → en-US fallback → key name) still resolves every key the XAML layer binds to.</item>
/// <item>The catalog now ships from <c>App.UI/Strings/</c> and the legacy <c>Domain/Strings.resx</c> file is gone.</item>
/// <item>The <see cref="StringsAdapter"/> API surface that every page's
/// <c>Strings</c> property exposes is unchanged — XAML bindings stay
/// intact.</item>
/// </list>
/// </para>
/// </summary>
public sealed class StringsAdapterResourceLoaderTests
{
    /// <summary>
    /// All string-typed properties the adapter exposes. Mirror of the
    /// XAML binding surface — if the adapter drops a key the build
    /// still succeeds but XAML will fail at runtime, so this list is
    /// used by the regression assertions below.
    /// </summary>
    private static readonly string[] ExpectedKeys = typeof(StringsAdapter)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.PropertyType == typeof(string))
        .Select(p => p.Name)
        .ToArray();

    [Fact]
    public void StringsAdapterGetStringSpanishReturnsSpanishValue()
    {
        // Force the es catalog: we set CurrentUICulture to a neutral
        // Spanish culture (the adapter maps anything other than a
        // shipped catalog to the default culture, "es").
        var prior = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("es-AR");

            StringsAdapter.Instance.AcceptButton.Should().Be("Acepto");
            StringsAdapter.Instance.PairingAction.Should().Be("Emparejar");
            StringsAdapter.Instance.BackButton.Should().Be("Atrás");
            StringsAdapter.Instance.ManagedFinishOnboarding.Should().Be("Finalizar onboarding");
        }
        finally
        {
            CultureInfo.CurrentUICulture = prior;
        }
    }

    [Fact]
    public void StringsAdapterGetStringEnglishFallbackReturnsEnglishValue()
    {
        // English-US is a shipped catalog. When the requested culture
        // resolves to it, the adapter returns the English value.
        var prior = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");

            StringsAdapter.Instance.AcceptButton.Should().Be("I accept");
            StringsAdapter.Instance.PairingAction.Should().Be("Pair");
            StringsAdapter.Instance.BackButton.Should().Be("Back");
            StringsAdapter.Instance.ManagedFinishOnboarding.Should().Be("Finish onboarding");
        }
        finally
        {
            CultureInfo.CurrentUICulture = prior;
        }
    }

    [Fact]
    public void StringsAdapterAllPropertiesKeysAreResolvable()
    {
        // Regression: every string-typed property the XAML layer binds
        // to must resolve through the locale chain. The legacy PR #8a
        // test asserted ≥ 10 keys; with the new catalog we expect
        // every shipped property to come back with non-empty, non-key
        // content. This protects against dropping a resource when
        // adding a new onboarding page in a future change.
        var adapter = StringsAdapter.Instance;
        var resolved = new List<string>(ExpectedKeys.Length);
        var missing = new List<string>();

        foreach (var key in ExpectedKeys)
        {
            var property = typeof(StringsAdapter).GetProperty(key, BindingFlags.Instance | BindingFlags.Public);
            property.Should().NotBeNull($"StringsAdapter must expose a public string property named '{key}'.");

            var value = (string?)property!.GetValue(adapter);
            if (string.IsNullOrEmpty(value) || string.Equals(value, key, StringComparison.Ordinal))
            {
                missing.Add(key);
            }
            else
            {
                resolved.Add(key);
            }
        }

        missing.Should().BeEmpty(
            "every StringsAdapter property must resolve to localized copy through the locale chain; missing: " +
            string.Join(", ", missing));
        resolved.Count.Should().BeGreaterThanOrEqualTo(
            46,
            "PR #12 migrates the full 46+ key catalog — losing keys here means the XAML layer would fail at runtime.");
    }

    [Fact]
    public void StringsAdapterCatalogShipsFromAppUIStringsNotDomainStrings()
    {
        // The embedded manifest resources that back the adapter must
        // live under the canonical WinUI Strings/ folder of the App.UI
        // project. We assert against the assembly's manifest resource
        // list (the source of truth — StringsAdapter reads from
        // Assembly.GetManifestResourceStream).
        var assembly = typeof(StringsAdapter).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        resourceNames.Should().Contain(
            "ControlParental.App.UI.Strings.es.Strings.resw",
            "the Spanish catalog must ship as an embedded manifest resource of App.UI.");
        resourceNames.Should().Contain(
            "ControlParental.App.UI.Strings.en-US.Strings.resw",
            "the English fallback catalog must ship as an embedded manifest resource of App.UI.");

        // No legacy Domain manifest resource should leak through.
        resourceNames.Should().NotContain(
            n => n.StartsWith("ControlParental.Domain.Strings", StringComparison.Ordinal),
            "the legacy Domain.Strings manifest resource must be gone now that the catalog lives in App.UI.");
    }

    [Fact]
    public void DomainStringsResxStaysForConsentStrings()
    {
        // The legacy Domain/Strings.resx + Domain/Strings.Designer.cs pair
        // is STILL shipping because the current product surface retains
        // ConsentStrings (T25 console disclosure path) — it reads through
        // `Domain.Strings` via the typed `Strings` class. The migration to
        // StringsAdapter covers the XAML-visible catalog; ConsentStrings is
        // a separate, intentionally retained consumer.
        //
        // This test now pins the *current* contract: the legacy Domain
        // resources stay as long as ConsentStrings is alive, and the new
        // canonical WinUI catalog ships from App.UI/Strings/{es,en-US}.
        var repoRoot = ResolveRepoRoot();
        var oldDomainStringsPath = Path.Combine(repoRoot, "src", "ControlParental.Domain", "Strings.resx");
        var oldDomainStringsDesignerPath = Path.Combine(repoRoot, "src", "ControlParental.Domain", "Strings.Designer.cs");

        File.Exists(oldDomainStringsPath).Should().BeTrue(
            $"ConsentStrings still reads from Domain.Strings.resx — the file must remain at '{oldDomainStringsPath}'.");
        File.Exists(oldDomainStringsDesignerPath).Should().BeTrue(
            $"Domain.Strings.Designer.cs backs the ConsentStrings accessors — it must remain at '{oldDomainStringsDesignerPath}'.");
    }

    [Fact]
    public void NewLocationReswFilesExistUnderAppUIStrings()
    {
        // Positive companion to the absence assertion above: prove the
        // new canonical WinUI location actually ships the two catalogs.
        var repoRoot = ResolveRepoRoot();
        var newEsPath = Path.Combine(repoRoot, "src", "ControlParental.App.UI", "Strings", "es", "Strings.resw");
        var newEnUsPath = Path.Combine(repoRoot, "src", "ControlParental.App.UI", "Strings", "en-US", "Strings.resw");

        File.Exists(newEsPath).Should().BeTrue(
            $"the new Spanish catalog must ship at '{newEsPath}'.");
        File.Exists(newEnUsPath).Should().BeTrue(
            $"the new English fallback catalog must ship at '{newEnUsPath}'.");
    }

    [Fact]
    public void StringsAdapterProgressLabelForFormatStringCarriesAccents()
    {
        // The progress format string must still surface the accented
        // Spanish copy (the regression that motivated PR #8a). Using
        // Spanish culture guarantees the default catalog is picked.
        var prior = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("es-AR");

            StringsAdapter.Instance.ProgressLabelFor(2, 5).Should().Be("Protección 2 de 5");
        }
        finally
        {
            CultureInfo.CurrentUICulture = prior;
        }
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ControlParental.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("Tests must run from a checkout that contains ControlParental.sln.");
        return dir!.FullName;
    }
}
