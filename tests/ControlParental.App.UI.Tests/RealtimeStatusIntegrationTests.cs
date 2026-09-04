// <copyright file="RealtimeStatusIntegrationTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Xunit;

public sealed class RealtimeStatusIntegrationTests
{
    [Fact]
    public async Task AcceptedHint_UsesAuthenticatedServiceTriggerSyncWithoutApplyingPayload()
    {
        var policyChannel = new FakeRealtimeChannel();
        var grantsChannel = new FakeRealtimeChannel();
        var lifecycle = new FakeWindowLifecycleObserver();
        var uiChannel = new RecordingUIChannel();
        var subscriber = new RealtimeSubscriber(
            policyChannel,
            grantsChannel,
            lifecycle,
            "device-123",
            uiChannel);
        var policyChanged = 0;
        var grantsChanged = 0;
        subscriber.PolicyChanged += (_, _) => policyChanged++;
        subscriber.GrantsChanged += (_, _) => grantsChanged++;

        try
        {
            lifecycle.SimulateEnterForeground();
            await subscriber.ConnectAsync();

            policyChannel.FireBroadcast(new Dictionary<string, object?>
            {
                ["contract"] = "control-parental.windows",
                ["version"] = 1,
                ["message_type"] = "realtime.hint",
                ["correlation_id"] = "00000000-0000-4000-8000-000000000030",
                ["payload"] = new Dictionary<string, object?> { ["hint_type"] = "sync" },
            });

            await SpinUntilAsync(() => uiChannel.TriggerSyncCount == 1);
            Assert.Equal(1, uiChannel.TriggerSyncCount);
            Assert.Equal(0, policyChanged);
            Assert.Equal(0, grantsChanged);
        }
        finally
        {
            subscriber.Dispose();
            await subscriber.LifecycleTask;
        }
    }

    private static async Task SpinUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class RecordingUIChannel : IUIChannel
    {
        public int TriggerSyncCount { get; private set; }

        public Task<TResponse?> QueryAsync<TQuery, TResponse>(
            TQuery query,
            CancellationToken ct = default)
            where TQuery : IUIMessage
            where TResponse : class, IUIMessage => Task.FromResult<TResponse?>(null);

        public Task SendAsync<T>(T message, CancellationToken ct = default)
            where T : IUIMessage
        {
            if (message is TriggerSync)
            {
                this.TriggerSyncCount++;
            }

            return Task.CompletedTask;
        }
    }
}
