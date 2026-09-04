namespace ControlParental.Domain.WireContracts;

using System.Text.Json;
using System.Text.Json.Serialization;
using ControlParental.Domain.WireContracts.Models;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SetProtectedAccountRequestWire))]
[JsonSerializable(typeof(SetProtectedAccountResponseWire))]
[JsonSerializable(typeof(RuntimeActivationStateWire))]
[JsonSerializable(typeof(CreateTimeRequestWire))]
[JsonSerializable(typeof(TimeRequestOutboxWire))]
[JsonSerializable(typeof(IntegrityEvidenceWire))]
[JsonSerializable(typeof(IntegrityVerdictWire))]
[JsonSerializable(typeof(HintWire))]
[JsonSerializable(typeof(PolicySnapshotWire))]
[JsonSerializable(typeof(FrozenScheduleWire))]
[JsonSerializable(typeof(FrozenCategoryLimitWire))]
[JsonSerializable(typeof(FrozenWindowWire))]
[JsonSerializable(typeof(FrozenAppPolicyWire))]
[JsonSerializable(typeof(FrozenGrantWire))]
[JsonSerializable(typeof(RuntimeActivationResultWire))]
public sealed partial class WireContractJsonContext : JsonSerializerContext;
