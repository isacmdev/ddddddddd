// <copyright file="SharedJsonContextTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Text.Json;
using ControlParental.Domain;
using Xunit;

/// <summary>
/// Coverage for <see cref="SharedJsonContext"/> — the canonical catalogue that
/// closes T14/E5 (shared JSON handling). Each registered type is round-tripped
/// through the source-generated metadata to prove the catalogue is wired
/// correctly and that snake_case wire contracts survive the round-trip.
/// </summary>
public class SharedJsonContextTests
{
    [Fact]
    public void RoundTrip_UsageLogEntry_PreservesSnakeCaseContract()
    {
        var original = new UsageLogEntry
        {
            AppId = "app.exe",
            Minutes = 42,
            ServerDate = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero),
            DedupKey = "dedup-1",
        };

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.UsageLogEntry);

        Assert.Contains("\"appId\"", json); // camelCase per SharedJsonContext naming policy.
        var roundTripped = JsonSerializer.Deserialize(json, SharedJsonContext.Default.UsageLogEntry);
        Assert.NotNull(roundTripped);
        Assert.Equal(original.AppId, roundTripped!.AppId);
        Assert.Equal(original.Minutes, roundTripped.Minutes);
        Assert.Equal(original.ServerDate, roundTripped.ServerDate);
        Assert.Equal(original.DedupKey, roundTripped.DedupKey);
    }

    [Fact]
    public void RoundTrip_HeartbeatData_PreservesValues()
    {
        var original = new HeartbeatData
        {
            Enforcement = EnforcementLevel.Standard,
            BatteryPct = 80,
            ClockOffsetMs = 150,
            AgentUptimeMs = 3_600_000,
        };

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.HeartbeatData);
        var roundTripped = JsonSerializer.Deserialize(json, SharedJsonContext.Default.HeartbeatData);

        Assert.NotNull(roundTripped);
        Assert.Equal(EnforcementLevel.Standard, roundTripped!.Enforcement);
        Assert.Equal(80, roundTripped.BatteryPct);
        Assert.Equal(150, roundTripped.ClockOffsetMs);
        Assert.Equal(3_600_000, roundTripped.AgentUptimeMs);
    }

    [Fact]
    public void RoundTrip_PolicyFetchResponse_PreservesValues()
    {
        var original = new PolicyFetchResponse
        {
            Version = 7,
            PolicyJson = "{\"version\":7}",
        };

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.PolicyFetchResponse);

        Assert.Contains("\"policy_json\"", json);
        var roundTripped = JsonSerializer.Deserialize(json, SharedJsonContext.Default.PolicyFetchResponse);
        Assert.NotNull(roundTripped);
        Assert.Equal(7, roundTripped!.Version);
        Assert.Equal("{\"version\":7}", roundTripped.PolicyJson);
    }

    [Fact]
    public void RoundTrip_HeartbeatResponse_PreservesValues()
    {
        var original = new HeartbeatResponse
        {
            ServerTimeOffsetMs = 1234,
            NewPolicyAvailable = true,
        };

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.HeartbeatResponse);
        var roundTripped = JsonSerializer.Deserialize(json, SharedJsonContext.Default.HeartbeatResponse);

        Assert.NotNull(roundTripped);
        Assert.Equal(1234L, roundTripped!.ServerTimeOffsetMs);
        Assert.True(roundTripped.NewPolicyAvailable);
    }

    [Fact]
    public void RoundTrip_IntegrityReport_PreservesValues()
    {
        var original = new IntegrityReport
        {
            ReportHash = "ABCD",
            Timestamp = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero),
            AgentVersion = "1.0.0",
            Platform = "windows",
            SignatureValid = true,
            BinaryHash = "DEAD",
        };

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.IntegrityReport);
        var roundTripped = JsonSerializer.Deserialize(json, SharedJsonContext.Default.IntegrityReport);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.ReportHash, roundTripped!.ReportHash);
        Assert.Equal(original.Timestamp, roundTripped.Timestamp);
        Assert.Equal(original.AgentVersion, roundTripped.AgentVersion);
        Assert.Equal(original.Platform, roundTripped.Platform);
        Assert.Equal(original.SignatureValid, roundTripped.SignatureValid);
        Assert.Equal(original.BinaryHash, roundTripped.BinaryHash);
    }

    [Fact]
    public void RoundTrip_DeviceAlertEntry_PreservesValues()
    {
        var original = new DeviceAlertEntry
        {
            EventType = "tamper",
            Description = "clock jump",
            Severity = "Warning",
            DetectedAt = new DateTimeOffset(2026, 7, 23, 1, 2, 3, TimeSpan.Zero),
            DedupKey = "alert-1",
        };

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.DeviceAlertEntry);
        var roundTripped = JsonSerializer.Deserialize(json, SharedJsonContext.Default.DeviceAlertEntry);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.EventType, roundTripped!.EventType);
        Assert.Equal(original.Description, roundTripped.Description);
        Assert.Equal(original.Severity, roundTripped.Severity);
        Assert.Equal(original.DetectedAt, roundTripped.DetectedAt);
        Assert.Equal(original.DedupKey, roundTripped.DedupKey);
    }

    [Fact]
    public void DeviceAlertEntry_SerializesAsCamelCaseThroughSharedJsonContext()
    {
        // Phase 4 seam: DeviceAlertEntry is the typed domain contract. Through
        // SharedJsonContext it MUST serialize as camelCase — the SharedJsonContext
        // is the canonical serializer for domain/local data (e.g. outbox payloads),
        // and the camelCase naming policy is part of that contract.
        var original = new DeviceAlertEntry
        {
            EventType = "tamper",
            Description = "clock jump",
            Severity = "Warning",
            DetectedAt = new DateTimeOffset(2026, 7, 23, 1, 2, 3, TimeSpan.Zero),
            DedupKey = "alert-1",
        };

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.DeviceAlertEntry);

        Assert.Contains("\"eventType\":", json);
        Assert.Contains("\"description\":", json);
        Assert.Contains("\"severity\":", json);
        Assert.Contains("\"detectedAt\":", json);
        Assert.Contains("\"dedupKey\":", json);

        // And the snake_case wire shape must NOT leak into the domain contract.
        Assert.DoesNotContain("\"event_type\":", json);
        Assert.DoesNotContain("\"detected_at\":", json);
        Assert.DoesNotContain("\"dedup_key\":", json);
    }

    [Fact]
    public void DeviceAlertEntry_PropertySet_IsStableAndDomainOnly()
    {
        // Phase 4 seam: the domain contract must NOT widen with backend-specific
        // or wire-specific properties. The set of public properties must stay
        // exactly the same — DeviceAlertEntry is the canonical, reusable contract
        // for service-health alerts; the wire shape lives behind a private DTO
        // inside BackendClient.
        var props = typeof(DeviceAlertEntry)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "DedupKey", "Description", "DetectedAt", "EventType", "Severity" },
            props);
    }

    [Fact]
    public void DeviceAlertEntry_FromCamelCaseJson_RoundTripsThroughSharedJsonContext()
    {
        // Phase 4 seam: SharedJsonContext is the catalogue for domain/local data.
        // Verifying that camelCase JSON (what producers like ServiceHealthMonitor
        // and OutboxManager emit) round-trips back into a fully populated domain
        // object — without leaking wire-only assumptions like snake_case.
        const string camelCaseJson = @"{
            ""eventType"": ""tamper"",
            ""description"": ""clock jump"",
            ""severity"": ""Warning"",
            ""detectedAt"": ""2026-07-23T01:02:03.0000000+00:00"",
            ""dedupKey"": ""alert-1""
        }";

        var roundTripped = JsonSerializer.Deserialize(camelCaseJson, SharedJsonContext.Default.DeviceAlertEntry);

        Assert.NotNull(roundTripped);
        Assert.Equal("tamper", roundTripped!.EventType);
        Assert.Equal("clock jump", roundTripped.Description);
        Assert.Equal("Warning", roundTripped.Severity);
        Assert.Equal(new DateTimeOffset(2026, 7, 23, 1, 2, 3, TimeSpan.Zero), roundTripped.DetectedAt);
        Assert.Equal("alert-1", roundTripped.DedupKey);
    }

    [Fact]
    public void RoundTrip_BehavioralEventEntry_PreservesValues()
    {
        var original = new BehavioralEventEntry
        {
            EventType = "app_launched",
            AppId = "app.exe",
            Timestamp = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero),
            DedupKey = "ev-1",
        };

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.BehavioralEventEntry);
        var roundTripped = JsonSerializer.Deserialize(json, SharedJsonContext.Default.BehavioralEventEntry);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.EventType, roundTripped!.EventType);
        Assert.Equal(original.AppId, roundTripped.AppId);
        Assert.Equal(original.Timestamp, roundTripped.Timestamp);
        Assert.Equal(original.DedupKey, roundTripped.DedupKey);
    }

    [Fact]
    public void RoundTrip_TimeRequestEntry_PreservesValues()
    {
        var original = new TimeRequestEntry
        {
            RequestId = "req-1",
            Minutes = 30,
            Reason = "Homework",
            CreatedAt = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero),
        };

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.TimeRequestEntry);
        var roundTripped = JsonSerializer.Deserialize(json, SharedJsonContext.Default.TimeRequestEntry);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.RequestId, roundTripped!.RequestId);
        Assert.Equal(original.Minutes, roundTripped.Minutes);
        Assert.Equal(original.Reason, roundTripped.Reason);
        Assert.Equal(original.CreatedAt, roundTripped.CreatedAt);
    }

    [Fact]
    public void RoundTrip_PairingRequest_PreservesValues()
    {
        var original = new PairingRequest(
            Code: "ABCD2345",
            DeviceName: "TEST-PC",
            DeviceModel: "Dell XPS",
            OsVersion: "Windows 11",
            AppVersion: "1.0.0",
            AgeBand: "13-16");

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.PairingRequest);
        var roundTripped = JsonSerializer.Deserialize(json, SharedJsonContext.Default.PairingRequest);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Code, roundTripped!.Code);
        Assert.Equal(original.DeviceName, roundTripped.DeviceName);
        Assert.Equal(original.DeviceModel, roundTripped.DeviceModel);
        Assert.Equal(original.OsVersion, roundTripped.OsVersion);
        Assert.Equal(original.AppVersion, roundTripped.AppVersion);
        Assert.Equal(original.AgeBand, roundTripped.AgeBand);
    }

    [Fact]
    public void RoundTrip_PairingSuccessResponse_PreservesValues()
    {
        var original = new PairingSuccessResponse
        {
            DeviceId = "device-1",
            ParentId = "parent-1",
            PolicyVersion = 7,
        };

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.PairingSuccessResponse);

        Assert.Contains("\"device_id\"", json);
        var roundTripped = JsonSerializer.Deserialize(json, SharedJsonContext.Default.PairingSuccessResponse);

        Assert.NotNull(roundTripped);
        Assert.Equal("device-1", roundTripped!.DeviceId);
        Assert.Equal("parent-1", roundTripped.ParentId);
        Assert.Equal(7, roundTripped.PolicyVersion);
    }

    [Fact]
    public void RoundTrip_ListUsageLogEntry_PreservesCount()
    {
        var original = new List<UsageLogEntry>
        {
            new() { AppId = "a", Minutes = 1, ServerDate = DateTimeOffset.UtcNow, DedupKey = "k1" },
            new() { AppId = "b", Minutes = 2, ServerDate = DateTimeOffset.UtcNow, DedupKey = "k2" },
        };

        var json = JsonSerializer.Serialize(original, SharedJsonContext.Default.ListUsageLogEntry);
        var roundTripped = JsonSerializer.Deserialize(json, SharedJsonContext.Default.ListUsageLogEntry);

        Assert.NotNull(roundTripped);
        Assert.Equal(2, roundTripped!.Count);
    }
}
