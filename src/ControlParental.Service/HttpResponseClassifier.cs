// <copyright file="HttpResponseClassifier.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Net;
using System.Text.Json;
using ControlParental.Domain;

/// <summary>
/// Centralizes HTTP response classification into <see cref="HttpOutcome"/>.
/// </summary>
public static class HttpResponseClassifier
{
    /// <summary>
    /// Classifies an HTTP response.
    /// </summary>
    /// <param name="response">HTTP response.</param>
    /// <returns>The outcome.</returns>
    public static HttpOutcome Classify(HttpResponseMessage? response)
    {
        if (response == null)
        {
            return HttpOutcome.Network;
        }

        var statusCode = (int)response.StatusCode;

        if (statusCode >= 200 && statusCode < 300)
        {
            return HttpOutcome.Success;
        }

        if (statusCode >= 400 && statusCode < 500)
        {
            return HttpOutcome.Permanent;
        }

        if (statusCode >= 500 && statusCode < 600)
        {
            return HttpOutcome.Transient;
        }

        return HttpOutcome.Transient;
    }

    /// <summary>
    /// Classifies an exception thrown during the HTTP request.
    /// </summary>
    /// <param name="exception">Exception thrown.</param>
    /// <param name="cancellationToken">The cancellation token for the request.</param>
    /// <returns>The outcome.</returns>
    public static HttpOutcome Classify(Exception exception, CancellationToken cancellationToken)
    {
        return exception switch
        {
            HttpRequestException => HttpOutcome.Network,
            TaskCanceledException tce when tce.CancellationToken != cancellationToken => HttpOutcome.Transient,
            JsonException => HttpOutcome.Malformed,
            _ => HttpOutcome.Transient,
        };
    }
}
