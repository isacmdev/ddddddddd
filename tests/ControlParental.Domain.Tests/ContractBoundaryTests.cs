namespace ControlParental.Domain.Tests;

using System.Text;
using System.Text.Json;
using System.Collections;
using System.Collections.Generic;
using ControlParental.Domain.WireContracts;
using ControlParental.Domain.WireContracts.Models;
using Xunit;

public sealed class ContractBoundaryTests
{
    [Fact]
    public void EncodeEnvelope_OmitsNullOptionalMembers()
    {
        var wire = new CreateTimeRequestWire(
            Guid.Parse("00000000-0000-4000-8000-000000000011"),
            "today", 1, "overlay", 1,
            Guid.Parse("00000000-0000-4000-8000-000000000012"),
            new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero),
            null);

        var json = Encoding.UTF8.GetString(WireContractCodec.EncodeEnvelope(
            wire,
            Guid.Parse("00000000-0000-4000-8000-000000000010"),
            WireContractCatalog.CreateTimeRequest));

        Assert.DoesNotContain("\"extensions\":null", json);
    }

    [Fact]
    public void DecodeAndValidate_AcceptsLowercaseNonzeroUuidv7()
    {
        var json = Encoding.UTF8.GetBytes("{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"realtime.hint\",\"correlation_id\":\"00000000-0000-7000-8000-000000000010\",\"payload\":{\"correlation_hint\":\"00000000-0000-7000-8000-000000000011\"}}");
        Assert.True(WireContractCodec.DecodeAndValidate(json, WireContractCatalog.RealtimeHint).IsValid);
    }

    [Fact]
    public void DecodeAndValidate_AcceptsCanonicalNonzeroUuidWithAnyVariant()
    {
        var json = Encoding.UTF8.GetBytes("{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"realtime.hint\",\"correlation_id\":\"00000000-0000-7000-0000-000000000010\",\"payload\":{\"correlation_hint\":\"00000000-0000-7000-0000-000000000011\"}}");
        Assert.True(WireContractCodec.DecodeAndValidate(json, WireContractCatalog.RealtimeHint).IsValid);
    }

    [Fact]
    public void EncodeValidatedEnvelope_ReturnsOnlyValidatedCreateTimeBytes()
    {
        var request = new CreateTimeRequestWire(
            Guid.Parse("00000000-0000-4000-8000-000000000011"), "today", 1, "overlay", 1,
            Guid.Parse("00000000-0000-4000-8000-000000000012"),
            new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero), null);

        var bytes = WireContractCodec.EncodeValidatedEnvelope(
            request, Guid.Parse("00000000-0000-4000-8000-000000000010"),
            WireContractCatalog.CreateTimeRequest, new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));

        Assert.True(WireContractCodec.DecodeAndValidate(bytes, WireContractCatalog.CreateTimeRequest, new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero)).IsValid);
    }

    [Fact]
    public void EncodeValidatedEnvelope_SerializesMutableExtensionsExactlyOnce()
    {
        var extensions = new EnumeratesOnceExtensions();
        var request = new CreateTimeRequestWire(
            Guid.Parse("00000000-0000-4000-8000-000000000011"), "today", 1, "overlay", 1,
            Guid.Parse("00000000-0000-4000-8000-000000000012"),
            new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero), null, extensions);

        var bytes = WireContractCodec.EncodeValidatedEnvelope(
            request, Guid.Parse("00000000-0000-4000-8000-000000000010"),
            WireContractCatalog.CreateTimeRequest, new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(1, extensions.EnumerationCount);
        Assert.True(WireContractCodec.DecodeAndValidate(bytes, WireContractCatalog.CreateTimeRequest, new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero)).IsValid);
    }

    private sealed class EnumeratesOnceExtensions : IReadOnlyDictionary<string, JsonElement>
    {
        private readonly JsonElement _safe = JsonDocument.Parse("\"safe\"").RootElement.Clone();
        private readonly JsonElement _authority = JsonDocument.Parse("\"x-authority\"").RootElement.Clone();
        public int EnumerationCount { get; private set; }
        public JsonElement this[string key] => EnumerationCount <= 1 ? _safe : _authority;
        public IEnumerable<string> Keys => new[] { "x-safe" };
        public IEnumerable<JsonElement> Values => new[] { this["x-safe"] };
        public int Count => 1;
        public bool ContainsKey(string key) => key == "x-safe";
        public bool TryGetValue(string key, out JsonElement value) { value = this[key]; return ContainsKey(key); }
        public IEnumerator<KeyValuePair<string, JsonElement>> GetEnumerator()
        {
            EnumerationCount++;
            yield return new("x-safe", this["x-safe"]);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    [Fact]
    public void CanonicalJson_FractionalAndIntegralEquivalentNumbersMatchNormativeForm()
    {
        using var document = JsonDocument.Parse("{\"m\":-0.001,\"n\":0.001,\"i\":1000.000}");
        Assert.Equal("{\"i\":1000,\"m\":-0.001,\"n\":0.001}", CanonicalJson.Serialize(document.RootElement));
    }

    [Fact]
    public void DecodeAndValidate_ExtensionAuthorityInName_IsRejected()
    {
        var json = Encoding.UTF8.GetBytes("{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"set_protected_account.response\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{\"operation_id\":\"00000000-0000-4000-8000-000000000011\",\"status\":\"accepted\",\"activation_state\":\"active\",\"correlation_id\":\"00000000-0000-4000-8000-000000000012\",\"reason_code\":\"ok\",\"extensions\":{\"x-authority\":\"safe\"}}}");
        Assert.False(WireContractCodec.DecodeAndValidate(json, WireContractCatalog.SetProtectedAccountResponse).IsValid);
    }

    [Theory]
    [InlineData("api-key")]
    [InlineData("private.key")]
    public void DecodeAndValidate_EquivalentSecretBearingExtensionNames_AreRejected(string name)
    {
        var json = Encoding.UTF8.GetBytes($"{{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"set_protected_account.response\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{{\"operation_id\":\"00000000-0000-4000-8000-000000000011\",\"status\":\"accepted\",\"activation_state\":\"active\",\"correlation_id\":\"00000000-0000-4000-8000-000000000012\",\"reason_code\":\"ok\",\"extensions\":{{\"x-safe\":{{\"{name}\":\"opaque\"}}}}}}}}");
        var result = WireContractCodec.DecodeAndValidate(json, WireContractCatalog.SetProtectedAccountResponse);
        Assert.False(result.IsValid, "expected invalid_extensions");
        Assert.Contains(result.Diagnostics, x => x.Code == "invalid_extensions");
    }

    [Fact]
    public void DecodeAndValidate_ValidPolicyAndHintRemainAccepted()
    {
        var root = FindRepositoryRoot().FullName;
        var policy = File.ReadAllBytes(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json"));
        Assert.True(WireContractCodec.DecodeAndValidate(policy, WireContractCatalog.PolicySnapshot).IsValid);
        var hint = File.ReadAllBytes(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-wns-hint.json"));
        Assert.True(WireContractCodec.DecodeAndValidate(hint, WireContractCatalog.WnsHint).IsValid);
    }

    [Fact]
    public void DecodeAndValidate_UnknownContract_IsRejectedBeforeMaterialization()
    {
        var json = Encoding.UTF8.GetBytes("{\"contract\":\"other\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{}}");
        var result = WireContractCodec.DecodeAndValidate(json, WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "unsupported_contract");
    }

    [Fact]
    public void DecodeAndValidate_NonStringEnvelopeMember_IsRejectedWithoutThrowing()
    {
        var json = Encoding.UTF8.GetBytes("{\"contract\":true,\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{}}");
        var result = WireContractCodec.DecodeAndValidate(json, WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "unsupported_contract");
    }

    [Fact]
    public void DecodeAndValidate_ValidTimeRequest_ReturnsTypedPayload()
    {
        var json = Encoding.UTF8.GetBytes("{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{\"request_id\":\"00000000-0000-4000-8000-000000000011\",\"scope\":\"today\",\"minutes\":180,\"origin\":\"overlay\",\"policy_version\":7,\"device_id\":\"00000000-0000-4000-8000-000000000012\",\"created_at\":\"2026-08-28T12:00:00Z\"}}");
        var result = WireContractCodec.DecodeAndValidate(json, WireContractCatalog.CreateTimeRequest);
        Assert.True(result.IsValid);
        Assert.Equal(180, Assert.IsType<CreateTimeRequestWire>(result.Value).Minutes);
    }

    [Fact]
    public void DecodeAndValidate_UnknownMember_ReturnsSingleDiagnosticBeforeEffects()
    {
        var json = Encoding.UTF8.GetBytes("{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{\"request_id\":\"00000000-0000-4000-8000-000000000011\",\"scope\":\"today\",\"minutes\":180,\"origin\":\"overlay\",\"policy_version\":7,\"device_id\":\"00000000-0000-4000-8000-000000000012\",\"created_at\":\"2026-08-28T12:00:00Z\",\"extra\":true}}");
        var result = WireContractCodec.DecodeAndValidate(json, WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
        Assert.Single(result.Diagnostics);
        Assert.Equal("unknown_member", result.Diagnostics[0].Code);
    }

    [Fact]
    public void DecodeAndValidate_FractionalTimestamp_IsRejected()
    {
        var json = Encoding.UTF8.GetBytes("{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{\"request_id\":\"00000000-0000-4000-8000-000000000011\",\"scope\":\"today\",\"minutes\":1,\"origin\":\"overlay\",\"policy_version\":7,\"device_id\":\"00000000-0000-4000-8000-000000000012\",\"created_at\":\"2026-08-28T12:00:00.1Z\"}}");
        var result = WireContractCodec.DecodeAndValidate(json, WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "invalid_timestamp");
    }

    [Fact]
    public void DecodeAndValidate_Int64Overflow_IsRejected()
    {
        var json = Encoding.UTF8.GetBytes("{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{\"request_id\":\"00000000-0000-4000-8000-000000000011\",\"scope\":\"today\",\"minutes\":1,\"origin\":\"overlay\",\"policy_version\":9223372036854775808,\"device_id\":\"00000000-0000-4000-8000-000000000012\",\"created_at\":\"2026-08-28T12:00:00Z\"}}");
        var result = WireContractCodec.DecodeAndValidate(json, WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "invalid_int64");
    }

    [Fact]
    public void DecodeAndValidate_NonCanonicalCorrelationUuid_IsRejected()
    {
        var json = Encoding.UTF8.GetBytes("{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{\"request_id\":\"00000000-0000-4000-8000-00000000001A\",\"scope\":\"today\"}}");
        var result = WireContractCodec.DecodeAndValidate(json, WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "invalid_uuid");
    }

    [Fact]
    public void DecodeAndValidate_OversizedOrdinaryString_IsRejected()
    {
        var contract = new string('a', 4097);
        var json = Encoding.UTF8.GetBytes($"{{\"contract\":\"{contract}\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{{}}}}");

        var result = WireContractCodec.DecodeAndValidate(json, WireContractCatalog.CreateTimeRequest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "string_oversize");
    }

    [Fact]
    public void DecodeAndValidate_OversizedArray_IsRejected()
    {
        var schedules = string.Join(',', Enumerable.Repeat("{}", 257));
        var json = Encoding.UTF8.GetBytes($"{{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"policy.snapshot\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{{\"device_id\":\"00000000-0000-4000-8000-000000000012\",\"version\":1,\"device_state\":\"active\",\"daily_screen_time_minutes\":120,\"schedules\":[{schedules}],\"category_limits\":[],\"app_policies\":[],\"category_assignments\":{{}},\"grants\":[],\"snapshot_hash\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}}}}");

        var result = WireContractCodec.DecodeAndValidate(json, WireContractCatalog.PolicySnapshot);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "array_oversize");
    }

    [Fact]
    public void DecodeAndValidate_HintBoundaryUsesCompleteEnvelopeBytes()
    {
        var valid = File.ReadAllBytes(Path.Combine(FindRepositoryRoot().FullName, "openspec/changes/shared-contracts-freeze/fixtures/valid-hint-1024.json"));
        var invalid = File.ReadAllBytes(Path.Combine(FindRepositoryRoot().FullName, "openspec/changes/shared-contracts-freeze/fixtures/invalid-hint-1025.json"));
        var validResult = WireContractCodec.DecodeAndValidate(valid, WireContractCatalog.WnsHint);
        Assert.True(validResult.IsValid, string.Join(",", validResult.Diagnostics.Select(x => x.Code + x.Path)));
        Assert.False(WireContractCodec.DecodeAndValidate(invalid, WireContractCatalog.WnsHint).IsValid);
    }

    [Fact]
    public void DecodeAndValidate_NestedOutboxRequestIsValidated()
    {
        var json = File.ReadAllText(Path.Combine(FindRepositoryRoot().FullName, "openspec/changes/shared-contracts-freeze/fixtures/valid-time-request-outbox.json"), Encoding.UTF8)
            .Replace("\"origin\":\"overlay\"", "\"origin\":\"invalid\"", StringComparison.Ordinal);
        var result = WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(json), WireContractCatalog.TimeRequestOutbox);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Path.Contains("wire_request", StringComparison.Ordinal));
    }

    [Fact]
    public void Catalog_ContainsEveryFrozenMessageFamily()
    {
        Assert.Equal(10, WireContractCatalog.All.Count);
        Assert.Contains(WireContractCatalog.All, x => x is ContractDescriptor<PolicySnapshotWire>);
        Assert.Contains(WireContractCatalog.All, x => x is ContractDescriptor<IntegrityVerdictWire>);
    }

    [Fact]
    public void ValidatePolicySnapshotHash_UsesCanonicalPayloadAndRejectsMismatch()
    {
        using var document = JsonDocument.Parse("{\"z\":1.0,\"a\":2,\"snapshot_hash\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}");
        Assert.False(WireContractCodec.ValidatePolicySnapshotHash(document.RootElement));
    }

    [Fact]
    public void DecodeAndValidate_RejectsAuthorityExtensionNameAndUppercaseHint()
    {
        var root = FindRepositoryRoot().FullName;
        var evidence = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-integrity-evidence.json"), Encoding.UTF8)
            .Replace("\"evidence_schema_version\":1}", "\"evidence_schema_version\":1,\"extensions\":{\"x-authority\":\"safe\"}}", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(evidence), WireContractCatalog.IntegrityEvidence).IsValid);

        var hint = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-realtime-hint.json"), Encoding.UTF8)
            .Replace("00000000-0000-4000-8000-000000000021", "ABCDEFAB-CDEF-4ABC-8DEF-ABCDEFABCDEF", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(hint), WireContractCatalog.RealtimeHint).IsValid);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ControlParental.sln"))) return directory;
        }

        throw new DirectoryNotFoundException("Repository root not found");
    }

    [Fact]
    public void CanonicalJson_SortsMembersAndNormalizesEquivalentNumbers()
    {
        using var document = System.Text.Json.JsonDocument.Parse("{\"z\":1.0,\"é\":\"a\\n\",\"a\":[2,1e0]}");
        Assert.Equal("{\"a\":[2,1],\"z\":1,\"é\":\"a\\n\"}", CanonicalJson.Serialize(document.RootElement));
    }

    [Fact]
    public void DecodeAndValidate_RejectsResidualSemanticAuthorityValues()
    {
        var root = FindRepositoryRoot().FullName;
        var evidence = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-integrity-evidence.json"), Encoding.UTF8)
            .Replace("\"evidence_schema_version\":1", "\"evidence_schema_version\":0", StringComparison.Ordinal)
            .Replace("\"agent_version\":\"1.0.0\"", "\"agent_version\":\"\"", StringComparison.Ordinal)
            .Replace("\"signer_summary\":\"publisher-redacted\"", "\"signer_summary\":\"\"", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(evidence), WireContractCatalog.IntegrityEvidence).IsValid);

        var account = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-set-protected-account.json"), Encoding.UTF8)
            .Replace("\"requested_at\":\"2026-08-28T12:00:00Z\"", "\"requested_at\":\"not-a-timestamp\"", StringComparison.Ordinal)
            .Replace("\"extensions\":{\"x-example\":{\"display_hint\":\"safe\"}}", "\"extensions\":{\"x-authority\":\"secret\"}}", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(account), WireContractCatalog.SetProtectedAccountRequest).IsValid);

        var policy = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json"), Encoding.UTF8)
            .Replace("\"days\":[\"MON\",\"TUE\",\"WED\",\"THU\",\"SUN\"]", "\"days\":[\"FUNDAY\"]", StringComparison.Ordinal)
            .Replace("\"snapshot_hash\":\"", "\"snapshot_hash\":\"aaaaaaaa", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(policy), WireContractCatalog.PolicySnapshot).IsValid);
    }

    [Fact]
    public void CanonicalJson_UsesFrozenExtremeNumbersAndSupplementaryKeyOrder()
    {
        using var document = JsonDocument.Parse("{\"😀\":1e-30,\"a\":1.0000000000000001,\"z\":1e30,\"array\":[true,null,2.50]}");
        Assert.Equal("{\"a\":1.0000000000000001,\"array\":[true,null,2.5],\"z\":1000000000000000000000000000000,\"\\uD83D\\uDE00\":0.000000000000000000000000000001}", CanonicalJson.Serialize(document.RootElement));
    }

    [Fact]
    public void DecodeAndValidate_RejectsAccountIntegrityAndPolicySemanticValues()
    {
        var account = File.ReadAllText(Path.Combine(FindRepositoryRoot().FullName, "openspec/changes/shared-contracts-freeze/fixtures/valid-set-protected-account.json"), Encoding.UTF8)
            .Replace("child01", "", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(account), WireContractCatalog.SetProtectedAccountRequest).IsValid);

        var evidence = File.ReadAllText(Path.Combine(FindRepositoryRoot().FullName, "openspec/changes/shared-contracts-freeze/fixtures/valid-integrity-evidence.json"), Encoding.UTF8)
            .Replace("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "bad", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(evidence), WireContractCatalog.IntegrityEvidence).IsValid);

        var policy = File.ReadAllText(Path.Combine(FindRepositoryRoot().FullName, "openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json"), Encoding.UTF8)
            .Replace("\"state\":\"blocked\"", "\"state\":\"permit\"", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(policy), WireContractCatalog.PolicySnapshot).IsValid);
    }

    [Fact]
    public void DecodeAndValidate_RejectsPayloadLargerThan49152Bytes()
    {
        var json = "{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{\"request_id\":\"00000000-0000-4000-8000-000000000011\",\"scope\":\"today\",\"minutes\":1,\"origin\":\"overlay\",\"policy_version\":7,\"device_id\":\"00000000-0000-4000-8000-000000000012\",\"created_at\":\"2026-08-28T12:00:00Z\",\"reason\":\"" + new string('a', 50000) + "\"}}";
        var result = WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(json), WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "payload_oversize");
    }

    [Fact]
    public void DecodeAndValidate_MalformedTimeRequestScopeNull_IsRejectedWithoutThrowing()
    {
        var json = "{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{\"request_id\":\"00000000-0000-4000-8000-000000000011\",\"scope\":null,\"minutes\":1,\"origin\":\"overlay\",\"policy_version\":7,\"device_id\":\"00000000-0000-4000-8000-000000000012\",\"created_at\":\"2026-08-28T12:00:00Z\"}}";
        var result = WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(json), WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "bounds" && x.Path.EndsWith("/scope", StringComparison.Ordinal));
    }

    [Fact]
    public void DecodeAndValidate_AccountTimestampAndIntegrityTextUseFrozenLimits()
    {
        var root = FindRepositoryRoot().FullName;
        var account = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-set-protected-account.json"), Encoding.UTF8)
            .Replace("2026-08-28T12:00:00Z", "2026-08-27T11:59:59Z", StringComparison.Ordinal);
        var accountResult = WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(account), WireContractCatalog.SetProtectedAccountRequest);
        Assert.False(accountResult.IsValid, string.Join(",", accountResult.Diagnostics.Select(x => x.Code + x.Path)));

        var evidence = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-integrity-evidence.json"), Encoding.UTF8)
            .Replace("1.0.0", "é", StringComparison.Ordinal);
        var evidenceResult = WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(evidence), WireContractCatalog.IntegrityEvidence);
        Assert.False(evidenceResult.IsValid, string.Join(",", evidenceResult.Diagnostics.Select(x => x.Code + x.Path)));
    }

    [Fact]
    public void DecodeAndValidate_RejectsStaleCollectedAt()
    {
        var root = FindRepositoryRoot().FullName;
        var evidence = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-integrity-evidence.json"), Encoding.UTF8)
            .Replace("2026-08-28T12:00:00Z", "2026-08-27T11:59:59Z", StringComparison.Ordinal);
        var result = WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(evidence), WireContractCatalog.IntegrityEvidence);
        Assert.False(result.IsValid, string.Join(",", result.Diagnostics.Select(x => x.Code + x.Path)));
        Assert.Contains(result.Diagnostics, x => x.Path.EndsWith("/collected_at", StringComparison.Ordinal));
    }

    [Fact]
    public void CanonicalJson_MatchesSharedGoldenVectors()
    {
        var path = Path.Combine(FindRepositoryRoot().FullName, "tests", "contract-freeze", "canonical-golden.json");
        using var vectors = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            using var input = JsonDocument.Parse(vector.GetProperty("input").GetString()!);
            var expected = Encoding.UTF8.GetBytes(vector.GetProperty("canonical").GetString()!);
            var actual = CanonicalJson.Utf8(input.RootElement);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void DecodeAndValidate_RejectsDuplicateMembersAtAnyDepth()
    {
        var json = "{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{\"request_id\":\"00000000-0000-4000-8000-000000000011\",\"scope\":\"today\",\"scope\":\"tomorrow\",\"minutes\":1,\"origin\":\"overlay\",\"policy_version\":7,\"device_id\":\"00000000-0000-4000-8000-000000000012\",\"created_at\":\"2026-08-28T12:00:00Z\"}}";
        var result = WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(json), WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "duplicate_member" && x.Path == "/payload/scope");
    }

    [Fact]
    public void CanonicalJson_PreservesSmallFractionsAndRejectsExponentOverflow()
    {
        using var document = JsonDocument.Parse("{\"m\":-0.001,\"n\":0.001}");
        Assert.Equal("{\"m\":-0.001,\"n\":0.001}", CanonicalJson.Serialize(document.RootElement));
        using var extreme = JsonDocument.Parse("{\"z\":1e2147483648}");
        Assert.Throws<JsonException>(() => CanonicalJson.Serialize(extreme.RootElement));
        using var compensated = JsonDocument.Parse("{\"z\":1.2300e1000000}");
        Assert.Equal(1_000_007, CanonicalJson.Utf8(compensated.RootElement).Length);
        using var compensatedSmall = JsonDocument.Parse("{\"z\":1.2300e-1000000}");
        Assert.Equal(1_000_010, CanonicalJson.Utf8(compensatedSmall.RootElement).Length);
    }

    [Theory]
    [InlineData("{\"z\":\"\\uD800\"}")]
    [InlineData("{\"z\":\"\\uDC00\"}")]
    public void CanonicalJson_RejectsUnpairedSurrogatesAsJsonErrors(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        Assert.Throws<JsonException>(() => CanonicalJson.Serialize(document.RootElement));
    }

    [Fact]
    public void CanonicalJson_RejectsSharedInvalidVectors()
    {
        var path = Path.Combine(FindRepositoryRoot().FullName, "tests", "contract-freeze", "canonical-invalid.json");
        using var vectors = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            if (vector.TryGetProperty("input_hex", out var hex))
            {
                var rawBytes = Convert.FromHexString(hex.GetString()!.Replace(" ", "", StringComparison.Ordinal));
                Assert.Throws<JsonException>(() => CanonicalJson.Utf8(rawBytes));
                continue;
            }

            var raw = Encoding.UTF8.GetBytes(vector.GetProperty("input").GetString()!);
            var exception = Record.Exception(() =>
            {
                using var input = JsonDocument.Parse(raw);
                _ = CanonicalJson.Serialize(input.RootElement);
            });
            Assert.True(exception is JsonException, vector.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void DecodePayloadAndValidate_RejectsDuplicateMembersBeforeTypedMaterialization()
    {
        var payload = Encoding.UTF8.GetBytes("{\"request_id\":\"00000000-0000-4000-8000-000000000011\",\"scope\":\"today\",\"scope\":\"other\",\"minutes\":1,\"origin\":\"overlay\",\"policy_version\":7,\"device_id\":\"00000000-0000-4000-8000-000000000012\",\"created_at\":\"2026-08-28T12:00:00Z\"}");
        var result = WireContractCodec.DecodePayloadAndValidate(payload, Guid.Parse("00000000-0000-4000-8000-000000000010"), WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "duplicate_member" && x.Path == "/payload/scope");
    }

    [Fact]
    public void DecodePayloadAndValidate_RejectsUnpairedSurrogateWithoutThrowing()
    {
        var payload = Encoding.UTF8.GetBytes("{\"request_id\":\"00000000-0000-4000-8000-000000000011\",\"scope\":\"\\uD800\",\"minutes\":1,\"origin\":\"overlay\",\"policy_version\":7,\"device_id\":\"00000000-0000-4000-8000-000000000012\",\"created_at\":\"2026-08-28T12:00:00Z\"}");
        var result = WireContractCodec.DecodePayloadAndValidate(payload, Guid.Parse("00000000-0000-4000-8000-000000000010"), WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("{\"\\uD800\":1}")]
    [InlineData("{\"\\uDC00\":1}")]
    public void DecodeAndValidate_RejectsUnpairedSurrogateInMemberNameWithoutThrowing(string payload)
    {
        var envelope = "{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":" + payload + "}";
        var result = WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(envelope), WireContractCatalog.CreateTimeRequest);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePolicySnapshotHash_RejectsUnpairedSurrogateWithoutThrowing()
    {
        using var document = JsonDocument.Parse("{\"z\":\"\\uD800\",\"snapshot_hash\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}");
        Assert.False(WireContractCodec.ValidatePolicySnapshotHash(document.RootElement));
    }

    [Fact]
    public void CanonicalJson_MatchesCompensatedBoundaryGoldenBytesExactly()
    {
        var path = Path.Combine(FindRepositoryRoot().FullName, "tests", "contract-freeze", "canonical-golden.json");
        using var vectors = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        foreach (var vector in vectors.RootElement.EnumerateArray().Where(x => x.GetProperty("name").GetString() is "compensated_min" or "compensated_max"))
        {
            using var input = JsonDocument.Parse(vector.GetProperty("input").GetString()!);
            Assert.Equal(Encoding.UTF8.GetBytes(vector.GetProperty("canonical").GetString()!), CanonicalJson.Utf8(input.RootElement));
        }
    }

    [Fact]
    public void DecodeAndValidate_RejectsDuplicateScheduleDaysWithMatchingSnapshotHash()
    {
        var root = FindRepositoryRoot().FullName;
        var json = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json"), Encoding.UTF8)
            .Replace("[\"MON\",\"TUE\",\"WED\",\"THU\",\"SUN\"]", "[\"MON\",\"MON\",\"WED\",\"THU\",\"SUN\"]", StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement.GetProperty("payload");
        var payloadWithoutHash = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload.GetRawText())!;
        payloadWithoutHash.Remove("snapshot_hash");
        using var canonicalPayload = JsonDocument.Parse(JsonSerializer.Serialize(payloadWithoutHash));
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(CanonicalJson.Utf8(canonicalPayload.RootElement))).ToLowerInvariant();
        json = json.Replace(document.RootElement.GetProperty("payload").GetProperty("snapshot_hash").GetString()!, hash, StringComparison.Ordinal);

        var result = WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(json), WireContractCatalog.PolicySnapshot);

        Assert.False(result.IsValid, string.Join(",", result.Diagnostics.Select(x => x.Code + x.Path)));
        Assert.DoesNotContain(result.Diagnostics, x => x.Code is "snapshot_hash" or "invalid_hash");
        Assert.Contains(result.Diagnostics, x => x.Path.Contains("schedules", StringComparison.Ordinal));
    }

    [Fact]
    public void DecodeAndValidate_RejectsPolicyNestedBoundaryShapes()
    {
        var root = FindRepositoryRoot().FullName;
        var policy = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json"), Encoding.UTF8)
            .Replace("[\"MON\",\"TUE\",\"WED\",\"THU\",\"SUN\"]", "[\"MON\",\"MON\"]", StringComparison.Ordinal)
            .Replace("\"minutes\":120", "\"minutes\":1441", StringComparison.Ordinal)
            .Replace("\"from\":\"08:00\"", "\"from\":\"99:99\"", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(policy), WireContractCatalog.PolicySnapshot).IsValid);
    }

    [Fact]
    public void DecodeAndValidate_RejectsNullExtensionsAndEmptyPolicyDayLists()
    {
        var root = FindRepositoryRoot().FullName;
        var account = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-set-protected-account.json"), Encoding.UTF8)
            .Replace("\"extensions\":{\"x-example\":{\"display_hint\":\"safe\"}}", "\"extensions\":null", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(account), WireContractCatalog.SetProtectedAccountRequest).IsValid);

        var policy = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json"), Encoding.UTF8)
            .Replace("[\"MON\",\"TUE\",\"WED\",\"THU\",\"SUN\"]", "[]", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(policy), WireContractCatalog.PolicySnapshot).IsValid);
    }

    [Fact]
    public void DecodeAndValidate_RejectsNonAsciiCategoryNames()
    {
        var root = FindRepositoryRoot().FullName;
        var policy = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-policy-snapshot.json"), Encoding.UTF8)
            .Replace("\"category\":\"games\"", "\"category\":\"juegos-é\"", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(policy), WireContractCatalog.PolicySnapshot).IsValid);
    }

    [Fact]
    public void DecodeAndValidate_AcceptsSafePayloadExtensions()
    {
        var root = FindRepositoryRoot().FullName;
        var evidence = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-integrity-evidence.json"), Encoding.UTF8)
            .Replace("\"extensions\":{\"x-example\":{\"display_hint\":\"safe\"}}", "\"extensions\":{\"x-safe\":{\"display_hint\":\"safe\"}}", StringComparison.Ordinal);
        var result = WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(evidence), WireContractCatalog.IntegrityEvidence);
        Assert.True(result.IsValid, string.Join(",", result.Diagnostics.Select(x => x.Code + x.Path)));
    }

    [Fact]
    public void DecodeAndValidate_OutboxNonStringCorrelationIsRejectedWithoutThrowing()
    {
        var root = FindRepositoryRoot().FullName;
        var json = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-time-request-outbox.json"), Encoding.UTF8)
            .Replace("\"correlation_id\":\"00000000-0000-4000-8000-000000000013\"", "\"correlation_id\":7", StringComparison.Ordinal);
        var exception = Record.Exception(() => WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(json), WireContractCatalog.TimeRequestOutbox));
        Assert.Null(exception);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(json), WireContractCatalog.TimeRequestOutbox).IsValid);
    }

    [Fact]
    public void DecodeAndValidate_UsesReceiptClockForFreshness()
    {
        var json = File.ReadAllBytes(Path.Combine(FindRepositoryRoot().FullName, "openspec/changes/shared-contracts-freeze/fixtures/valid-set-protected-account.json"));
        var result = WireContractCodec.DecodeAndValidate(json, WireContractCatalog.SetProtectedAccountRequest,
            new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero));
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "age");
    }

    [Fact]
    public void DecodeAndValidate_RejectsCanonicalPayloadExpansion()
    {
        var json = "{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"create_time_request\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{\"request_id\":\"00000000-0000-4000-8000-000000000011\",\"scope\":\"today\",\"minutes\":1,\"origin\":\"overlay\",\"policy_version\":1e50000,\"device_id\":\"00000000-0000-4000-8000-000000000012\",\"created_at\":\"2026-08-28T12:00:00Z\"}}";
        var exception = Record.Exception(() => WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(json), WireContractCatalog.CreateTimeRequest));
        Assert.Null(exception);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(json), WireContractCatalog.CreateTimeRequest).IsValid);
    }

    [Fact]
    public void DecodeAndValidate_RejectsInvalidPresentHintAndAllowsSafeResponseExtensions()
    {
        var root = FindRepositoryRoot().FullName;
        var hint = File.ReadAllText(Path.Combine(root, "openspec/changes/shared-contracts-freeze/fixtures/valid-realtime-hint.json"), Encoding.UTF8)
            .Replace("00000000-0000-4000-8000-000000000021", "ABCDEFAB-CDEF-4ABC-8DEF-ABCDEFABCDEF", StringComparison.Ordinal);
        Assert.False(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(hint), WireContractCatalog.RealtimeHint).IsValid);

        var response = "{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"set_protected_account.response\",\"correlation_id\":\"00000000-0000-4000-8000-000000000010\",\"payload\":{\"operation_id\":\"00000000-0000-4000-8000-000000000011\",\"status\":\"accepted\",\"activation_state\":\"active\",\"correlation_id\":\"00000000-0000-4000-8000-000000000012\",\"reason_code\":\"ok\",\"extensions\":{\"x-safe\":\"display\"}}}";
        Assert.True(WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(response), WireContractCatalog.SetProtectedAccountResponse).IsValid);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("passwd")]
    [InlineData("api_key")]
    public void DecodeAndValidate_RejectsRecursiveSecretBearingExtensions(string member)
    {
        var json = $"{{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"realtime.hint\",\"correlation_id\":\"00000000-0000-7000-0000-000000000010\",\"payload\":{{\"hint_type\":\"sync\",\"extensions\":{{\"x-safe\":[{{\"{member}\":\"secret\"}}]}}}}}}";
        var result = WireContractCodec.DecodeAndValidate(Encoding.UTF8.GetBytes(json), WireContractCatalog.RealtimeHint);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "invalid_extensions");
    }
}
