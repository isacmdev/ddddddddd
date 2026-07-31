// <copyright file="IpcMessageContractTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

#pragma warning disable SA1636

// <copyright file="IpcMessageContractTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>
namespace ControlParental.App.UI.Tests;

using System.Reflection;
using System.Text.Json;
using ControlParental.Domain;
using Xunit;

#pragma warning disable CA1707, SA1600, SA1503, SA1636, CS1591

public sealed class IpcMessageContractTests
{
    [Fact]
    public void EveryIpcRecord_RoundTripsWithoutChangingFields()
    {
        var messageTypes = typeof(IUIMessage).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IUIMessage).IsAssignableFrom(type))
            .Where(type => type.GetConstructors().Length != 0)
            .ToArray();

        Assert.NotEmpty(messageTypes);
        foreach (var type in messageTypes)
        {
            var message = CreateMessage(type);
            var json = JsonSerializer.Serialize(message, type);
            var roundTripped = JsonSerializer.Deserialize(json, type);

            Assert.NotNull(roundTripped);
            Assert.Equal(json, JsonSerializer.Serialize(roundTripped, type));
            Assert.Equal(((IUIMessage)message).MessageType, ((IUIMessage)roundTripped!).MessageType);
        }
    }

    [Fact]
    public void ListAccounts_RoundTrip()
    {
        var original = new ListAccounts();

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<ListAccounts>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(nameof(ListAccounts), roundTripped!.MessageType);
    }

    [Fact]
    public void AdvanceOnboardingStep_RoundTrip_PreservesAllFields()
    {
        // T26 PR #11 — explicit guard for the new message type. The dynamic
        // EveryIpcRecord_RoundTrips test above covers this implicitly, but a
        // dedicated test documents the field set (no payload today) and gives
        // a sharper failure when a future refactor adds a payload field that
        // does not round-trip.
        var original = new AdvanceOnboardingStep();

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<AdvanceOnboardingStep>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.MessageType, roundTripped!.MessageType);
    }

    [Fact]
    public void ResetOnboardingState_RoundTrip_PreservesReason()
    {
        // T26 PR #11 — explicit guard for the optional Reason field. The dynamic
        // test uses a generic reflection-based factory so it cannot exercise a
        // specific Reason value; this test pins the audit-trail payload.
        var original = new ResetOnboardingState("kiosk reboot for incident #42");

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<ResetOnboardingState>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal("kiosk reboot for incident #42", roundTripped!.Reason);
        Assert.Equal(original.MessageType, roundTripped.MessageType);
    }

    private static object CreateMessage(Type type)
    {
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var arguments = constructor.GetParameters()
            .Select(parameter => CreateValue(parameter.ParameterType, parameter.Name ?? "field"))
            .ToArray();
        return constructor.Invoke(arguments);
    }

    private static object? CreateValue(Type type, string name)
    {
        if (type == typeof(string))
        {
            return name;
        }

        if (type == typeof(bool)) return true;
        if (type == typeof(int)) return 7;
        if (type == typeof(long)) return 42L;
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.UtcNow;
        if (type.IsEnum) return Enum.GetValues(type).GetValue(0);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            var element = type.GetGenericArguments()[0];
            var array = Array.CreateInstance(element, 1);
            array.SetValue(CreateValue(element, name), 0);
            return array;
        }

        if (type.IsClass && type.GetConstructors().Length != 0)
        {
            return CreateMessage(type);
        }

        return null;
    }
}
