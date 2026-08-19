// <copyright file="WnsRegistrationNavigationActivationContractTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.E2E.Tests;

using System.Net;
using System.Reflection;
using Xunit;

public sealed class WnsRegistrationNavigationActivationContractTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public async Task WnsRegistrationNavigationButton_Activation_UsesNovaWindows2InvokePattern()
    {
        var originalAutomationName = Environment.GetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME");
        Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME", "NovaWindows2");

        try
        {
            await this.AssertActivationRequestAsync(
                expectedPath: "/session/known-session-id/execute/sync",
                expectedPayload: "{\"script\":\"windows: invoke\",\"args\":[{\"element-6066-11e4-a52e-4f735466cecf\":\"known-navigation-element-id\"}]}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME", originalAutomationName);
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task WnsRegistrationNavigationButton_Activation_PreservesWindowsClick()
    {
        var originalAutomationName = Environment.GetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME");
        Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME", null);

        try
        {
            await this.AssertActivationRequestAsync(
                expectedPath: "/session/known-session-id/element/known-navigation-element-id/click",
                expectedPayload: null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME", originalAutomationName);
        }
    }

    private async Task AssertActivationRequestAsync(string expectedPath, string? expectedPayload)
    {
        const string sessionId = "known-session-id";
        const string elementId = "known-navigation-element-id";
        using var handler = new CapturingHttpMessageHandler();
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:4723"),
        };

        var helper = typeof(WnsRegistrationE2ETests).GetMethod(
            "ClickElementAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(WnsRegistrationE2ETests), "ClickElementAsync");
        var invocation = helper.Invoke(null, [http, sessionId, elementId]) as Task
            ?? throw new InvalidOperationException("ClickElementAsync did not return a Task.");

        await invocation;

        var usesDirectInvoke =
            string.Equals(handler.RequestPath, expectedPath, StringComparison.Ordinal) &&
            string.Equals(handler.RequestBody, expectedPayload, StringComparison.Ordinal) &&
            (expectedPath.EndsWith("/execute/sync", StringComparison.Ordinal)
                ? !handler.RequestPaths.Any(static path => path.Contains("/element/", StringComparison.Ordinal) && path.EndsWith("/click", StringComparison.Ordinal))
                : handler.RequestPaths.Count == 1);

        Assert.True(
            usesDirectInvoke,
            $"Expected {expectedPath} with payload {expectedPayload ?? "<empty>"}; observed {handler.RequestPath} {handler.RequestBody}. Click requests: {string.Join(", ", handler.RequestPaths)}");
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public List<string> RequestPaths { get; } = [];

        public string? RequestPath { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri?.AbsolutePath;
            RequestPaths.Add(RequestPath ?? string.Empty);
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
