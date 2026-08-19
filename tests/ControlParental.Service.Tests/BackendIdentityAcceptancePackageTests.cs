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

    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null && !Directory.Exists(Path.Combine(current, "openspec"))) current = Directory.GetParent(current)?.FullName;
        return current ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
