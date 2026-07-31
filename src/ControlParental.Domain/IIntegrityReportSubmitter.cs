// <copyright file="IIntegrityReportSubmitter.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Capability seam for submitting integrity reports to the backend.
/// A null implementation (or no registration) means shadow mode: evidence is
/// collected locally but not submitted.
/// </summary>
public interface IIntegrityReportSubmitter
{
    /// <summary>
    /// Submits an integrity report to the backend.
    /// </summary>
    /// <param name="report">Report to submit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the submission.</returns>
    Task<IntegrityReportResult> SubmitAsync(
        IntegrityReport report,
        CancellationToken cancellationToken = default);
}
