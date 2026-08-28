// <copyright file="BackendIdentityAcceptancePackageTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

public sealed class BackendIdentityAcceptancePackageTests
{
    [Fact]
    public void LocalHarnessReceipt_IsDeterministicRedactedAndNeverExternallyVerified()
    {
        var first = new BackendIdentityContractV1Harness().RunLocalAcceptance();
        var second = new BackendIdentityContractV1Harness().RunLocalAcceptance();

        Assert.Equal(first.Mode, second.Mode);
        Assert.Equal(first.ContractVersion, second.ContractVersion);
        Assert.Equal(first.ExternalVerified, second.ExternalVerified);
        Assert.Equal(first.PassedProbes, second.PassedProbes);
        Assert.Equal(first.EvidenceHash, second.EvidenceHash);
        Assert.False(first.ExternalVerified);
        Assert.Equal("local", first.Mode);
        Assert.DoesNotContain("token", JsonSerializer.Serialize(first), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AcceptanceManifestAndSchema_FixLocalEvidenceToExternalVerifiedFalse()
    {
        var root = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "openspec/changes/backend-identity-contract/evidence/external-acceptance-manifest.v1.json")));
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "openspec/changes/backend-identity-contract/evidence/local-receipt.schema.v1.json")));

        Assert.Equal(1, manifest.RootElement.GetProperty("contractVersion").GetInt32());
        Assert.Equal("backend-integration-acceptance", manifest.RootElement.GetProperty("futureExecutionOwner").GetString());
        Assert.False(manifest.RootElement.GetProperty("externalVerified").GetBoolean());
        Assert.False(schema.RootElement.GetProperty("properties").GetProperty("ExternalVerified").GetProperty("const").GetBoolean());
        Assert.Contains("required", schema.RootElement.EnumerateObject().Select(x => x.Name));
        foreach (var artifact in manifest.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            var bytes = File.ReadAllBytes(Path.Combine(root, artifact.GetProperty("path").GetString()!));
            Assert.Equal(artifact.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)));
        }
    }

    [Fact]
    public void CompatibilityReceipts_PreservePausedMatrixAndScopedWin11Evidence()
    {
        var root = FindRepositoryRoot();
        var currentReceiptPath = Path.Combine(root, "openspec/changes/backend-identity-contract/evidence/wns-e2e-compatibility-receipt.current.json");
        using var currentReceipt = JsonDocument.Parse(File.ReadAllText(currentReceiptPath));
        var current = currentReceipt.RootElement;

        Assert.Equal(1, current.GetProperty("schemaVersion").GetInt32());
        Assert.False(current.GetProperty("externalVerified").GetBoolean());
        Assert.Equal("paused-by-user-pending-prerequisites", current.GetProperty("matrixStatus").GetString());
        Assert.Equal("paused-by-user", current.GetProperty("pause").GetProperty("status").GetString());
        Assert.Equal(1, current.GetProperty("matrix").GetProperty("passed").GetInt32());
        Assert.Equal(3, current.GetProperty("matrix").GetProperty("total").GetInt32());
        Assert.Equal(2, current.GetProperty("pause").GetProperty("pendingPrerequisites").GetArrayLength());

        var cells = current.GetProperty("matrix").GetProperty("cells").EnumerateArray().ToArray();
        var passedCells = cells.Where(cell => cell.GetProperty("status").GetString() == "passed").ToArray();
        Assert.Single(passedCells);
        Assert.Equal("Windows 11 25H2 x64", passedCells[0].GetProperty("cell").GetString());
        var linkedCellReceipt = passedCells[0].GetProperty("receipt").GetString()!;
        Assert.Equal("evidence/win11-25h2-x64-cell-receipt.json", linkedCellReceipt);
        Assert.True(File.Exists(Path.Combine(root, "openspec/changes/backend-identity-contract", linkedCellReceipt.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal(2, cells.Count(cell => cell.GetProperty("status").GetString() == "blocked"));
        AssertExcludedClaims(
            current,
            "client-ready",
            "full matrix support",
            "backend",
            "JWT",
            "RLS",
            "TLS",
            "WNS fan-out",
            "ExternalVerified");

        var cellReceiptPath = Path.Combine(root, "openspec/changes/backend-identity-contract", linkedCellReceipt.Replace('/', Path.DirectorySeparatorChar));
        using var cellReceipt = JsonDocument.Parse(File.ReadAllText(cellReceiptPath));
        var cell = cellReceipt.RootElement;
        Assert.Equal(1, cell.GetProperty("schemaVersion").GetInt32());
        Assert.False(cell.GetProperty("externalVerified").GetBoolean());
        Assert.Equal("Windows 11 25H2 x64", cell.GetProperty("cell").GetString());
        Assert.Equal("passed-cell-1-of-3", cell.GetProperty("matrixStatus").GetString());

        var result = cell.GetProperty("result");
        Assert.Equal("passed", result.GetProperty("status").GetString());
        Assert.Equal(19, result.GetProperty("passed").GetInt32());
        Assert.Equal(0, result.GetProperty("failed").GetInt32());
        Assert.Equal(0, result.GetProperty("skipped").GetInt32());
        Assert.Equal(0, result.GetProperty("processExitCode").GetInt32());
        Assert.Equal("real App.UI->Service IPC", result.GetProperty("flow").GetString());
        Assert.Equal("passed", result.GetProperty("w3cSessionDelete").GetString());
        Assert.False(result.GetProperty("externalVerified").GetBoolean());

        var cleanup = cell.GetProperty("cleanup");
        Assert.Equal(0, cleanup.GetProperty("appUiProcessCount").GetInt32());
        Assert.Equal("free", cleanup.GetProperty("port4725").GetString());
        Assert.Equal("untouched", cleanup.GetProperty("appium4731").GetString());
        Assert.Equal("cleaned", cleanup.GetProperty("ownedRuntimeState").GetString());
        AssertExcludedClaims(cell, "backend", "JWT", "RLS", "TLS endpoint", "WNS fan-out");
        Assert.Equal(
            new[] { "Windows 10 22H2 x64", "Windows 11 24H2 x64" },
            cell.GetProperty("remainingMatrix").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    private static void AssertExcludedClaims(JsonElement receipt, params string[] expectedClaims)
    {
        var claims = receipt.EnumerateObject()
            .Where(property => property.Name is "claimsExcluded" or "pause" or "result")
            .SelectMany(property => property.Name == "claimsExcluded"
                ? property.Value.EnumerateArray()
                : property.Value.TryGetProperty("claimsExcluded", out var nested) ? nested.EnumerateArray() : [])
            .Select(value => value.GetString())
            .Where(value => value is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var claim in expectedClaims)
        {
            Assert.Contains(claim, claims);
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null && !Directory.Exists(Path.Combine(current, "openspec"))) current = Directory.GetParent(current)?.FullName;
        return current ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
