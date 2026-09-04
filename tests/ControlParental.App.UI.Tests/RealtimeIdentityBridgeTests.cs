namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI;
using ControlParental.Domain;
using ControlParental.App.UI.Interop;
using FluentAssertions;
using Xunit;

public sealed class RealtimeIdentityBridgeTests
{
    [Fact]
    public async Task StartAsync_PublishesServiceLeaseWithoutPersistingIt()
    {
        var token = "header.payload.signature";
        var channel = new FakeChannel(new RealtimeIdentityResponse(true, token, "device-a", 4, DateTimeOffset.UtcNow.AddMinutes(5), "none"));
        using var bridge = new RealtimeIdentityBridge(channel);

        await bridge.StartAsync();

        bridge.Current.Should().NotBeNull();
        bridge.Current!.AccessToken.Should().Be(token);
        bridge.Current.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RefreshAsync_InvalidResponseClearsLease()
    {
        var channel = new FakeChannel(new RealtimeIdentityResponse(true, "header.payload.signature", "device-a", 4, DateTimeOffset.UtcNow.AddMinutes(5), "none"));
        using var bridge = new RealtimeIdentityBridge(channel);
        await bridge.StartAsync();
        channel.Response = new RealtimeIdentityResponse(true, "", "device-a", 5, DateTimeOffset.UtcNow.AddMinutes(5), "none");

        await bridge.RefreshAsync();

        bridge.Current.Should().BeNull();
    }

    [Fact]
    public async Task StopAsync_DrainsRefreshLoopAndClearsLease()
    {
        var channel = new FakeChannel(new RealtimeIdentityResponse(true, "header.payload.signature", "device-a", 4, DateTimeOffset.UtcNow.AddMinutes(5), "none"));
        using var bridge = new RealtimeIdentityBridge(channel);

        await bridge.StartAsync();
        await bridge.StopAsync();

        bridge.Current.Should().BeNull();
        bridge.LifecycleTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    private sealed class FakeChannel : IUIChannel
    {
        public FakeChannel(RealtimeIdentityResponse response) => this.Response = response;
        public RealtimeIdentityResponse Response { get; set; }
        public Task<TResponse?> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken ct = default)
            where TQuery : ControlParental.Domain.IUIMessage
            where TResponse : class, ControlParental.Domain.IUIMessage
        {
            if (query is GetRealtimeIdentity request && this.Response is RealtimeIdentityResponse response)
            {
                return Task.FromResult((response with
                {
                    ContractVersion = request.ContractVersion,
                    CorrelationId = request.CorrelationId,
                }) as TResponse);
            }

            return Task.FromResult(this.Response as TResponse);
        }
        public Task SendAsync<T>(T message, CancellationToken ct = default) where T : ControlParental.Domain.IUIMessage
            => Task.CompletedTask;
    }
}
