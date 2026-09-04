// <copyright file="RealtimeIdentityBridgeRedTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ControlParental.App.UI;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Xunit;

/// <summary>
/// RED coverage for the Service-owned realtime identity bridge and its fences.
/// </summary>
public sealed class RealtimeIdentityBridgeRedTests
{
    private const string Issuer = "https://auth.control-parental.test";
    private const string Audience = "control-parental-windows";
    private static readonly RSA SigningKey = RSA.Create(2048);
    private static readonly DateTimeOffset FixedNow =
        new(2042, 06, 01, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_UsesCanonicalNonEmptyCorrelationOverTheUiChannel()
    {
        var transport = new RespondingTransport(request => ValidResponse(request.CorrelationId));
        using var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        using var bridge = new RealtimeIdentityBridge(
            channel,
            clock: () => FixedNow,
            delay: static (_, cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

        await bridge.StartAsync();

        var request = DeserializeRequest(transport.Writes.Single());
        Assert.True(Guid.TryParse(request.CorrelationId, out var correlationId));
        Assert.NotEqual(Guid.Empty, correlationId);
        Assert.NotNull(bridge.Current);
        Assert.Equal("device-a", bridge.Current!.DeviceId);
        await bridge.StopAsync();
    }

    [Fact]
    public async Task RefreshAsync_RejectsAResponseWithMismatchedCorrelation()
    {
        var response = ValidResponse("00000000-0000-4000-8000-000000000201");
        var transport = new RespondingTransport(_ => response);
        using var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        using var bridge = new RealtimeIdentityBridge(channel, clock: () => FixedNow);

        await bridge.RefreshAsync();

        Assert.Null(bridge.Current);
    }

    [Fact]
    public async Task RefreshAsync_RejectsAResponseWithUnsupportedContractVersion()
    {
        var transport = new RespondingTransport(request =>
            ValidResponse(request.CorrelationId) with { ContractVersion = 2 });
        using var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        using var bridge = new RealtimeIdentityBridge(channel, clock: () => FixedNow);

        await bridge.RefreshAsync();

        Assert.Null(bridge.Current);
    }

    [Fact]
    public async Task RefreshAsync_AfterServiceTimeout_ClearsTheLeaseWithoutEnvironmentFallback()
    {
        // Intentional green control: a null wire response reaches the bridge's
        // fail-closed Publish(sequence, null). The current production API has
        // no legacy realtime-identity environment/cache reader to sentinel;
        // this verifies the observable clear, while persistence remains an
        // unobservable seam until production exposes an audit sink.
        var transport = new RespondingTransport(request => ValidResponse(request.CorrelationId));
        using var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        using var bridge = new RealtimeIdentityBridge(channel, clock: () => FixedNow);

        await bridge.StartAsync();
        Assert.NotNull(bridge.Current);

        transport.ResponseFactory = static _ => null;
        await bridge.RefreshAsync();

        Assert.Null(bridge.Current);
    }

    [Fact]
    public async Task RefreshAsync_AfterServiceRevocationResponse_ClearsThePreviousLease()
    {
        // Intentional green control: IsValid rejects Success=false/revoked and
        // Publish clears the previous in-memory snapshot synchronously.
        var transport = new RespondingTransport(request => ValidResponse(request.CorrelationId));
        using var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        using var bridge = new RealtimeIdentityBridge(channel, clock: () => FixedNow);

        await bridge.StartAsync();
        Assert.NotNull(bridge.Current);

        transport.ResponseFactory = request => ValidResponse(request.CorrelationId) with
        {
            Success = false,
            AccessToken = null,
            DeviceId = null,
            Generation = 0,
            ErrorCode = "revoked",
        };
        await bridge.RefreshAsync();

        Assert.Null(bridge.Current);
    }

    [Theory]
    [InlineData(16_384, true)]
    [InlineData(16_385, false)]
    public async Task RefreshAsync_EnforcesTheUtf8AccessTokenBoundary(int tokenLength, bool expectedAccepted)
    {
        // Intentional green control: the bridge enforces its explicit 16,384
        // byte token bound before publishing a snapshot.
        var transport = new RespondingTransport(request =>
            ValidResponse(request.CorrelationId) with
            {
                AccessToken = TokenWithLength(tokenLength),
            });
        using var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        using var bridge = new RealtimeIdentityBridge(channel, clock: () => FixedNow);

        await bridge.RefreshAsync();

        Assert.Equal(expectedAccepted, bridge.Current is not null);
    }

    [Fact]
    public async Task RefreshLoop_RefreshesOnARevocationBoundIndependentOfLeaseExpiry()
    {
        var transport = new RespondingTransport(_ => ValidResponse(string.Empty));
        using var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        var delayStarted = NewSignal<TimeSpan>();
        using var bridge = new RealtimeIdentityBridge(
            channel,
            clock: () => FixedNow,
            delay: (interval, cancellationToken) =>
            {
                delayStarted.TrySetResult(interval);
                return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });

        await bridge.StartAsync();
        var firstInterval = await delayStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.InRange(firstInterval, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        await bridge.StopAsync();
    }

    [Fact]
    public async Task StopAsync_DrainsAnInFlightInitialQuery()
    {
        var transport = new BlockingReadTransport();
        using var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        using var bridge = new RealtimeIdentityBridge(channel, clock: () => FixedNow);
        using var startCancellation = new CancellationTokenSource();
        var start = bridge.StartAsync(startCancellation.Token);
        await transport.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await bridge.StopAsync();
            Assert.True(start.IsCompleted);
        }
        finally
        {
            startCancellation.Cancel();
            transport.ReleaseRead();
            try
            {
                await start;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Fact]
    public async Task RefreshAsync_AnOlderResponseCannotRepublishAfterANewGeneration()
    {
        // Intentional green control: refreshSequence prevents an older query
        // completion from publishing after a newer generation wins.
        var channel = new SequencedIdentityChannel();
        using var bridge = new RealtimeIdentityBridge(channel, clock: () => FixedNow);

        var oldRefresh = bridge.RefreshAsync();
        await channel.FirstQueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var newRefresh = bridge.RefreshAsync();
        await channel.SecondQueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        channel.CompleteSecond(CreateResponse("device-b", generation: 5));
        await newRefresh;
        channel.CompleteFirst(CreateResponse("device-a", generation: 4));
        await oldRefresh;

        Assert.NotNull(bridge.Current);
        Assert.Equal("device-b", bridge.Current!.DeviceId);
        Assert.Equal(5, bridge.Current.Generation);
    }

    [Fact]
    public async Task IdentityAcquiredAfterThirtyOneSeconds_RebuildsTheFailClosedSubscriber()
    {
        var authority = new MutableIdentityAuthority();
        var policy = new TestRealtimeChannel();
        var grants = new TestRealtimeChannel();
        var lifecycle = new TestLifecycleObserver();
        using var subscriber = new RealtimeSubscriber(
            policy,
            grants,
            lifecycle,
            deviceId: string.Empty,
            identityAuthority: authority);

        lifecycle.EnterForeground();
        await subscriber.LifecycleTask;
        Assert.False(subscriber.IsConnected);

        authority.Current = CreateIdentity("device-a", generation: 4);
        authority.RaiseChanged();
        await subscriber.LifecycleTask;

        Assert.True(subscriber.IsConnected);
        Assert.True(policy.IsSubscribed);
        Assert.True(grants.IsSubscribed);
    }

    [Fact]
    public async Task Composition_IdentityAcquiredAfterThirtyOneSeconds_ReplacesFailClosedChannels()
    {
        // The scheduling clock is independent from the JWT validation clock;
        // the latter must describe a token valid against the production clock.
        var jwtNow = DateTimeOffset.UtcNow;
        var clock = new MutableClock(jwtNow);
        var gate = NewSignal<bool>();
        var delayStarted = NewSignal<bool>();
        var delayCount = 0;
        var responseAvailable = false;
        var transport = new RespondingTransport(request =>
        {
            if (!responseAvailable)
            {
                return null;
            }

            return ValidResponse(request.CorrelationId) with
            {
                AccessToken = CreateSignedToken("device-a", clock.UtcNow, generation: 4),
                ExpiresAt = clock.UtcNow.AddMinutes(5),
            };
        });
        using var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        using var bridge = new RealtimeIdentityBridge(
            channel,
            clock: () => clock.UtcNow,
            delay: (interval, cancellationToken) =>
            {
                if (Interlocked.Increment(ref delayCount) == 1)
                {
                    delayStarted.TrySetResult(true);
                    return gate.Task.WaitAsync(cancellationToken);
                }

                return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        using var channels = SupabaseRealtimeComposition.Create(CreateConfiguration(), bridge);
        var identityChanged = NewSignal<bool>();
        bridge.Changed += (_, _) =>
        {
            if (bridge.Current is not null)
            {
                identityChanged.TrySetResult(true);
            }
        };

        await bridge.StartAsync();
        await delayStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Null(channels.DeviceId);
        Assert.IsType<FailClosedRealtimeChannel>(channels.Policy);
        Assert.IsType<FailClosedRealtimeChannel>(channels.Grants);
        clock.Advance(TimeSpan.FromSeconds(31));
        responseAvailable = true;
        gate.TrySetResult(true);
        await identityChanged.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await bridge.StopAsync();

        // RED seam: Create returns an immutable fail-closed set and does not
        // create/subscribe a generation-owned replacement when the authority
        // changes. No factory/dispose observer exists without production edits.
        Assert.Equal("device-a", channels.DeviceId);
        Assert.IsType<RealtimeChannelAdapter>(channels.Policy);
        Assert.IsType<RealtimeChannelAdapter>(channels.Grants);
    }

    [Fact]
    public async Task Composition_DeviceAndGenerationChangeReplacesTheOldTransportSet()
    {
        // Keep JWT claims valid for the real composition validator; advancing
        // this clock is only for the bridge's refresh scheduling.
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var deviceId = "device-a";
        var generation = 4L;
        var transport = new RespondingTransport(request =>
            ValidResponse(request.CorrelationId) with
            {
                AccessToken = CreateSignedToken(deviceId, clock.UtcNow, generation: generation),
                DeviceId = deviceId,
                Generation = generation,
                ExpiresAt = clock.UtcNow.AddMinutes(5),
            });
        using var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        using var bridge = new RealtimeIdentityBridge(channel, clock: () => clock.UtcNow);
        await bridge.RefreshAsync();
        using var channels = SupabaseRealtimeComposition.Create(CreateConfiguration(), bridge);
        Assert.Equal("device-a", channels.DeviceId);
        Assert.IsType<RealtimeChannelAdapter>(channels.Policy);
        Assert.IsType<RealtimeChannelAdapter>(channels.Grants);
        var oldPolicy = channels.Policy;
        var oldGrants = channels.Grants;

        deviceId = "device-b";
        generation = 5;
        await bridge.RefreshAsync();

        // RED seam: the current composition fences the old socket but exposes
        // no generation-owned factory that can atomically create/subscribe a
        // new set and dispose the old one.
        Assert.Equal("device-b", channels.DeviceId);
        Assert.NotSame(oldPolicy, channels.Policy);
        Assert.NotSame(oldGrants, channels.Grants);
    }

    [Fact]
    public async Task DeviceAndGenerationChange_DisposesTheOldRealtimeTransport()
    {
        var authority = new MutableIdentityAuthority
        {
            Current = CreateIdentity("device-a", generation: 4),
        };
        var policy = new TestRealtimeChannel();
        var grants = new TestRealtimeChannel();
        var lifecycle = new TestLifecycleObserver();
        using var subscriber = new RealtimeSubscriber(
            policy,
            grants,
            lifecycle,
            deviceId: "device-a",
            identityAuthority: authority);

        lifecycle.EnterForeground();
        await subscriber.LifecycleTask;
        Assert.True(subscriber.IsConnected);

        authority.Current = CreateIdentity("device-b", generation: 5);
        authority.RaiseChanged();
        await subscriber.LifecycleTask;

        Assert.True(policy.DisposeCount > 0);
        Assert.True(grants.DisposeCount > 0);
        Assert.False(subscriber.IsConnected);
    }

    [Fact]
    public void JwtValidationRejectsEveryInvalidAuthorityDimensionAndIsolatesDevices()
    {
        // Intentional green control: composition validates signature, issuer,
        // audience, time, device and generation before creating real channels.
        var now = DateTimeOffset.UtcNow;
        using var wrongSigningKey = RSA.Create(2048);
        var invalidTokens = new[]
        {
            CreateSignedToken("device-a", now, signingKey: wrongSigningKey),
            CreateSignedToken("device-a", now, algorithm: "HS256"),
            CreateSignedToken("device-a", now, issuer: "https://other.test"),
            CreateSignedToken("device-a", now, audience: "other-audience"),
            CreateSignedToken("device-a", now, notBefore: now.AddMinutes(10)),
            CreateSignedToken("device-a", now, expiresAt: now.AddMinutes(-10)),
            CreateSignedToken("device-b", now),
            CreateSignedToken("device-a", now, generation: 5),
        };

        foreach (var token in invalidTokens)
        {
            using var channels = SupabaseRealtimeComposition.Create(
                CreateConfiguration(),
                new FixedIdentityAuthority(new RealtimeIdentitySnapshot(token, "device-a", 4)
                {
                    ExpiresAt = now.AddMinutes(5),
                }));

            Assert.IsType<FailClosedRealtimeChannel>(channels.Policy);
            Assert.IsType<FailClosedRealtimeChannel>(channels.Grants);
        }

        using var deviceA = CreateSecureChannels("device-a", generation: 4, now);
        using var deviceB = CreateSecureChannels("device-b", generation: 9, now);
        Assert.NotEqual(deviceA.DeviceId, deviceB.DeviceId);
        Assert.NotEqual(((RealtimeChannelAdapter)deviceA.Policy).Topic, ((RealtimeChannelAdapter)deviceB.Policy).Topic);
    }

    [Fact]
    public async Task RealtimeDiagnostics_NeverExposeBearerTokensOrRawDeviceIds()
    {
        // Intentional green control: the bridge and subscriber lifecycle paths
        // emit no credential/device diagnostic payload to Trace.
        const string deviceId = "device-sensitive";
        const string token = "header.sensitive-bearer.signature";
        var listener = new RecordingTraceListener();
        Trace.Listeners.Add(listener);

        var transport = new RespondingTransport(_ => new RealtimeIdentityResponse(
            true,
            token,
            deviceId,
            4,
            FixedNow.AddMinutes(5),
            "none")
        {
            ContractVersion = 1,
            CorrelationId = "00000000-0000-4000-8000-000000000501",
        });
        using var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        using var bridge = new RealtimeIdentityBridge(channel, clock: () => FixedNow);
        var authority = new MutableIdentityAuthority
        {
            Current = new RealtimeIdentitySnapshot(token, deviceId, 4),
        };
        var policy = new TestRealtimeChannel();
        var grants = new TestRealtimeChannel();
        var lifecycle = new TestLifecycleObserver();
        using var subscriber = new RealtimeSubscriber(
            policy,
            grants,
            lifecycle,
            deviceId,
            identityAuthority: authority);

        try
        {
            await bridge.StartAsync();
            transport.ResponseFactory = static _ => null;
            await bridge.RefreshAsync();
            await bridge.StopAsync();

            lifecycle.EnterForeground();
            await subscriber.LifecycleTask;
            authority.Current = null;
            authority.RaiseChanged();
            await subscriber.LifecycleTask;
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }

        Assert.DoesNotContain(token, listener.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(deviceId, listener.Text, StringComparison.Ordinal);
    }

    private static SupabaseRealtimeChannels CreateSecureChannels(
        string deviceId,
        long generation,
        DateTimeOffset now)
    {
        var token = CreateSignedToken(deviceId, now, generation: generation, expiresAt: now.AddMinutes(10));
        return SupabaseRealtimeComposition.Create(
            CreateConfiguration(),
            new FixedIdentityAuthority(new RealtimeIdentitySnapshot(token, deviceId, generation)
            {
                ExpiresAt = now.AddMinutes(10),
            }));
    }

    private static SupabaseRealtimeConfiguration CreateConfiguration()
        => new(
            "https://example.supabase.co",
            "sb_publishable_test",
            Issuer,
            Audience,
            SigningKey.ExportSubjectPublicKeyInfoPem(),
            Convert.ToBase64String(new byte[32]));

    private static RealtimeIdentitySnapshot CreateIdentity(string deviceId, long generation)
        => new("header.payload.signature", deviceId, generation)
        {
            ExpiresAt = FixedNow.AddMinutes(5),
        };

    private static RealtimeIdentityResponse ValidResponse(string correlationId)
        => new(
            true,
            "header.payload.signature",
            "device-a",
            4,
            FixedNow.AddMinutes(5),
            "none")
        {
            ContractVersion = 1,
            CorrelationId = correlationId,
        };

    private static RealtimeIdentityResponse CreateResponse(string deviceId, long generation)
        => ValidResponse("00000000-0000-4000-8000-000000000999") with
        {
            DeviceId = deviceId,
            Generation = generation,
        };

    private static string TokenWithLength(int tokenLength)
    {
        const string prefix = "header.";
        const string suffix = ".signature";
        Assert.True(tokenLength > prefix.Length + suffix.Length);
        return prefix
            + new string('x', tokenLength - prefix.Length - suffix.Length)
            + suffix;
    }

    private static GetRealtimeIdentity DeserializeRequest(byte[] bytes)
        => JsonSerializer.Deserialize(
            bytes,
            ControlParental.Domain.UIMessagesJsonContext.Default.GetRealtimeIdentity)!;

    private static string CreateSignedToken(
        string claimDeviceId,
        DateTimeOffset now,
        string issuer = Issuer,
        string audience = Audience,
        long generation = 4,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? notBefore = null,
        string algorithm = "RS256",
        RSA? signingKey = null)
    {
        var header = Base64Url(new { alg = algorithm, typ = "JWT" });
        var payload = Base64Url(new Dictionary<string, object?>
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["device_id"] = claimDeviceId,
            ["generation"] = generation,
            ["exp"] = (expiresAt ?? now.AddMinutes(10)).ToUnixTimeSeconds(),
            ["nbf"] = (notBefore ?? now.AddMinutes(-1)).ToUnixTimeSeconds(),
        });
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{payload}");
        var signature = (signingKey ?? SigningKey).SignData(
            signingInput,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{header}.{payload}.{Base64Url(signature)}";
    }

    private static string Base64Url(object value)
        => Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static TaskCompletionSource<T> NewSignal<T>()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class RespondingTransport(
        Func<GetRealtimeIdentity, RealtimeIdentityResponse?> responseFactory) : IUITransport
    {
        public List<byte[]> Writes { get; } = new();
        public Func<GetRealtimeIdentity, RealtimeIdentityResponse?> ResponseFactory { get; set; } = responseFactory;

        public Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task WriteAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            this.Writes.Add(buffer.ToArray());
            return Task.CompletedTask;
        }

        public Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            var request = DeserializeRequest(this.Writes[^1]);
            var response = this.ResponseFactory(request);
            if (response is null)
            {
                return Task.FromResult(0);
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(
                response,
                ControlParental.Domain.UIMessagesJsonContext.Default.RealtimeIdentityResponse);
            Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);
            return Task.FromResult(bytes.Length);
        }

        public void Dispose()
        {
        }
    }

    private sealed class BlockingReadTransport : IUITransport
    {
        private readonly TaskCompletionSource<bool> release = NewSignal<bool>();
        public TaskCompletionSource<bool> ReadStarted { get; } = NewSignal<bool>();

        public Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task WriteAsync(byte[] buffer, CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            this.ReadStarted.TrySetResult(true);
            await this.release.Task.WaitAsync(cancellationToken);
            return 0;
        }

        public void ReleaseRead() => this.release.TrySetResult(true);

        public void Dispose()
        {
        }
    }

    private sealed class SequencedIdentityChannel : IUIChannel
    {
        public TaskCompletionSource<bool> FirstQueryStarted { get; } = NewSignal<bool>();
        public TaskCompletionSource<bool> SecondQueryStarted { get; } = NewSignal<bool>();
        private readonly TaskCompletionSource<RealtimeIdentityResponse?> firstResponse = NewSignal<RealtimeIdentityResponse?>();
        private readonly TaskCompletionSource<RealtimeIdentityResponse?> secondResponse = NewSignal<RealtimeIdentityResponse?>();
        private int queryCount;

        public async Task<TResponse?> QueryAsync<TRequest, TResponse>(
            TRequest request,
            CancellationToken ct = default)
            where TRequest : ControlParental.Domain.IUIMessage
            where TResponse : class, ControlParental.Domain.IUIMessage
        {
            var query = Interlocked.Increment(ref this.queryCount);
            if (query == 1)
            {
                this.FirstQueryStarted.TrySetResult(true);
                return (TResponse?)(object?)await this.firstResponse.Task
                    .WaitAsync(ct)
                    .ConfigureAwait(false);
            }

            this.SecondQueryStarted.TrySetResult(true);
            return (TResponse?)(object?)await this.secondResponse.Task
                .WaitAsync(ct)
                .ConfigureAwait(false);
        }

        public Task SendAsync<TMessage>(TMessage message, CancellationToken ct = default)
            where TMessage : ControlParental.Domain.IUIMessage
            => Task.CompletedTask;

        public void CompleteFirst(RealtimeIdentityResponse response)
            => this.firstResponse.TrySetResult(response);

        public void CompleteSecond(RealtimeIdentityResponse response)
            => this.secondResponse.TrySetResult(response);
    }

    private sealed class MutableClock(DateTimeOffset initial)
    {
        public DateTimeOffset UtcNow { get; private set; } = initial;

        public void Advance(TimeSpan amount) => this.UtcNow += amount;
    }

    private sealed class FixedIdentityAuthority(RealtimeIdentitySnapshot identity) : IRealtimeIdentityAuthority
    {
        public RealtimeIdentitySnapshot? Current { get; } = identity;
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }

    private sealed class MutableIdentityAuthority : IRealtimeIdentityAuthority
    {
        public RealtimeIdentitySnapshot? Current { get; set; }
        public event EventHandler? Changed;
        public void RaiseChanged() => this.Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class TestLifecycleObserver : IWindowLifecycleObserver
    {
        public bool IsInForeground { get; private set; }
        public event EventHandler? EnteredForeground;
        public event EventHandler? EnteredBackground;

        public void EnterForeground()
        {
            this.IsInForeground = true;
            this.EnteredForeground?.Invoke(this, EventArgs.Empty);
        }

        public void EnterBackground()
        {
            this.IsInForeground = false;
            this.EnteredBackground?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class TestRealtimeChannel : IRealtimeChannel
    {
        public bool IsSubscribed { get; private set; }
        public int DisposeCount { get; private set; }
        public event EventHandler<Broadcast>? BroadcastReceived;

        public Task SubscribeAsync()
        {
            this.IsSubscribed = true;
            return Task.CompletedTask;
        }

        public void Unsubscribe() => this.IsSubscribed = false;

        public void Dispose()
        {
            this.DisposeCount++;
            this.IsSubscribed = false;
            this.BroadcastReceived = null;
        }
    }

    private sealed class RecordingTraceListener : TraceListener
    {
        private readonly StringBuilder text = new();
        public string Text => this.text.ToString();
        public override void Write(string? message) => this.text.Append(message);
        public override void WriteLine(string? message) => this.text.AppendLine(message);
    }
}
