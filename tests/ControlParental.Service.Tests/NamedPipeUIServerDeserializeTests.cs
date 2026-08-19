// <copyright file="NamedPipeUIServerDeserializeTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Reflection;
using System.Text.Json;
using ControlParental.Domain;
using ControlParental.Service.Interop;
using Xunit;

/// <summary>
/// Regression coverage for the UI pipe server's source-generated message dispatch.
/// </summary>
public sealed class NamedPipeUIServerDeserializeTests
{
    [Fact]
    public void DeserializeMessage_TriggerSync_NotNull()
    {
        // Arrange
        var original = new TriggerSync();
        var json = JsonSerializer.Serialize(original, UIMessagesJsonContext.Default.TriggerSync);

        // Act
        var deserialized = DeserializeThroughServer(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<TriggerSync>(deserialized);
        Assert.Equal(nameof(TriggerSync), deserialized!.MessageType);
    }

    [Fact]
    public void DeserializeMessage_AdvanceOnboardingStep_NotNull()
    {
        // Arrange
        var original = new AdvanceOnboardingStep();
        var json = JsonSerializer.Serialize(original, UIMessagesJsonContext.Default.AdvanceOnboardingStep);

        // Act
        var deserialized = DeserializeThroughServer(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<AdvanceOnboardingStep>(deserialized);
    }

    [Fact]
    public void DeserializeMessage_ResetOnboardingState_WithReason_NotNull()
    {
        // Arrange
        var original = new ResetOnboardingState("user requested");
        var json = JsonSerializer.Serialize(original, UIMessagesJsonContext.Default.ResetOnboardingState);

        // Act
        var deserialized = DeserializeThroughServer(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<ResetOnboardingState>(deserialized);
        Assert.Equal("user requested", ((ResetOnboardingState)deserialized).Reason);
    }

    /// <summary>
    /// Spec: Malformed IPC JSON arrives. The parser MUST fail closed (return null)
    /// and the process MUST remain alive. Calling the listener must not throw.
    /// </summary>
    [Fact]
    public void DeserializeMessage_MalformedJson_ReturnsNull()
    {
        // Arrange — explicitly malformed JSON that JsonDocument.Parse cannot read.
        var malformed = "{ this is not valid json }";

        // Act
        var deserialized = DeserializeThroughServer(malformed);

        // Assert — fail closed: no message produced, no exception escapes.
        Assert.Null(deserialized);
    }

    /// <summary>
    /// Spec: Malformed IPC JSON — covers a top-level non-object payload (e.g. an
    /// array or a plain string). The parser MUST fail closed because the
    /// dispatch expects a JSON object with a "MessageType" property.
    /// </summary>
    [Fact]
    public void DeserializeMessage_NonObjectJson_ReturnsNull()
    {
        // Arrange — a JSON array (valid JSON but not a message envelope).
        var notAMessage = "[1,2,3]";

        // Act
        var deserialized = DeserializeThroughServer(notAMessage);

        // Assert — does not throw and does not produce a message.
        Assert.Null(deserialized);
    }

    /// <summary>
    /// Spec: an unknown MessageType (valid JSON, valid object, but no handler)
    /// MUST be dropped without crashing the dispatch.
    /// </summary>
    [Fact]
    public void DeserializeMessage_UnknownMessageType_ReturnsNull()
    {
        // Arrange — well-formed JSON object with an unrecognized MessageType.
        var unknown = "{\"MessageType\":\"NotARealMessage\"}";

        // Act
        var deserialized = DeserializeThroughServer(unknown);

        // Assert
        Assert.Null(deserialized);
    }

    private static IIpcMessage? DeserializeThroughServer(string json)
    {
        var listenerType = typeof(NamedPipeUIServer).GetNestedType(
            "PipeServerListener",
            BindingFlags.NonPublic);
        Assert.NotNull(listenerType);

        var listener = Activator.CreateInstance(
            listenerType!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object?[] { "test", null, null, null, null, CancellationToken.None, null, null, null },
            culture: null);
        Assert.NotNull(listener);

        try
        {
            var deserializeMethod = listenerType!.GetMethod(
                "DeserializeMessage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(deserializeMethod);
            return (IIpcMessage?)deserializeMethod!.Invoke(listener, new object[] { json });
        }
        finally
        {
            ((IDisposable)listener!).Dispose();
        }
    }
}
