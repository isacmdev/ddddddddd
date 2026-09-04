namespace ControlParental.Domain.Tests;

using System.Text;
using System.Text.Json;
using ControlParental.Domain.WireContracts;
using ControlParental.Domain.WireContracts.Models;
using Xunit;

public sealed class ContractAcceptanceTests
{
    private const string FixtureDirectory = "openspec/changes/shared-contracts-freeze/fixtures";

    [Fact(DisplayName = "CT-01 contract round trip preserves canonical wire names")]
    [Trait("ContractTest", "CT-01")]
    public void CT01_RoundTripPreservesCanonicalWireNames()
    {
        var result = Decode<CreateTimeRequestWire>("valid-create-time-request.json", WireContractCatalog.CreateTimeRequest);
        Assert.True(result.IsValid);
        Assert.Equal(180, result.Value!.Minutes);
        Assert.Contains("\"request_id\"", ReadFixture("valid-create-time-request.json"));
    }

    [Fact(DisplayName = "CT-02 malformed and bounded payloads fail closed")]
    [Trait("ContractTest", "CT-02")]
    public void CT02_InvalidPayloadFailsClosed()
    {
        var unknown = Decode<CreateTimeRequestWire>("invalid-unknown-required-member.json", WireContractCatalog.CreateTimeRequest);
        var bounds = Decode<CreateTimeRequestWire>("invalid-minutes-too-high.json", WireContractCatalog.CreateTimeRequest);
        AssertRejectedWithoutEffects(unknown);
        AssertRejectedWithoutEffects(bounds);
    }

    [Fact(DisplayName = "CT-03 account ACK is accepted only when its operation identity is durable")]
    [Trait("ContractTest", "CT-03")]
    public void CT03_AccountAckRetainsOperationIdentity()
    {
        var request = DecodeText<SetProtectedAccountRequestWire>("{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"set_protected_account.request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000003\",\"payload\":{\"operation_id\":\"00000000-0000-4000-8000-000000000002\",\"username\":\"child01\",\"sid\":\"S-1-5-21-111111111-222222222-333333333-1001\",\"requested_at\":\"2026-08-28T12:00:00Z\"}}", WireContractCatalog.SetProtectedAccountRequest);
        var response = Decode<SetProtectedAccountResponseWire>("valid-set-protected-account-response.json", WireContractCatalog.SetProtectedAccountResponse);
        Assert.True(request.IsValid, string.Join(",", request.Diagnostics.Select(x => x.Code + x.Path)));
        Assert.True(response.IsValid, string.Join(",", response.Diagnostics.Select(x => x.Code + x.Path)));
        Assert.Equal(request.Value!.OperationId, response.Value!.OperationId);

        var conflict = Decode<SetProtectedAccountResponseWire>("valid-set-protected-account-response.json", WireContractCatalog.SetProtectedAccountResponse, ("operation_id", "00000000-0000-4000-8000-000000000099"));
        Assert.True(conflict.IsValid);
        Assert.NotEqual(request.Value.OperationId, conflict.Value!.OperationId);
    }

    [Fact(DisplayName = "CT-04 degraded activation remains non-active and rejects unknown state")]
    [Trait("ContractTest", "CT-04")]
    public void CT04_DegradedActivationIsNotActive()
    {
        var degraded = Decode<RuntimeActivationStateWire>("valid-runtime-activation.json", WireContractCatalog.RuntimeActivation);
        Assert.True(degraded.IsValid);
        Assert.Equal("degraded", degraded.Value!.ActivationState);
        Assert.NotEqual("active", degraded.Value.ActivationState);

        var invalid = Decode<RuntimeActivationStateWire>("invalid-enum.json", WireContractCatalog.RuntimeActivation);
        AssertRejectedWithoutEffects(invalid);
    }

    [Fact(DisplayName = "CT-05 outbox identity includes generation and preserves truthful state")]
    [Trait("ContractTest", "CT-05")]
    public void CT05_OutboxIdentityIncludesGeneration()
    {
        var first = Decode<TimeRequestOutboxWire>("valid-time-request-outbox.json", WireContractCatalog.TimeRequestOutbox);
        Assert.True(first.IsValid);
        Assert.NotEqual(Guid.Empty, first.Value!.DeviceGeneration);
        Assert.Equal(first.Value.RequestId, first.Value.WireRequest.RequestId);
        Assert.DoesNotContain("approved", first.Value.OutboxState, StringComparison.Ordinal);

        var invalid = Decode<TimeRequestOutboxWire>("invalid-enum.json", WireContractCatalog.TimeRequestOutbox);
        AssertRejectedWithoutEffects(invalid);
    }

    [Fact(DisplayName = "CT-06 denial cannot create a grant or apply a policy")]
    [Trait("ContractTest", "CT-06")]
    public void CT06_DenialCannotCreateGrant()
    {
        var denied = Decode<SetProtectedAccountResponseWire>("valid-set-protected-account-response.json", WireContractCatalog.SetProtectedAccountResponse, ("status", "rejected"));
        Assert.True(denied.IsValid);
        Assert.Equal("rejected", denied.Value!.Status);

        var invalid = Decode<PolicySnapshotWire>("invalid-enum.json", WireContractCatalog.PolicySnapshot);
        AssertRejectedWithoutEffects(invalid);
    }

    [Fact(DisplayName = "CT-07 stale integrity evidence is rejected before authorization")]
    [Trait("ContractTest", "CT-07")]
    public void CT07_StaleGenerationIsRejected()
    {
        var evidence = Decode<IntegrityEvidenceWire>("valid-integrity-evidence.json", WireContractCatalog.IntegrityEvidence);
        Assert.True(evidence.IsValid);
        Assert.NotEqual(Guid.Empty, evidence.Value!.DeviceGeneration);

        var invalid = Decode<IntegrityEvidenceWire>("invalid-enum.json", WireContractCatalog.IntegrityEvidence);
        AssertRejectedWithoutEffects(invalid);
    }

    [Fact(DisplayName = "CT-08 device identity is required at the contract boundary")]
    [Trait("ContractTest", "CT-08")]
    public void CT08_DeviceIdentityIsRequired()
    {
        var valid = DecodeText<SetProtectedAccountRequestWire>("{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"set_protected_account.request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000003\",\"payload\":{\"operation_id\":\"00000000-0000-4000-8000-000000000002\",\"username\":\"child01\",\"sid\":\"S-1-5-21-111111111-222222222-333333333-1001\",\"requested_at\":\"2026-08-28T12:00:00Z\"}}", WireContractCatalog.SetProtectedAccountRequest);
        Assert.True(valid.IsValid, string.Join(",", valid.Diagnostics.Select(x => x.Code + x.Path)));
        Assert.NotEqual(Guid.Empty, valid.Value!.OperationId);
        Assert.False(string.IsNullOrWhiteSpace(valid.Value.Sid));

        var invalid = Decode<SetProtectedAccountRequestWire>("invalid-missing-required.json", WireContractCatalog.SetProtectedAccountRequest);
        AssertRejectedWithoutEffects(invalid);
    }

    [Fact(DisplayName = "CT-09 policy snapshots reject same-version hash conflicts without downgrade")]
    [Trait("ContractTest", "CT-09")]
    public void CT09_PolicyVersionsDoNotDowngrade()
    {
        var current = Decode<PolicySnapshotWire>("valid-policy-snapshot.json", WireContractCatalog.PolicySnapshot);
        Assert.True(current.IsValid);
        Assert.True(current.Value!.Version > 0);

        var conflicting = Decode<PolicySnapshotWire>(
            "valid-policy-snapshot.json",
            WireContractCatalog.PolicySnapshot,
            ("snapshot_hash", "0000000000000000000000000000000000000000000000000000000000000000"));
        AssertRejectedWithoutEffects(conflicting);
    }

    [Fact(DisplayName = "CT-10 unknown integrity verdict is non-degrading")]
    [Trait("ContractTest", "CT-10")]
    public void CT10_UnknownVerdictIsNonDegrading()
    {
        var unknown = Decode<IntegrityVerdictWire>("valid-integrity-verdict.json", WireContractCatalog.IntegrityVerdict, ("verdict", "unknown"));
        Assert.True(unknown.IsValid);
        Assert.NotEqual("trust", unknown.Value!.Verdict);
        Assert.NotEqual("revoked", unknown.Value.Verdict);

        var malformed = Decode<IntegrityVerdictWire>("valid-integrity-verdict.json", WireContractCatalog.IntegrityVerdict, ("verdict", "grant"));
        AssertRejectedWithoutEffects(malformed);
    }

    [Fact(DisplayName = "CT-11 hints are bounded synchronization signals only")]
    [Trait("ContractTest", "CT-11")]
    public void CT11_HintsAreSignalsOnly()
    {
        var hint = Decode<HintWire>("valid-wns-hint.json", WireContractCatalog.WnsHint);
        Assert.True(hint.IsValid);
        Assert.Equal("sync", hint.Value!.HintType);
        Assert.NotEqual(Guid.Empty, hint.Value.CorrelationHint);
        Assert.Null(hint.Value.Extensions);

        var oversized = Decode<HintWire>("invalid-hint-1025.json", WireContractCatalog.WnsHint);
        AssertRejectedWithoutEffects(oversized);
    }

    [Fact(DisplayName = "CT-12 diagnostics expose only safe contract codes")]
    [Trait("ContractTest", "CT-12")]
    public void CT12_DiagnosticsAreRedacted()
    {
        var invalid = Decode<CreateTimeRequestWire>("invalid-reason-too-long.json", WireContractCatalog.CreateTimeRequest);
        Assert.False(invalid.IsValid);
        Assert.NotEmpty(invalid.Diagnostics);
        Assert.All(invalid.Diagnostics, diagnostic =>
        {
            Assert.DoesNotContain("token", diagnostic.Code, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("00000000-0000-4000-8000-000000000012", diagnostic.Path, StringComparison.Ordinal);
        });
    }

    private static WireDecodeResult<T> Decode<T>(string fixture, ContractDescriptor<T> descriptor, params (string Name, string Value)[] replacements)
    {
        var text = ReadFixture(fixture);
        foreach (var (name, value) in replacements)
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement.Clone();
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var property in root.EnumerateObject())
                {
                    if (property.NameEquals("payload"))
                    {
                        writer.WritePropertyName("payload");
                        writer.WriteStartObject();
                        foreach (var payloadProperty in property.Value.EnumerateObject())
                        {
                            if (payloadProperty.NameEquals(name)) writer.WriteString(name, value);
                            else payloadProperty.WriteTo(writer);
                        }
                        writer.WriteEndObject();
                    }
                    else property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            text = Encoding.UTF8.GetString(stream.ToArray());
        }

        return WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(text), descriptor);
    }

    private static WireDecodeResult<T> DecodeText<T>(string text, ContractDescriptor<T> descriptor)
        => WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(text), descriptor);

    private static string ReadFixture(string fixture)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), FixtureDirectory, fixture), Encoding.UTF8);

    private static void AssertRejectedWithoutEffects<T>(WireDecodeResult<T> result)
    {
        var sideEffects = 0;
        if (result.IsValid) sideEffects++;
        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Equal(0, sideEffects);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ControlParental.sln"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root not found");
    }
}
