// <copyright file="UIMessagesJsonContextTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ControlParental.Domain;
using Xunit;

/// <summary>
/// T26 PR #14 — Regression coverage for the source-generated
/// <see cref="UIMessagesJsonContext"/>. Closes S4 (verify report) by proving
/// (a) every <see cref="IIpcMessage"/> record in the Domain assembly has
/// type info registered in the source-gen context and (b) source-gen
/// round-trips preserve data for a representative sample of envelope types.
///
/// These tests intentionally use reflection + the <see cref="Type"/> API
/// (not the source-gen metadata directly) so the failure mode is "missing
/// registration" — adding a new IPC envelope record without registering
/// it in <c>UIMessagesJsonContext</c> will fail this test with a clear
/// diff rather than crashing the production code path.
/// </summary>
public sealed class UIMessagesJsonContextTests
{
    [Fact]
    public void UIMessagesJsonContext_ProvidesTypeInfo_ForEveryIpcMessageRecord()
    {
        // Arrange — every concrete sealed record implementing IIpcMessage
        // in the Domain assembly.
        var domainAssembly = typeof(IIpcMessage).Assembly;
        var envelopeTypes = domainAssembly
            .GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .Where(type => typeof(IIpcMessage).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(envelopeTypes);

        // Act + Assert — UIMessagesJsonContext exposes a JsonTypeInfo<T>
        // property for each registered type. Reflection finds them and
        // confirms the type matches the registry.
        var contextType = typeof(UIMessagesJsonContext);
        var registeredTypes = contextType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(JsonTypeInfo<>))
            .Select(property => property.PropertyType.GetGenericArguments()[0])
            .ToHashSet();

        var missing = envelopeTypes.Where(t => !registeredTypes.Contains(t)).ToArray();
        Assert.True(
            missing.Length == 0,
            "UIMessagesJsonContext is missing [JsonSerializable] registration for: "
            + string.Join(", ", missing.Select(t => t.FullName))
            + ". Add them so the source-gen metadata is generated and the IPC layer can "
            + "serialize/deserialize them without reflection.");
    }

    [Fact]
    public void UIMessagesJsonContext_ProvidesTypeInfo_ForPayloadRecords()
    {
        // The IPC envelopes carry these payload records inside their
        // constructor params (OnboardingStateResponse.State, AccountList.Accounts, etc.).
        // They must be in the catalogue too so the source-gen metadata covers
        // the full transitive closure.
        var payloadTypes = new[]
        {
            typeof(OnboardingState),
            typeof(OnboardingStep),
            typeof(FunnelEvent),
            typeof(GrantInfo),
            typeof(ActiveIssue),
            typeof(AccountInfo),
        };

        var contextType = typeof(UIMessagesJsonContext);
        var registeredTypes = contextType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(JsonTypeInfo<>))
            .Select(property => property.PropertyType.GetGenericArguments()[0])
            .ToHashSet();

        var missing = payloadTypes.Where(t => !registeredTypes.Contains(t)).ToArray();
        Assert.True(
            missing.Length == 0,
            "UIMessagesJsonContext is missing payload type registration for: "
            + string.Join(", ", missing.Select(t => t.FullName)));
    }

    [Theory]
    [InlineData("EnforcementLevel")]
    [InlineData("EnforcementIssueType")]
    [InlineData("EnforcementIssueSeverity")]
    [InlineData("OnboardingStepStatus")]
    [InlineData("FunnelEventType")]
    [InlineData("GrantSource")]
    [InlineData("PairingStatus")]
    public void UIMessagesJsonContext_ProvidesTypeInfo_ForEnumTypesReferencedByEnvelopes(string enumTypeName)
    {
        var domainAssembly = typeof(IIpcMessage).Assembly;
        var enumType = domainAssembly.GetTypes().Single(t => t.Name == enumTypeName && t.IsEnum);

        var property = typeof(UIMessagesJsonContext).GetProperty(
            enumTypeName,
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.Equal(typeof(JsonTypeInfo<>).MakeGenericType(enumType), property!.PropertyType);
    }

    [Fact]
    public void JsonSerializer_RoundTrip_AllSampleEnvelopes_PreservesData_ViaSourceGen()
    {
        // Sample of representative envelope types — exercise the source-gen
        // catalogue end-to-end. The dynamic "EveryIpcRecord_RoundTrips"
        // test in IpcMessageContractTests covers the full set; this test
        // pins a few specific cases through the source-gen metadata
        // (not reflection) to prove the catalogue is wired correctly.
        var samples = new (string Name, object Value, JsonTypeInfo TypeInfo)[]
        {
            ("PairDevice", new PairDevice("ABC12345", "13-16"), UIMessagesJsonContext.Default.PairDevice),
            ("PairDeviceResponse", new PairDeviceResponse(true, "dev-1", "par-1", 7, PairingStatus.Success, null), UIMessagesJsonContext.Default.PairDeviceResponse),
            ("ListAccounts", new ListAccounts(), UIMessagesJsonContext.Default.ListAccounts),
            ("AccountList", new AccountList(new[] { new AccountInfo("alice", "Standard", true) }), UIMessagesJsonContext.Default.AccountList),
            ("AdvanceOnboardingStep", new AdvanceOnboardingStep(), UIMessagesJsonContext.Default.AdvanceOnboardingStep),
            ("ResetOnboardingState", new ResetOnboardingState("test reason"), UIMessagesJsonContext.Default.ResetOnboardingState),
        };

        foreach (var (name, value, typeInfo) in samples)
        {
            var json = JsonSerializer.Serialize(value, typeInfo);
            Assert.False(string.IsNullOrWhiteSpace(json), $"{name}: source-gen serialize produced empty JSON");

            var roundTripped = JsonSerializer.Deserialize(json, typeInfo);
            Assert.NotNull(roundTripped);

            // Re-serializing the round-tripped value must produce the same
            // JSON — the strongest source-gen invariant we can assert
            // without coupling to record equality semantics.
            var roundTrippedJson = JsonSerializer.Serialize(roundTripped, typeInfo);
            Assert.Equal(json, roundTrippedJson);
        }
    }
}
