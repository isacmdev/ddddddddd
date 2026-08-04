// <copyright file="LocalizationTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using ControlParental.App.UI;
using FluentAssertions;
using Xunit;

/// <summary>
/// T26 Fase 8 (PR #8a) — regression tests asserting that user-facing copy
/// is sourced from the canonical WinUI <c>Strings/{es,en-US}/Strings.resw</c>
/// catalog (design.md §10, ADR-007) via <see cref="StringsAdapter"/>
/// rather than hardcoded in XAML or code-behind.
/// </summary>
public sealed class LocalizationTests
{
    [Fact]
    public void StringsAdapterExposesExpectedKeys()
    {
        // The set of properties the XAML layer binds to. If a future locale
        // drops a key the compiler will catch the binding, but the runtime
        // test still asserts the resource keys all resolve — a missing
        // key returns the key name (T25 fallback contract).
        var adapter = StringsAdapter.Instance;
        var properties = typeof(StringsAdapter)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);

        properties.Should().NotBeNull();
        properties.Should().NotBeEmpty();

        var unboundKeys = 0;
        foreach (var property in properties)
        {
            // Skip helper methods (only string properties count).
            if (property.PropertyType != typeof(string))
            {
                continue;
            }

            var value = property.GetValue(adapter) as string;
            value.Should().NotBeNullOrEmpty(
                $"StringsAdapter.{property.Name} must resolve to localized copy.");
            value.Should().NotBe(
                property.Name,
                $"StringsAdapter.{property.Name} returned the key name itself — resource is missing.");
            unboundKeys++;
        }

        unboundKeys.Should().BeGreaterThan(
            10,
            "StringsAdapter must surface at least the onboarding copy keys for P8a verification.");
    }

    [Fact]
    public void OnboardingXamlFilesBindThroughStringsAdapterNoLiteralAcceptOrPairButtons()
    {
        // The audit's primary "remove-hardcoded-Spanish" assertion: the literal
        // "Acepto", "Emparejar", "Crear nueva cuenta", "Iniciar demo",
        // "Activar modo reforzado", "Finalizar onboarding", "Subir el nivel",
        // "Atrás" must NOT appear in the XAML for the onboarding pages.
        var repoRoot = ResolveRepoRoot();

        var forbidden = new[]
        {
            ">Acepto<",
            ">Emparejar<",
            ">Crear nueva cuenta<",
            ">Iniciar demo<",
            ">Activar modo reforzado<",
            ">Finalizar onboarding<",
            ">Subir el nivel<",
            ">Atrás<",
        };

        var xamlPages = new[]
        {
            "ConsentPage.xaml",
            "PairingPage.xaml",
            "AccountStepPage.xaml",
            "DemoStepPage.xaml",
            "ManagedStepPage.xaml",
            "MainWindow.xaml",
        };

        foreach (var page in xamlPages)
        {
            var path = Path.Combine(repoRoot, "src", "ControlParental.App.UI", page);
            File.Exists(path).Should().BeTrue($"Test expects {page} to exist.");

            var content = File.ReadAllText(path);
            foreach (var literal in forbidden)
            {
                content.Should().NotContain(
                    literal,
                    $"{page} still contains the hardcoded literal '{literal}' — Fase 8 must replace it with a {{x:Bind Strings.<Key>}} reference.");
            }
        }
    }

    [Fact]
    public void PairingPageBindsAgeBandsFromStringsAdapterNotLiteralYerCopy()
    {
        // The "7-12 años" literals are the user-visible copy for the ComboBox
        // items. Per P8a the values must be sourced from the resource manager;
        // the literal strings must NOT appear inside `Content="..."` markup.
        // Comments are allowed to keep the ADR-004 explanation in place.
        var repoRoot = ResolveRepoRoot();
        var path = Path.Combine(repoRoot, "src", "ControlParental.App.UI", "PairingPage.xaml");
        var content = File.ReadAllText(path);

        var contentAttributePattern = new Regex(@"Content\s*=\s*""([^""]*)""", RegexOptions.Compiled);
        var matches = contentAttributePattern.Matches(content);
        matches.Should().NotBeEmpty("PairingPage must declare ComboBoxItem Content attributes.");

        foreach (Match match in matches)
        {
            var literal = match.Groups[1].Value;
            literal.Should().NotBe("7-12 años", "PairingPage must bind the 7-12 band through StringsAdapter.");
            literal.Should().NotBe("13-16 años", "PairingPage must bind the 13-16 band through StringsAdapter.");
            literal.Should().NotBe("17-18 años", "PairingPage must bind the 17-18 band through StringsAdapter.");
        }

        foreach (var propertyName in new[] { "AgeBand712", "AgeBand1316", "AgeBand1718" })
        {
            content.Should().Contain($"Strings.{propertyName}");
            typeof(StringsAdapter).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                .Should().NotBeNull($"the compiled XAML binding Strings.{propertyName} must resolve");
        }
    }

    [Fact]
    public void MainWindowBindsProtectionActiveOverlayWithAccents()
    {
        // The historical overlay copy ("Tu proteccion esta activa!") was missing
        // accents and hardcoded. The XAML must now route through
        // Strings.ProtectionActiveOverlay — which is "Tu protección está activa!".
        var repoRoot = ResolveRepoRoot();
        var path = Path.Combine(repoRoot, "src", "ControlParental.App.UI", "MainWindow.xaml");
        var content = File.ReadAllText(path);

        content.Should().NotContain(
            "Tu proteccion esta activa",
            "MainWindow must bind the overlay copy through Strings.ProtectionActiveOverlay (with accents).");
        content.Should().Contain("Strings.ProtectionActiveOverlay");
    }

    [Fact]
    public void DemoStepPageDoesNotHaveUnaccentedHeaderCopy()
    {
        var repoRoot = ResolveRepoRoot();
        var path = Path.Combine(repoRoot, "src", "ControlParental.App.UI", "DemoStepPage.xaml");
        var content = File.ReadAllText(path);

        content.Should().NotContain(
            "Probemos tu proteccion",
            "DemoStepPage must bind the title through Strings.DemoTitle — accents live in the resource file.");
        content.Should().NotContain(
            "Veamos como funciona la proteccion",
            "DemoStepPage description must go through Strings.DemoDescription.");
    }

    [Fact]
    public void StringsAdapterProgressLabelForUsesLocalizedFormat()
    {
        // Verifies the progress format string "Protección {0} de {1}" surfaces
        // accents from the resource (the original literal did, but moved it
        // through the manager keeps accents from regressing).
        var label = StringsAdapter.Instance.ProgressLabelFor(2, 5);
        label.Should().Be("Protección 2 de 5");
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
