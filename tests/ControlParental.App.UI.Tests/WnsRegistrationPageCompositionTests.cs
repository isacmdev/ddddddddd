// <copyright file="WnsRegistrationPageCompositionTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

/// <summary>
/// Strict-TDD RED contract for the smallest observable WNS registration surface.
/// These tests intentionally describe the page/composition seam before its
/// production XAML and navigation wiring exist.
/// </summary>
public sealed class WnsRegistrationPageCompositionTests
{
    [Fact]
    public void WnsRegistrationPage_Xaml_ExposesStableAutomationIdsAndSafeBindings()
    {
        var xaml = ReadAppUiFile("WnsRegistrationPage.xaml");

        Assert.Contains("AutomationProperties.AutomationId=\"WnsRegistrationButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"WnsRegistrationStatus\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"WnsRegistrationProgress\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding StatusText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsActive=\"{Binding IsBusy}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void WnsRegistrationPage_Xaml_StatusAutomationContract_UsesTypedAccessibleState()
    {
        var document = XDocument.Parse(ReadAppUiFile("WnsRegistrationPage.xaml"));

        var statusOwners = document.Descendants()
            .Where(element => (string?)element.Attribute("AutomationProperties.AutomationId") == "WnsRegistrationStatus")
            .ToArray();

        Assert.Single(statusOwners);
        var status = statusOwners[0];
        Assert.Equal("TextBlock", status.Name.LocalName);
        Assert.True((string?)status.Attribute("Visibility") is null
            or "Visible");
        Assert.Equal("{Binding StatusText}", (string?)status.Attribute("Text"));
        Assert.Equal("{Binding StatusText}", (string?)status.Attribute("AutomationProperties.Name"));
        Assert.Equal("Registration status", (string?)status.Attribute("AutomationProperties.HelpText"));
    }

    [Fact]
    public void WnsRegistrationPage_Xaml_UsesStableVisibleProgressContainerWithoutClaimingActivity()
    {
        var document = XDocument.Parse(ReadAppUiFile("WnsRegistrationPage.xaml"));

        var progressIdOwners = document.Descendants()
            .Where(element => (string?)element.Attribute("AutomationProperties.AutomationId") == "WnsRegistrationProgress")
            .ToArray();

        Assert.Single(progressIdOwners);
        var progressContainer = progressIdOwners[0];
        Assert.NotEqual("ProgressRing", progressContainer.Name.LocalName);
        Assert.Equal("Visible", (string?)progressContainer.Attribute("Visibility"));
        Assert.Equal("Registration in progress", (string?)progressContainer.Attribute("AutomationProperties.Name"));

        var progressRings = progressContainer.Descendants()
            .Where(element => element.Name.LocalName == "ProgressRing")
            .ToArray();

        Assert.Single(progressRings);
        Assert.Equal("{Binding IsBusy}", (string?)progressRings[0].Attribute("IsActive"));
        Assert.DoesNotContain(
            document.Descendants().Where(element => element.Name.LocalName == "ProgressRing"),
            element => (string?)element.Attribute("AutomationProperties.AutomationId") == "WnsRegistrationProgress");
    }

    [Fact]
    public void WnsRegistrationPage_CodeBehind_InvokesExactlyOneAsyncRegistrationActionThroughViewModel()
    {
        var codeBehind = ReadAppUiFile("WnsRegistrationPage.xaml.cs");

        Assert.Equal(1, CountOccurrences(codeBehind, "RegisterAsync("));
        Assert.DoesNotContain("IWnsRegistrationPort", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterChannelAsync", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void WnsRegistrationPage_Xaml_DoesNotBindOrRenderTransportOperationOrCredentialFields()
    {
        var xaml = ReadAppUiFile("WnsRegistrationPage.xaml");
        var forbiddenBindingsOrFields = new[]
        {
            "ChannelUri",
            "Uri",
            "OperationId",
            "CorrelationId",
            "Backend",
            "Credential",
            "Secret",
            "AccessToken",
            "Password",
            "HttpClient",
            "Supabase",
        };

        foreach (var forbidden in forbiddenBindingsOrFields)
        {
            Assert.DoesNotContain(forbidden, xaml, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void App_Composition_RegistersWnsPortAndViewModelWithoutBackendClientConstruction()
    {
        var appSource = ReadAppUiFile("App.xaml.cs");
        var pageCodeBehind = ReadAppUiFile("WnsRegistrationPage.xaml.cs");

        Assert.Contains("AddSingleton<IWnsRegistrationPort, WnsPushNotificationHandler>", appSource, StringComparison.Ordinal);
        Assert.Contains("WnsRegistrationViewModel", appSource, StringComparison.Ordinal);
        Assert.Contains("GetRequiredService<WnsRegistrationViewModel>", pageCodeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("new HttpClient", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BackendClient", appSource, StringComparison.Ordinal);
        Assert.Contains("SupabaseRealtimeComposition.Create", appSource, StringComparison.Ordinal);
        Assert.Contains("CreateFromEnvironment", appSource, StringComparison.Ordinal);
        Assert.Contains("channels.DeviceId", appSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ExposesDeterministicWnsRegistrationEntryThroughPageHost()
    {
        var shellSource = ReadAppUiFile("MainWindow.xaml.cs");
        var shellXaml = ReadAppUiFile("MainWindow.xaml");

        // The RED contract deliberately chooses the existing PageHost as the
        // Appium entry: a stable shell action must mount the dedicated page,
        // rather than overloading ServiceSetupPage or relying on timing.
        Assert.Contains("AutomationProperties.AutomationId=\"WnsRegistrationNavigationButton\"", shellXaml, StringComparison.Ordinal);
        Assert.Contains("WnsRegistrationPage", shellSource, StringComparison.Ordinal);
        Assert.Contains("OnWnsRegistrationRequested", shellSource, StringComparison.Ordinal);
        Assert.Contains("PageHost.Content", shellSource, StringComparison.Ordinal);
    }

    private static string ReadAppUiFile(string fileName)
    {
        var repoRoot = RepositoryRootLocator.Locate(typeof(WnsRegistrationPageCompositionTests));
        return File.ReadAllText(Path.Combine(repoRoot, "src", "ControlParental.App.UI", fileName));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

}
