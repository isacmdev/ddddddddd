namespace ControlParental.Domain.WireContracts;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlParental.Domain.WireContracts.Models;

public sealed record WireDiagnostic(string Code, string Path);
public sealed class WireDecodeResult<T>
{
    private WireDecodeResult(T? value, IReadOnlyList<WireDiagnostic> diagnostics) { Value = value; Diagnostics = diagnostics; }
    public T? Value { get; }
    public IReadOnlyList<WireDiagnostic> Diagnostics { get; }
    public bool IsValid => Diagnostics.Count == 0;
    public static WireDecodeResult<T> Valid(T value) => new(value, Array.Empty<WireDiagnostic>());
    public static WireDecodeResult<T> Invalid(IEnumerable<WireDiagnostic> errors) => new(default, errors.ToArray());
}

public sealed record ContractDescriptor<T>(string MessageType, IReadOnlySet<string> RequiredPayloadMembers,
    IReadOnlySet<string> AllowedPayloadMembers, Func<JsonElement, List<WireDiagnostic>, T?> Materialize);
public sealed record CreateTimeRequestWire(
    Guid RequestId, string Scope, int Minutes, string Origin, ulong PolicyVersion,
    Guid DeviceId, [property: JsonConverter(typeof(UtcSecondDateTimeOffsetConverter))] DateTimeOffset CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, JsonElement>? Extensions = null);

public sealed class UtcSecondDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTimeOffset.ParseExact(reader.GetString() ?? string.Empty, Format, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture));
}

public static class WireContractCatalog
{
    private static readonly IReadOnlySet<string> AccountRequest = Set("operation_id", "username", "sid", "requested_at", "extensions");
    private static readonly IReadOnlySet<string> AccountResponse = Set("operation_id", "status", "activation_state", "correlation_id", "reason_code", "extensions");
    private static readonly IReadOnlySet<string> Activation = Set("activation_state", "reason_code", "observed_at", "state_version", "extensions");
    private static readonly IReadOnlySet<string> Time = Set("request_id", "scope", "minutes", "origin", "policy_version", "device_id", "created_at", "reason", "extensions");
    private static readonly IReadOnlySet<string> Outbox = Set("request_id", "device_generation", "wire_request", "outbox_state", "attempt_count", "next_attempt_at", "extensions");
    private static readonly IReadOnlySet<string> Evidence = Set("evidence_id", "device_generation", "agent_version", "binary_sha256", "signature_result", "signer_summary", "collected_at", "evidence_schema_version", "extensions");
    private static readonly IReadOnlySet<string> Verdict = Set("verdict", "evidence_id", "evaluated_at", "verdict_version", "reason_code", "extensions");
    private static readonly IReadOnlySet<string> Hint = Set("hint_type", "correlation_hint", "extensions");
    private static readonly IReadOnlySet<string> Policy = Set("device_id", "version", "device_state", "daily_screen_time_minutes", "schedules", "category_limits", "app_policies", "category_assignments", "grants", "snapshot_hash", "extensions");
    private static IReadOnlySet<string> Set(params string[] values) => new HashSet<string>(values, StringComparer.Ordinal);
    private static T? Deserialize<T>(JsonElement p, List<WireDiagnostic> e)
    {
        try
        {
            return (T?)JsonSerializer.Deserialize(p.GetRawText(), typeof(T), WireContractJsonContext.Default);
        }
        catch (JsonException)
        {
            e.Add(new("invalid_value", "/payload"));
            return default;
        }
    }
    public static ContractDescriptor<T> Descriptor<T>(string type, IReadOnlySet<string> required, IReadOnlySet<string> allowed) => new(type, required, allowed, Deserialize<T>);
    public static ContractDescriptor<SetProtectedAccountRequestWire> SetProtectedAccountRequest { get; } = Descriptor<SetProtectedAccountRequestWire>("set_protected_account.request", Set("operation_id", "username", "sid", "requested_at"), AccountRequest);
    public static ContractDescriptor<SetProtectedAccountResponseWire> SetProtectedAccountResponse { get; } = Descriptor<SetProtectedAccountResponseWire>("set_protected_account.response", Set("operation_id", "status", "activation_state", "correlation_id", "reason_code"), AccountResponse);
    public static ContractDescriptor<RuntimeActivationStateWire> RuntimeActivation { get; } = Descriptor<RuntimeActivationStateWire>("runtime_activation.state", Set("activation_state", "reason_code", "observed_at", "state_version"), Activation);
    public static ContractDescriptor<CreateTimeRequestWire> CreateTimeRequest { get; } = new("create_time_request", Set("request_id", "scope", "minutes", "origin", "policy_version", "device_id", "created_at"), Time, MaterializeTime);
    public static ContractDescriptor<TimeRequestOutboxWire> TimeRequestOutbox { get; } = Descriptor<TimeRequestOutboxWire>("time_request.outbox", Set("request_id", "device_generation", "wire_request", "outbox_state", "attempt_count", "next_attempt_at"), Outbox);
    public static ContractDescriptor<PolicySnapshotWire> PolicySnapshot { get; } = Descriptor<PolicySnapshotWire>("policy.snapshot", Set("device_id", "version", "device_state", "daily_screen_time_minutes", "schedules", "category_limits", "app_policies", "category_assignments", "grants", "snapshot_hash"), Policy);
    public static ContractDescriptor<IntegrityEvidenceWire> IntegrityEvidence { get; } = Descriptor<IntegrityEvidenceWire>("integrity.evidence", Set("evidence_id", "device_generation", "agent_version", "binary_sha256", "signature_result", "signer_summary", "collected_at", "evidence_schema_version"), Evidence);
    public static ContractDescriptor<IntegrityVerdictWire> IntegrityVerdict { get; } = Descriptor<IntegrityVerdictWire>("integrity.verdict", Set("verdict", "evidence_id", "evaluated_at", "verdict_version", "reason_code"), Verdict);
    public static ContractDescriptor<HintWire> WnsHint { get; } = Descriptor<HintWire>("wns.hint", Set(), Hint);
    public static ContractDescriptor<HintWire> RealtimeHint { get; } = Descriptor<HintWire>("realtime.hint", Set(), Hint);
    public static IReadOnlyList<object> All => new object[] { SetProtectedAccountRequest, SetProtectedAccountResponse, RuntimeActivation, CreateTimeRequest, TimeRequestOutbox, PolicySnapshot, IntegrityEvidence, IntegrityVerdict, WnsHint, RealtimeHint };

    private static CreateTimeRequestWire? MaterializeTime(JsonElement p, List<WireDiagnostic> e)
    {
        var value = Deserialize<CreateTimeRequestWire>(p, e);
        if (value is null) return null;
        if (value.RequestId == Guid.Empty || value.DeviceId == Guid.Empty) e.Add(new("invalid_uuid", "/payload"));
        if (value.Minutes is < 1 or > 180) e.Add(new("bounds", "/payload/minutes"));
        if (value.Origin is not ("status_page" or "overlay")) e.Add(new("invalid_enum", "/payload/origin"));
        if (string.IsNullOrEmpty(value.Scope) || value.Scope.Length > 64 || value.Scope.Any(c => c > 0x7f)) e.Add(new("bounds", "/payload/scope"));
        if (value.Reason is not null && (value.Reason.Length == 0 || Encoding.UTF8.GetByteCount(value.Reason) > 256)) e.Add(new("bounds", "/payload/reason"));
        if (!ExactUtc(value.CreatedAt)) e.Add(new("invalid_timestamp", "/payload/created_at"));
        return e.Count == 0 ? value : null;
    }
    internal static bool ExactUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero && value.Ticks % TimeSpan.TicksPerSecond == 0;
}

public static class WireContractCodec
{
    private const int MaxEnvelopeBytes = 65_536, MaxPayloadBytes = 49_152;
    private static bool IsClockTime(JsonElement value) => value.ValueKind == JsonValueKind.String
        && System.Text.RegularExpressions.Regex.IsMatch(value.GetString() ?? string.Empty, "^(?:[01][0-9]|2[0-3]):[0-5][0-9]$");
    private static bool TryTimestamp(JsonElement p, string name, out DateTimeOffset value)
    {
        value = default;
        return p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParseExact(v.GetString(), "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    }
    private static bool AsciiLength(string? value, int min, int max)
        => !string.IsNullOrEmpty(value) && value.Length >= min && value.Length <= max && value.All(c => c <= 0x7f);

    /// <summary>Checks a policy hash over the canonical payload without the hash member.</summary>
    public static bool ValidatePolicySnapshotHash(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("snapshot_hash", out var supplied)
            || supplied.ValueKind != JsonValueKind.String)
            return false;

        string actual;
        try
        {
            actual = Convert.ToHexString(SHA256.HashData(CanonicalJson.Utf8ObjectWithoutProperty(payload, "snapshot_hash"))).ToLowerInvariant();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or OverflowException or ArgumentOutOfRangeException) { return false; }
        var expected = supplied.GetString() ?? string.Empty;
        return expected.Length == 64
            && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));
    }


    public static WireDecodeResult<T> DecodeAndValidate<T>(ReadOnlySpan<byte> utf8, ContractDescriptor<T> descriptor, DateTimeOffset? receivedAt = null)
    {
        var errors = new List<WireDiagnostic>();
        if (utf8.Length > MaxEnvelopeBytes) return WireDecodeResult<T>.Invalid(new[] { new WireDiagnostic("envelope_oversize", "") });
        if (utf8.Length >= 3 && utf8[..3].SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf })) errors.Add(new("bom", ""));
        JsonDocument? document = null;
        try { document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 16 }); }
        catch (JsonException) { return WireDecodeResult<T>.Invalid(new[] { new WireDiagnostic("invalid_json", "") }); }
        using (document)
        {
            if (!CanonicalJson.HasValidUtf16Escapes(utf8))
                return WireDecodeResult<T>.Invalid(new[] { new WireDiagnostic("invalid_unicode", "") });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return WireDecodeResult<T>.Invalid(new[] { new WireDiagnostic("invalid_envelope", "") });
            ValidateGlobalLimits(root, "", errors);
            ValidateDuplicateMembers(root, "", errors);
            var names = root.EnumerateObject().Select(x => x.Name).ToArray();
            foreach (var name in new[] { "contract", "version", "message_type", "correlation_id", "payload" }) if (!root.TryGetProperty(name, out _)) errors.Add(new("required", "/" + name));
            foreach (var name in names.Where(x => x is not ("contract" or "version" or "message_type" or "correlation_id" or "payload" or "extensions"))) errors.Add(new("unknown_member", "/" + name));
            if (!root.TryGetProperty("contract", out var c) || c.ValueKind != JsonValueKind.String || c.GetString() != "control-parental.windows") errors.Add(new("unsupported_contract", "/contract"));
            if (!root.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var version) || version != 1) errors.Add(new("unsupported_version", "/version"));
            if (!root.TryGetProperty("message_type", out var mt) || mt.ValueKind != JsonValueKind.String || mt.GetString() != descriptor.MessageType) errors.Add(new("unsupported_message_type", "/message_type"));
            if (!root.TryGetProperty("correlation_id", out var ci) || ci.ValueKind != JsonValueKind.String || !Guid.TryParseExact(ci.GetString(), "D", out var id) || id == Guid.Empty || ci.GetString() != id.ToString("D") || !System.Text.RegularExpressions.Regex.IsMatch(ci.GetString() ?? string.Empty, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")) errors.Add(new("invalid_uuid", "/correlation_id"));
            if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object) errors.Add(new("invalid_payload", "/payload"));
            else {
                try
                {
                            if (CanonicalJson.Utf8(payload).Length > MaxPayloadBytes) errors.Add(new("payload_oversize", "/payload"));
                }
                catch (JsonException) { errors.Add(new("invalid_json", "/payload")); }
                if (descriptor.MessageType is "wns.hint" or "realtime.hint"
                    && utf8.Length > 1024) errors.Add(new("hint_oversize", ""));
                var pn = payload.EnumerateObject().Select(x => x.Name).ToArray();
                foreach (var name in descriptor.RequiredPayloadMembers.Where(x => !payload.TryGetProperty(x, out _))) errors.Add(new("required", "/payload/" + name));
                foreach (var name in pn.Where(x => !descriptor.AllowedPayloadMembers.Contains(x))) errors.Add(new("unknown_member", "/payload/" + name));
                ValidateExtensions(root, "", errors);
                ValidateEnumsAndTimestamps(payload, descriptor.MessageType, errors, receivedAt ?? FrozenContractClock);
                if (descriptor.MessageType == "time_request.outbox"
                    && payload.TryGetProperty("wire_request", out var nested)
                    && root.TryGetProperty("correlation_id", out var nestedCorrelationElement)
                    && nestedCorrelationElement.ValueKind == JsonValueKind.String
                    && Guid.TryParseExact(nestedCorrelationElement.GetString(), "D", out var nestedCorrelationId)
                    && nestedCorrelationId != Guid.Empty)
                {
                    var nestedEnvelope = EncodeEnvelope(
                        Encoding.UTF8.GetBytes(nested.GetRawText()),
                        nestedCorrelationId,
                        WireContractCatalog.CreateTimeRequest.MessageType);
                    var nestedResult = DecodeAndValidate<CreateTimeRequestWire>(
                        nestedEnvelope,
                        WireContractCatalog.CreateTimeRequest,
                        receivedAt);
                    if (!nestedResult.IsValid)
                    {
                        errors.AddRange(nestedResult.Diagnostics.Select(x =>
                            new WireDiagnostic(x.Code, "/payload/wire_request" + x.Path.TrimStart('/').Replace("payload", string.Empty, StringComparison.Ordinal))));
                    }
                    if (nested.ValueKind == JsonValueKind.Object
                        && nested.TryGetProperty("request_id", out var nestedRequestId)
                        && payload.TryGetProperty("request_id", out var outboxRequestId)
                        && (nestedRequestId.ValueKind != JsonValueKind.String
                            || outboxRequestId.ValueKind != JsonValueKind.String
                            || nestedRequestId.GetString() != outboxRequestId.GetString()))
                        errors.Add(new("identity_mismatch", "/payload/wire_request/request_id"));
                }
                ValidateTypedScalars(payload, descriptor.MessageType, errors);
                    if (descriptor.MessageType == "policy.snapshot" && !ValidatePolicySnapshotHash(payload))
                    errors.Add(new("invalid_hash", "/payload/snapshot_hash"));
                if (errors.Count == 0) { var result = descriptor.Materialize(payload, errors); return errors.Count == 0 && result is not null ? WireDecodeResult<T>.Valid(result) : WireDecodeResult<T>.Invalid(errors); }
            }
        }
        return WireDecodeResult<T>.Invalid(errors);
    }

    /// <summary>
    /// Validates a typed payload through the same envelope boundary used by
    /// transport callers. This is intentionally the only adapter for legacy
    /// domain DTOs that have not yet been promoted to wire DTOs.
    /// </summary>
    public static WireDecodeResult<T> EncodeAndValidate<T>(T payload, Guid correlationId, ContractDescriptor<T> descriptor, DateTimeOffset? receivedAt = null)
    {
        if (correlationId == Guid.Empty)
        {
            return WireDecodeResult<T>.Invalid(new[] { new WireDiagnostic("invalid_uuid", "/correlation_id") });
        }

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, typeof(T), WireContractJsonContext.Default);
        return DecodeAndValidate(EncodeEnvelope(payloadBytes, correlationId, descriptor.MessageType), descriptor, receivedAt ?? DateTimeOffset.UtcNow);
    }

    /// <summary>Encodes a typed payload only after it passes the frozen boundary.</summary>
    public static byte[] EncodeValidatedEnvelope<T>(T payload, Guid correlationId, ContractDescriptor<T> descriptor, DateTimeOffset? receivedAt = null)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, typeof(T), WireContractJsonContext.Default);
        var envelope = EncodeEnvelope(payloadBytes, correlationId, descriptor.MessageType);
        var validation = DecodeAndValidate(envelope, descriptor, receivedAt ?? DateTimeOffset.UtcNow);
        if (!validation.IsValid)
            throw new JsonException("Payload failed contract validation.");

        return envelope;
    }

    /// <summary>Serializes a typed payload using the frozen envelope wire format.</summary>
    public static byte[] EncodeEnvelope<T>(T payload, Guid correlationId, ContractDescriptor<T> descriptor)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, typeof(T), WireContractJsonContext.Default);
        return EncodeEnvelope(payloadBytes, correlationId, descriptor.MessageType);
    }

    private static byte[] EncodeEnvelope(byte[] payloadBytes, Guid correlationId, string messageType)
    {
        using var payloadDocument = JsonDocument.Parse(payloadBytes);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("contract", "control-parental.windows");
            writer.WriteNumber("version", 1);
            writer.WriteString("message_type", messageType);
            writer.WriteString("correlation_id", correlationId.ToString("D"));
            writer.WritePropertyName("payload");
            payloadDocument.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    /// <summary>Validates an already serialized payload without deserializing it first.</summary>
    public static WireDecodeResult<T> DecodePayloadAndValidate<T>(
        ReadOnlySpan<byte> payloadUtf8,
        Guid correlationId,
        ContractDescriptor<T> descriptor)
    {
        try
        {
            if (!CanonicalJson.HasValidUtf16Escapes(payloadUtf8))
                return WireDecodeResult<T>.Invalid(new[] { new WireDiagnostic("invalid_unicode", "/payload") });
            using var payload = JsonDocument.Parse(payloadUtf8.ToArray());
            var duplicateErrors = new List<WireDiagnostic>();
            ValidateDuplicateMembers(payload.RootElement, "/payload", duplicateErrors);
            if (duplicateErrors.Count > 0) return WireDecodeResult<T>.Invalid(duplicateErrors);
            var envelope = EncodeEnvelope(payloadUtf8.ToArray(), correlationId, descriptor.MessageType);
            return DecodeAndValidate(envelope, descriptor);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or DecoderFallbackException)
        {
            return WireDecodeResult<T>.Invalid(new[] { new WireDiagnostic("invalid_payload", "/payload") });
        }
    }
    private static void ValidateDuplicateMembers(JsonElement value, string path, List<WireDiagnostic> errors)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) errors.Add(new("duplicate_member", path + "/" + property.Name));
                ValidateDuplicateMembers(property.Value, path + "/" + property.Name, errors);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray()) ValidateDuplicateMembers(item, path + "/" + index++, errors);
        }
    }
    private static void ValidateGlobalLimits(JsonElement value, string path, List<WireDiagnostic> errors)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                if (Encoding.UTF8.GetByteCount(value.GetString() ?? string.Empty) > 4096) errors.Add(new("string_oversize", path));
                break;
            case JsonValueKind.Array:
                if (value.GetArrayLength() > 256) errors.Add(new("array_oversize", path));
                var index = 0;
                foreach (var item in value.EnumerateArray()) ValidateGlobalLimits(item, path + "/" + index++, errors);
                break;
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject()) ValidateGlobalLimits(property.Value, path + "/" + property.Name, errors);
                break;
        }
    }
    private static void ValidateExtensions(JsonElement obj, string path, List<WireDiagnostic> e)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            if (obj.TryGetProperty("extensions", out var x)) ValidateExtensionValue(x, path + "/extensions", e);
            foreach (var property in obj.EnumerateObject()) ValidateExtensions(property.Value, path + "/" + property.Name, e);
        }
        else if (obj.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in obj.EnumerateArray()) ValidateExtensions(item, path + "/" + index++, e);
        }
    }

    private static void ValidateExtensionValue(JsonElement x, string path, List<WireDiagnostic> e)
    {
        static bool SecretToken(string value)
        {
            var normalized = System.Text.RegularExpressions.Regex.Replace(value.ToLowerInvariant(), "[-_.]+", "_");
            return new[] { "authority", "identity", "secret", "credential", "token", "grant", "verdict", "password", "passwd", "api_key", "apikey", "private_key", "access_key", "jwt", "role" }
                .Any(token => normalized == token || normalized.StartsWith("x_" + token, StringComparison.Ordinal));
        }
        static bool ForbiddenName(string name)
            => SecretToken(name);
        static bool Forbidden(JsonElement value)
            => value.ValueKind == JsonValueKind.String
                ? SecretToken(value.GetString() ?? string.Empty)
                : value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Any(p => Forbidden(p.Value) || ForbiddenName(p.Name))
                || value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(Forbidden);
        var canonicalSize = 0;
        try { canonicalSize = CanonicalJson.Utf8(x).Length; }
        catch (JsonException) { canonicalSize = int.MaxValue; }
        if (x.ValueKind != JsonValueKind.Object || x.EnumerateObject().Count() > 8 || canonicalSize > 8192
            || x.EnumerateObject().Any(p => ForbiddenName(p.Name) || !System.Text.RegularExpressions.Regex.IsMatch(p.Name, "^x-[a-z0-9][a-z0-9._-]{0,63}$") || Forbidden(p.Value)))
            e.Add(new("invalid_extensions", path));
    }

    private static void ValidateTypedScalars(JsonElement p, string type, List<WireDiagnostic> e)
    {
        static void Uuid(JsonElement p, string name, List<WireDiagnostic> e)
        {
            if (!p.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
                || !Guid.TryParseExact(value.GetString(), "D", out var id) || id == Guid.Empty
                || value.GetString() != id.ToString("D")
                || !System.Text.RegularExpressions.Regex.IsMatch(value.GetString() ?? string.Empty, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")
                )
                e.Add(new("invalid_uuid", "/payload/" + name));
        }
        static void Int64(JsonElement p, string name, List<WireDiagnostic> e)
        {
            if (!p.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number
                || !value.TryGetUInt64(out var number) || number > long.MaxValue)
                e.Add(new("invalid_int64", "/payload/" + name));
        }
        foreach (var name in type switch
        {
            "set_protected_account.request" => new[] { "operation_id" },
            "set_protected_account.response" => new[] { "operation_id", "correlation_id" },
            "create_time_request" => new[] { "request_id", "device_id" },
            "time_request.outbox" => new[] { "request_id", "device_generation" },
            "policy.snapshot" => new[] { "device_id" },
            "integrity.evidence" => new[] { "evidence_id", "device_generation" },
            "integrity.verdict" => new[] { "evidence_id" },
            _ => Array.Empty<string>()
        }) Uuid(p, name, e);
        foreach (var name in type switch
        {
            "create_time_request" => new[] { "policy_version" },
            "runtime_activation.state" => new[] { "state_version" },
            "policy.snapshot" => new[] { "version" },
            "integrity.verdict" => new[] { "verdict_version" },
            _ => Array.Empty<string>()
        }) Int64(p, name, e);
        if (type == "time_request.outbox"
            && (!p.TryGetProperty("attempt_count", out var attempts) || attempts.ValueKind != JsonValueKind.Number
                || !attempts.TryGetUInt32(out _)))
            e.Add(new("invalid_uint32", "/payload/attempt_count"));
        if (type == "set_protected_account.request")
        {
            if (!p.TryGetProperty("username", out var username) || username.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(username.GetString()) || Encoding.UTF8.GetByteCount(username.GetString()!) > 256)
                e.Add(new("invalid_username", "/payload/username"));
            if (!p.TryGetProperty("sid", out var sid) || sid.ValueKind != JsonValueKind.String
                || Encoding.ASCII.GetByteCount(sid.GetString() ?? string.Empty) > 184
                || !System.Text.RegularExpressions.Regex.IsMatch(sid.GetString() ?? "", @"^S-1-[0-5](?:-(?:0|[1-9]\d{0,17})){1,15}$"))
                e.Add(new("invalid_sid", "/payload/sid"));
        }
        if (type == "integrity.evidence"
            && (!p.TryGetProperty("binary_sha256", out var hash) || hash.ValueKind != JsonValueKind.String
                || !System.Text.RegularExpressions.Regex.IsMatch(hash.GetString() ?? "", "^[0-9a-f]{64}$")))
            e.Add(new("invalid_hash", "/payload/binary_sha256"));
        if (type == "integrity.evidence"
            && (!p.TryGetProperty("evidence_schema_version", out var schema) || !schema.TryGetUInt32(out var schemaVersion) || schemaVersion == 0))
            e.Add(new("invalid_uint32", "/payload/evidence_schema_version"));
        if (type == "integrity.evidence"
            && ((!p.TryGetProperty("agent_version", out var agent) || agent.ValueKind != JsonValueKind.String || !AsciiLength(agent.GetString(), 1, 128))
                || (!p.TryGetProperty("signer_summary", out var signer) || signer.ValueKind != JsonValueKind.String || !AsciiLength(signer.GetString(), 1, 256))))
            e.Add(new("invalid_value", "/payload/integrity"));

    }

    private static readonly DateTimeOffset FrozenContractClock = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
    private static void ValidateEnumsAndTimestamps(JsonElement p, string type, List<WireDiagnostic> e, DateTimeOffset now)
    {
        static bool Is(JsonElement p, string name, params string[] values) => p.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && values.Contains(value.GetString(), StringComparer.Ordinal);

        static void Enum(JsonElement p, string name, List<WireDiagnostic> e, params string[] values) { if (!Is(p, name, values)) e.Add(new("invalid_enum", "/payload/" + name)); }
        void Timestamp(JsonElement p, string name, List<WireDiagnostic> e, bool windowed = false)
        {
            if (!TryTimestamp(p, name, out var value)) { e.Add(new("invalid_timestamp", "/payload/" + name)); return; }
            if (windowed && (value > now.AddMinutes(5) || value < now.AddHours(-24)))
                e.Add(new(value > now ? "future" : "age", "/payload/" + name));
        }
        switch (type)
        {
            case "set_protected_account.response": Enum(p, "status", e, "accepted", "pending", "rejected", "failed"); Enum(p, "activation_state", e, "not_configured", "activating", "active", "degraded", "failed"); break;
            case "runtime_activation.state": Enum(p, "activation_state", e, "not_configured", "activating", "active", "degraded", "failed"); Timestamp(p, "observed_at", e); break;
            case "integrity.evidence": Enum(p, "signature_result", e, "valid", "invalid", "unknown"); Timestamp(p, "collected_at", e, true); break;
            case "integrity.verdict": Enum(p, "verdict", e, "trust", "revoked", "unknown"); Timestamp(p, "evaluated_at", e); break;
            case "create_time_request": Timestamp(p, "created_at", e, true); break;
            case "set_protected_account.request": Timestamp(p, "requested_at", e, true); break;
            case "time_request.outbox": Enum(p, "outbox_state", e, "queued", "pending", "approved", "denied", "failed", "applied"); if (p.TryGetProperty("next_attempt_at", out var next) && next.ValueKind != JsonValueKind.Null) Timestamp(p, "next_attempt_at", e); break;
            case "policy.snapshot":
                if (!Is(p, "device_state", "active", "locked", "downtime")) e.Add(new("invalid_enum", "/payload/device_state"));
                if (p.TryGetProperty("daily_screen_time_minutes", out var minutes)
                    && (!minutes.TryGetInt32(out var daily) || daily is < 0 or > 1440)) e.Add(new("bounds", "/payload/daily_screen_time_minutes"));
                if (p.TryGetProperty("snapshot_hash", out var hash)
                    && (hash.ValueKind != JsonValueKind.String || !System.Text.RegularExpressions.Regex.IsMatch(hash.GetString() ?? "", "^[0-9a-f]{64}$"))) e.Add(new("invalid_hash", "/payload/snapshot_hash"));
                if (!p.TryGetProperty("category_assignments", out var assignments) || assignments.ValueKind != JsonValueKind.Object
                    || assignments.EnumerateObject().Any(x => x.Name.Length is < 1 or > 64 || x.Value.ValueKind != JsonValueKind.String || !AsciiLength(x.Name, 1, 64) || !AsciiLength(x.Value.GetString(), 1, 64)))
                    e.Add(new("invalid_shape", "/payload/category_assignments"));
                ValidatePolicyNested(p, e);
                break;
            case "wns.hint":
            case "realtime.hint":
                var hasHintType = p.TryGetProperty("hint_type", out var hintType)
                    && hintType.ValueKind == JsonValueKind.String
                    && hintType.GetString()!.Length is >= 1 and <= 32
                    && hintType.GetString()!.All(c => c <= 0x7f);
                var hasCorrelation = p.TryGetProperty("correlation_hint", out var correlation)
                    && correlation.ValueKind == JsonValueKind.String
                    && Guid.TryParseExact(correlation.GetString(), "D", out var correlationId)
                    && correlationId != Guid.Empty
                    && correlation.GetString() == correlationId.ToString("D")
                    && System.Text.RegularExpressions.Regex.IsMatch(correlation.GetString() ?? string.Empty, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");
                if (p.TryGetProperty("hint_type", out _) && !hasHintType) e.Add(new("invalid_hint_type", "/payload/hint_type"));
                if (p.TryGetProperty("correlation_hint", out _) && !hasCorrelation) e.Add(new("invalid_uuid", "/payload/correlation_hint"));
                if (!hasHintType && !hasCorrelation) e.Add(new("empty_hint", "/payload"));
                break;
        }
        foreach (var name in new[] { "reason_code" })
            if (p.TryGetProperty(name, out var reason) && (reason.ValueKind != JsonValueKind.String || !System.Text.RegularExpressions.Regex.IsMatch(reason.GetString() ?? "", "^[a-z][a-z0-9_]{0,63}$"))) e.Add(new("invalid_reason_code", "/payload/" + name));
    }

    private static void ValidatePolicyNested(JsonElement payload, List<WireDiagnostic> errors)
    {
        var shapes = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["schedules"] = ["id", "days", "from", "to", "action", "allow_list"],
            ["category_limits"] = ["category", "minutes"],
            ["app_policies"] = ["package_name", "state", "daily_limit_minutes", "category", "allowed_windows"],
            ["grants"] = ["id", "request_id", "scope", "minutes", "granted_at", "expires_at", "source"],
        };
        foreach (var (name, allowed) in shapes)
        {
            if (!payload.TryGetProperty(name, out var collection) || collection.ValueKind != JsonValueKind.Array)
            {
                errors.Add(new("invalid_shape", "/payload/" + name));
                continue;
            }

            for (var index = 0; index < collection.GetArrayLength(); index++)
            {
                var item = collection[index];
                if (item.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new("invalid_shape", $"/payload/{name}/{index}"));
                    continue;
                }

                foreach (var property in item.EnumerateObject())
                {
                    if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                    {
                        errors.Add(new("unknown_member", $"/payload/{name}/{index}/{property.Name}"));
                    }
                }
                if (name == "schedules")
                {
                    foreach (var required in new[] { "id", "days", "from", "to", "action" })
                        if (!item.TryGetProperty(required, out _)) errors.Add(new("required", $"/payload/{name}/{index}/{required}"));
                    if (item.TryGetProperty("action", out var action) && action.ValueKind == JsonValueKind.String
                        && action.GetString() is not ("lock" or "allow_only"))
                        errors.Add(new("invalid_enum", $"/payload/{name}/{index}/action"));
                    if (item.TryGetProperty("days", out var days) && (days.ValueKind != JsonValueKind.Array || days.GetArrayLength() == 0 || days.EnumerateArray().Any(day => day.ValueKind != JsonValueKind.String || !new[] { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" }.Contains(day.GetString(), StringComparer.Ordinal)) || days.EnumerateArray().Select(day => day.GetString()).Distinct(StringComparer.Ordinal).Count() != days.GetArrayLength()))
                        errors.Add(new("invalid_enum", $"/payload/{name}/{index}/days"));
                    if (!item.TryGetProperty("id", out var scheduleId) || scheduleId.ValueKind != JsonValueKind.String || !AsciiLength(scheduleId.GetString(), 1, 64))
                        errors.Add(new("invalid_value", $"/payload/{name}/{index}/id"));
                    if ((item.TryGetProperty("from", out var from) && !IsClockTime(from)) || (item.TryGetProperty("to", out var to) && !IsClockTime(to)) || (item.TryGetProperty("allow_list", out var allowList) && (allowList.ValueKind != JsonValueKind.Array || allowList.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String || !AsciiLength(x.GetString(), 1, 4096)))))
                        errors.Add(new("invalid_value", $"/payload/{name}/{index}/time"));
                    if (item.TryGetProperty("action", out action) && action.ValueKind == JsonValueKind.String
                        && action.GetString() == "allow_only"
                        && (!item.TryGetProperty("allow_list", out var allow) || allow.ValueKind != JsonValueKind.Array || allow.GetArrayLength() == 0))
                        errors.Add(new("invalid_value", $"/payload/{name}/{index}/allow_list"));
                }
                if (name == "app_policies" && (!item.TryGetProperty("package_name", out var packageName) || packageName.ValueKind != JsonValueKind.String || !AsciiLength(packageName.GetString(), 1, 4096)))
                    errors.Add(new("invalid_value", $"/payload/{name}/{index}/package_name"));
                if (name == "app_policies" && (!item.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.String
                    || !new[] { "allowed", "blocked", "limited", "always_allowed" }.Contains(state.GetString(), StringComparer.Ordinal)))
                    errors.Add(new("invalid_enum", $"/payload/{name}/{index}/state"));
                if (name == "app_policies" && item.TryGetProperty("state", out state) && state.ValueKind == JsonValueKind.String && state.GetString() == "limited"
                    && (!item.TryGetProperty("daily_limit_minutes", out var limit) || limit.ValueKind != JsonValueKind.Number || !limit.TryGetInt32(out var limitValue) || limitValue is < 0 or > 1440))
                    errors.Add(new("required", $"/payload/{name}/{index}/daily_limit_minutes"));
                if (name == "category_limits" && (!item.TryGetProperty("category", out var category) || category.ValueKind != JsonValueKind.String || !AsciiLength(category.GetString(), 1, 64) || !item.TryGetProperty("minutes", out var categoryMinutes) || !categoryMinutes.TryGetInt32(out var categoryValue) || categoryValue is < 0 or > 1440))
                    errors.Add(new("bounds", $"/payload/{name}/{index}/minutes"));
                if (name == "app_policies" && item.TryGetProperty("category", out var appCategory)
                    && (appCategory.ValueKind != JsonValueKind.String || !AsciiLength(appCategory.GetString(), 1, 64)))
                    errors.Add(new("invalid_value", $"/payload/{name}/{index}/category"));
                if (name == "app_policies" && item.TryGetProperty("daily_limit_minutes", out var appLimit)
                    && (appLimit.ValueKind != JsonValueKind.Number || !appLimit.TryGetInt32(out var appLimitValue) || appLimitValue is < 0 or > 1440))
                    errors.Add(new("bounds", $"/payload/{name}/{index}/daily_limit_minutes"));
                if (name == "app_policies" && item.TryGetProperty("allowed_windows", out var windows) && windows.ValueKind != JsonValueKind.Array)
                    errors.Add(new("invalid_shape", $"/payload/{name}/{index}/allowed_windows"));
                if (name == "app_policies" && item.TryGetProperty("allowed_windows", out windows) && windows.ValueKind == JsonValueKind.Array)
                    foreach (var window in windows.EnumerateArray())
                        if (window.ValueKind != JsonValueKind.Object || window.EnumerateObject().Any(property => property.Name is not ("days" or "from" or "to")) || !window.TryGetProperty("days", out var windowDays) || windowDays.ValueKind != JsonValueKind.Array || windowDays.GetArrayLength() == 0 || windowDays.EnumerateArray().Any(day => day.ValueKind != JsonValueKind.String || !new[] { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" }.Contains(day.GetString(), StringComparer.Ordinal)) || windowDays.EnumerateArray().Select(day => day.GetString()).Distinct(StringComparer.Ordinal).Count() != windowDays.GetArrayLength() || !window.TryGetProperty("from", out var windowFrom) || !IsClockTime(windowFrom) || !window.TryGetProperty("to", out var windowTo) || !IsClockTime(windowTo))
                            errors.Add(new("invalid_enum", $"/payload/{name}/{index}/allowed_windows/days"));
                if (name == "grants" && (!item.TryGetProperty("minutes", out var grantMinutes) || grantMinutes.ValueKind != JsonValueKind.Number || !grantMinutes.TryGetInt32(out var grantValue) || grantValue is < 1 or > 180 || !item.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.String || !new[] { "extra_time", "reward", "manual" }.Contains(source.GetString(), StringComparer.Ordinal) || !item.TryGetProperty("scope", out var scope) || scope.ValueKind != JsonValueKind.String || !AsciiLength(scope.GetString(), 1, 4096)))
                    errors.Add(new("invalid_value", $"/payload/{name}/{index}"));
                if (name == "grants")
                {
                    var validGranted = TryTimestamp(item, "granted_at", out var grantedAt);
                    var validExpires = TryTimestamp(item, "expires_at", out var expiresAt);
                    if (!validGranted) errors.Add(new("invalid_timestamp", $"/payload/{name}/{index}/granted_at"));
                    if (!validExpires) errors.Add(new("invalid_timestamp", $"/payload/{name}/{index}/expires_at"));
                    if (validGranted && validExpires && expiresAt <= grantedAt) errors.Add(new("invalid_range", $"/payload/{name}/{index}/expires_at"));
                }
            }
        }
    }
}
