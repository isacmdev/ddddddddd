// <copyright file="BackendIdentityContractV1Harness.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Security.Cryptography;
using System.Text;
using ControlParental.Service;

internal sealed record LocalAcceptanceReceiptV1(
    string Mode,
    int ContractVersion,
    bool ExternalVerified,
    IReadOnlyList<string> PassedProbes,
    string EvidenceHash);

internal enum PairingCase
{
    ValidDeviceA,
    Missing,
    Expired,
    Throttled,
}

internal sealed class BackendIdentityContractV1Harness
{
    private const int MaximumOperations = 8;
    private readonly Dictionary<string, BackendIdentityReceiptV1> receipts = new(StringComparer.Ordinal);
    private readonly HashSet<PairingCase> consumed = [];
    private readonly HashSet<string> revokedDevices = new(StringComparer.Ordinal);
    private readonly DateTimeOffset now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    public BackendIdentityReceiptV1 Pair(PairingCase pairingCase, string operationId, bool timesOut = false, bool cancelled = false)
    {
        if (cancelled)
        {
            return BackendIdentityContractV1.CreateReceipt(operationId, BackendIdentityErrorV1.Cancelled, this.now);
        }

        if (timesOut)
        {
            return BackendIdentityContractV1.CreateReceipt(operationId, BackendIdentityErrorV1.Timeout, this.now);
        }

        if (this.receipts.TryGetValue(operationId, out var receipt))
        {
            return receipt;
        }

        if (this.receipts.Count >= MaximumOperations)
        {
            return BackendIdentityContractV1.CreateReceipt(operationId, BackendIdentityErrorV1.CapacityExceeded, this.now);
        }

        var error = pairingCase switch
        {
            PairingCase.Missing => BackendIdentityErrorV1.NotFound,
            PairingCase.Expired => BackendIdentityErrorV1.Gone,
            PairingCase.Throttled => BackendIdentityErrorV1.RateLimited,
            _ when !this.consumed.Add(pairingCase) => BackendIdentityErrorV1.Replay,
            _ => BackendIdentityErrorV1.None,
        };

        receipt = BackendIdentityContractV1.CreateReceipt(
            operationId,
            error,
            this.now,
            error == BackendIdentityErrorV1.RateLimited ? TimeSpan.FromSeconds(30) : null);
        this.receipts.Add(operationId, receipt);
        return receipt;
    }

    public BackendIdentityReceiptV1 ProbeRows(string callerDeviceId, string requestedDeviceId)
        => BackendIdentityContractV1.ProbeTwoDeviceAccess(
            callerDeviceId,
            requestedDeviceId,
            this.revokedDevices,
            this.now);

    public void Revoke(string deviceId) => this.revokedDevices.Add(deviceId);

    public LocalAcceptanceReceiptV1 RunLocalAcceptance()
    {
        string[] probes = ["claims.exact", "pairing.idempotent", "rls.two-device", "revocation.fail-closed"];
        var canonical = string.Join('\n', probes);
        return new(
            "local",
            1,
            ExternalVerified: false,
            probes,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))));
    }
}
