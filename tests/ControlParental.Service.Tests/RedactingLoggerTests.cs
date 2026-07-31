// <copyright file="RedactingLoggerTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Text.Json;
using ControlParental.Domain;
using Xunit;

/// <summary>
/// Coverage for <see cref="RedactingLogger"/> — T14/E6 redacted observability.
/// Pinned invariants:
/// - allow-listed scalar fields pass through verbatim;
/// - deny-listed fields are replaced with the redaction token regardless of value;
/// - unknown fields keep structure (collection vs scalar) but never expose raw values.
/// </summary>
public class RedactingLoggerTests
{
    [Fact]
    public void Write_AllowListedField_PassesThrough()
    {
        var logger = RedactingLogger.Default;

        var json = logger.Write(
            "test.event",
            new Dictionary<string, object?>
            {
                ["device_id"] = "device-abc-123",
                ["app_id"] = "com.example.app",
            });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("device-abc-123", root.GetProperty("device_id").GetString());
        Assert.Equal("com.example.app", root.GetProperty("app_id").GetString());
    }

    [Theory]
    [InlineData("access_token", "supersecret")]
    [InlineData("refresh_token", "supersecret")]
    [InlineData("id_token", "supersecret")]
    [InlineData("authorization", "Bearer xyz")]
    [InlineData("apikey", "anon-key")]
    [InlineData("channel_uri", "https://wns.example/abc")]
    [InlineData("push_handle", "https://wns.example/handle")]
    [InlineData("raw_body", "{\"foo\":\"bar\"}")]
    [InlineData("credentials", "user:pass")]
    [InlineData("password", "hunter2")]
    [InlineData("secret", "shh")]
    [InlineData("report_hash", "deadbeef")]
    [InlineData("binary_hash", "cafebabe")]
    [InlineData("executable_path", "C:\\Windows\\System32\\cmd.exe")]
    [InlineData("payload", "any value")]
    public void Write_DenyListedField_IsReplaced(string fieldName, string fieldValue)
    {
        var logger = RedactingLogger.Default;

        var json = logger.Write(
            "test.deny",
            new Dictionary<string, object?> { [fieldName] = fieldValue });

        using var doc = JsonDocument.Parse(json);
        var actual = doc.RootElement.GetProperty(fieldName).GetString();
        Assert.Equal("[REDACTED]", actual);
    }

    [Fact]
    public void Write_UnknownScalarField_IsReplacedWithToken()
    {
        var logger = RedactingLogger.Default;

        var json = logger.Write(
            "test.unknown",
            new Dictionary<string, object?>
            {
                ["some_mystery_field"] = "leakable-secret",
            });

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("[REDACTED]", doc.RootElement.GetProperty("some_mystery_field").GetString());
    }

    [Fact]
    public void Write_UnknownCollectionField_KeepsShapeButStripsValues()
    {
        var logger = RedactingLogger.Default;

        var json = logger.Write(
            "test.collection",
            new Dictionary<string, object?>
            {
                ["unrecognized_collection"] = new[] { "a", "b", "c" },
            });

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("[collection]", doc.RootElement.GetProperty("unrecognized_collection").GetString());
    }

    [Fact]
    public void Write_AllowsCustomAllowListToPassExtraField()
    {
        var logger = new RedactingLogger(allowList: new[] { "tenant_id" });

        var json = logger.Write(
            "test.custom",
            new Dictionary<string, object?>
            {
                ["tenant_id"] = "tenant-007",
            });

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("tenant-007", doc.RootElement.GetProperty("tenant_id").GetString());
    }

    [Fact]
    public void Write_NestedDenyListField_IsRedactedRecursively()
    {
        var logger = RedactingLogger.Default;

        // Use an allow-listed container key so the recursive sanitization path
        // is taken for the nested dictionary. Allow-listed fields pass through
        // their value, and nested secrets inside them are still redacted.
        var nested = new Dictionary<string, object?>
        {
            ["device_id"] = "device-1",
            ["access_token"] = "should-not-leak",
        };

        var json = logger.Write(
            "test.nested",
            new Dictionary<string, object?>
            {
                ["device_id"] = nested,
            });

        using var doc = JsonDocument.Parse(json);
        // Allow-listed container passes through; nested deny-listed field is redacted.
        var nestedElement = doc.RootElement.GetProperty("device_id");
        Assert.Equal("device-1", nestedElement.GetProperty("device_id").GetString());
        Assert.Equal("[REDACTED]", nestedElement.GetProperty("access_token").GetString());
    }

    [Fact]
    public void Write_EventNameAndTimestamp_AreAlwaysPresent()
    {
        var logger = RedactingLogger.Default;

        var json = logger.Write("test.event", new Dictionary<string, object?>());

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("test.event", doc.RootElement.GetProperty("event_name").GetString());
        Assert.True(doc.RootElement.TryGetProperty("timestamp", out _));
    }
}
