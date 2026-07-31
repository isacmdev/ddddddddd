// <copyright file="HttpResponseClassifierTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Net;
using System.Text.Json;
using ControlParental.Domain;
using Xunit;

/// <summary>
/// Coverage for <see cref="HttpResponseClassifier"/> — the central piece of the
/// T14/B3 HTTP boundary contract. The full status-code/exceptions table is
/// pinned here so future refactors cannot silently change classification.
/// </summary>
public class HttpResponseClassifierTests
{
    // ── Response classification ──────────────────────────────────────────

    [Fact]
    public void Classify_NullResponse_ReturnsNetwork()
    {
        var outcome = HttpResponseClassifier.Classify(null);

        Assert.Equal(HttpOutcome.Network, outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.Accepted)]
    public void Classify_2xx_ReturnsSuccess(HttpStatusCode status)
    {
        var outcome = HttpResponseClassifier.Classify(new HttpResponseMessage(status));

        Assert.Equal(HttpOutcome.Success, outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public void Classify_4xx_ReturnsPermanent(HttpStatusCode status)
    {
        var outcome = HttpResponseClassifier.Classify(new HttpResponseMessage(status));

        Assert.Equal(HttpOutcome.Permanent, outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void Classify_5xx_ReturnsTransient(HttpStatusCode status)
    {
        var outcome = HttpResponseClassifier.Classify(new HttpResponseMessage(status));

        Assert.Equal(HttpOutcome.Transient, outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.Continue)]
    [InlineData(HttpStatusCode.Ambiguous)]
    [InlineData(HttpStatusCode.MultipleChoices)]
    [InlineData(HttpStatusCode.Moved)]
    [InlineData(HttpStatusCode.NotImplemented)]
    public void Classify_OtherCodes_DefaultToTransient(HttpStatusCode status)
    {
        var outcome = HttpResponseClassifier.Classify(new HttpResponseMessage(status));

        Assert.Equal(HttpOutcome.Transient, outcome);
    }

    // ── Exception classification ─────────────────────────────────────────

    [Fact]
    public void Classify_HttpRequestException_ReturnsNetwork()
    {
        var outcome = HttpResponseClassifier.Classify(
            new HttpRequestException("connection refused"),
            CancellationToken.None);

        Assert.Equal(HttpOutcome.Network, outcome);
    }

    [Fact]
    public void Classify_TaskCanceled_WithMatchingToken_ReturnsTransient()
    {
        using var cts = new CancellationTokenSource();
        // Same token the call site would use → not a server-side timeout.
        var outcome = HttpResponseClassifier.Classify(
            new TaskCanceledException(),
            cts.Token);

        Assert.Equal(HttpOutcome.Transient, outcome);
    }

    [Fact]
    public void Classify_TaskCanceled_WithDifferentToken_ReturnsTransient()
    {
        using var cts = new CancellationTokenSource();
        // Different token → server-side timeout, classified transient.
        var outcome = HttpResponseClassifier.Classify(
            new TaskCanceledException(),
            CancellationToken.None);

        Assert.Equal(HttpOutcome.Transient, outcome);
    }

    [Fact]
    public void Classify_JsonException_ReturnsMalformed()
    {
        var outcome = HttpResponseClassifier.Classify(
            new JsonException("unexpected token"),
            CancellationToken.None);

        Assert.Equal(HttpOutcome.Malformed, outcome);
    }

    [Fact]
    public void Classify_GenericException_ReturnsTransient()
    {
        var outcome = HttpResponseClassifier.Classify(
            new InvalidOperationException("oops"),
            CancellationToken.None);

        Assert.Equal(HttpOutcome.Transient, outcome);
    }
}
