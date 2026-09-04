// <copyright file="RealtimeCompositionTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using FluentAssertions;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;

public sealed class RealtimeCompositionTests
{
    private const string TestIssuer = "https://auth.control-parental.test";
    private const string TestAudience = "control-parental-windows";
    private static readonly RSA TestSigningKey = RSA.Create(2048);
    private static readonly string TestSigningKeyPem = TestSigningKey.ExportSubjectPublicKeyInfoPem();
    private static readonly string TestCertificatePin = Convert.ToBase64String(new byte[32]);

    [Fact]
    public void Create_UsesDeviceScopedTopicsAndAuthenticatedClient()
    {
        using var channels = CreateSecureChannels("device-123");

        channels.Policy.Should().BeOfType<RealtimeChannelAdapter>();
        channels.Grants.Should().BeOfType<RealtimeChannelAdapter>();
        channels.DeviceId.Should().Be("device-123");
        ((RealtimeChannelAdapter)channels.Policy).Topic.Should().Be("device:device-123:policy");
        ((RealtimeChannelAdapter)channels.Grants).Topic.Should().Be("device:device-123:grants");
    }

    [Fact]
    public void Create_RejectsMissingIdentityByFailingClosed()
    {
        using var channels = SupabaseRealtimeComposition.Create(
            CreateConfiguration(),
            new TestIdentityAuthority(null));

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Fact]
    public void Create_RejectsPlainHttpByFailingClosed()
    {
        using var channels = SupabaseRealtimeComposition.Create(
            CreateConfiguration("http://example.supabase.co"),
            new TestIdentityAuthority(CreateIdentity("device-123")));

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Fact]
    public void Create_DerivesDeviceTopicOnlyFromBackendIssuedTokenClaim()
    {
        using var channels = CreateSecureChannels("device-from-token");

        channels.DeviceId.Should().Be("device-from-token");
        ((RealtimeChannelAdapter)channels.Policy).Topic.Should().Be("device:device-from-token:policy");
    }

    [Fact]
    public void Create_RejectsAccessTokenWithoutExactDeviceClaim()
    {
        using var channels = SupabaseRealtimeComposition.Create(
            CreateConfiguration(),
            new TestIdentityAuthority(new RealtimeIdentitySnapshot(
                CreateAccessTokenWithoutDeviceClaim(),
                "device-123",
                7)));

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Fact]
    public void Create_RejectsUnsignedAccessToken()
    {
        using var channels = SupabaseRealtimeComposition.Create(
            CreateConfiguration(),
            new TestIdentityAuthority(new RealtimeIdentitySnapshot(
                CreateAccessToken("device-123", algorithm: "none"),
                "device-123",
                7)));

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Fact]
    public void Create_RejectsAccessTokenWithInvalidSignature()
    {
        using var channels = SupabaseRealtimeComposition.Create(
            CreateConfiguration(),
            new TestIdentityAuthority(new RealtimeIdentitySnapshot(
                CreateAccessToken("device-123"),
                "device-123",
                7)));

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Fact]
    public void Create_RejectsExpiredAccessToken()
    {
        using var channels = CreateSecureChannels(
            "device-123",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-6));

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Fact]
    public void Create_AllowsAccessTokenWithinFiveMinuteClockSkew()
    {
        using var channels = CreateSecureChannels(
            "device-123",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        channels.Policy.Should().BeOfType<RealtimeChannelAdapter>();
        channels.Grants.Should().BeOfType<RealtimeChannelAdapter>();
    }

    [Theory]
    [InlineData("https://other-issuer.test", TestAudience)]
    [InlineData(TestIssuer, "other-audience")]
    public void Create_RejectsTokenWithWrongIssuerOrAudience(string issuer, string audience)
    {
        var token = CreateSignedAccessToken("device-123", issuer: issuer, audience: audience);
        using var channels = SupabaseRealtimeComposition.Create(
            CreateConfiguration(),
            new TestIdentityAuthority(new RealtimeIdentitySnapshot(token, "device-123", 7)));

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Fact]
    public void Create_RejectsTokenFromDifferentIdentityGeneration()
    {
        var token = CreateSignedAccessToken("device-123", generation: 8);
        using var channels = SupabaseRealtimeComposition.Create(
            CreateConfiguration(),
            new TestIdentityAuthority(new RealtimeIdentitySnapshot(token, "device-123", 7)));

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Fact]
    public void Create_RejectsTokenWithoutNotBeforeClaim()
    {
        var token = CreateSignedAccessToken("device-123", includeNotBefore: false);
        using var channels = SupabaseRealtimeComposition.Create(
            CreateConfiguration(),
            new TestIdentityAuthority(new RealtimeIdentitySnapshot(token, "device-123", 7)));

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Fact]
    public void Create_RequiresPinnedRealtimeTransport()
    {
        var configuration = CreateConfiguration() with { CertificatePins = string.Empty };
        using var channels = SupabaseRealtimeComposition.Create(
            configuration,
            new TestIdentityAuthority(CreateIdentity("device-123")));

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Theory]
    [InlineData("publishable-key")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.legacy-anon")]
    public void Create_RejectsNonPublishableSupabaseKeys(string key)
    {
        using var channels = SupabaseRealtimeComposition.Create(
            CreateConfiguration() with { PublishableKey = key },
            new TestIdentityAuthority(CreateIdentity("device-123")));

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Fact]
    public void Create_RequiresServiceOwnedIdentityAuthority()
    {
        using var channels = SupabaseRealtimeComposition.Create(CreateConfiguration());

        channels.Policy.Should().BeOfType<FailClosedRealtimeChannel>();
        channels.Grants.Should().BeOfType<FailClosedRealtimeChannel>();
    }

    [Fact]
    public void PinnedRealtimeTransportSecurity_RejectsMissingPins()
    {
        var act = () => new PinnedRealtimeTransportSecurity(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task FailClosedChannels_NeverReportAProductiveConnection()
    {
        using var channels = SupabaseRealtimeComposition.Create(
            CreateConfiguration(),
            new TestIdentityAuthority(null));
        var lifecycle = new FakeWindowLifecycleObserver();
        using var subscriber = new RealtimeSubscriber(
            channels.Policy,
            channels.Grants,
            lifecycle,
            string.Empty);

        lifecycle.SimulateEnterForeground();
        await subscriber.LifecycleTask;

        subscriber.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void AppComposition_ResolvesOneWindowLifecycleObserverForInterfaceAndLifetime()
    {
        var services = new ServiceCollection();
        var configure = typeof(App).GetMethod("ConfigureServices", BindingFlags.NonPublic | BindingFlags.Static);
        configure.Should().NotBeNull();
        configure!.Invoke(null, new object[] { services });

        using var provider = services.BuildServiceProvider();
        var concrete = provider.GetRequiredService<WindowLifecycleObserver>();
        var contract = provider.GetRequiredService<Domain.IWindowLifecycleObserver>();

        contract.Should().BeSameAs(concrete);
    }

    [Fact]
    public async Task AppComposition_ConnectsHintToAuthenticatedTriggerSync()
    {
        var policy = new RecordingRealtimeChannel();
        var grants = new RecordingRealtimeChannel();
        var ui = new RecordingUIChannel();
        var services = new ServiceCollection();
        var configure = typeof(App).GetMethod("ConfigureServices", BindingFlags.NonPublic | BindingFlags.Static);
        configure.Should().NotBeNull();
        configure!.Invoke(null, new object[] { services });
        services.AddSingleton<IRealtimeIdentityAuthority>(
            new TestIdentityAuthority(new RealtimeIdentitySnapshot("header.payload.signature", "app-device", 1)));
        services.AddSingleton(new SupabaseRealtimeChannels(policy, grants, "app-device", null));
        services.AddSingleton<IUIChannel>(ui);

        using var provider = services.BuildServiceProvider();
        var subscriber = provider.GetRequiredService<Domain.IRealtimeSubscriber>();
        var concreteSubscriber = (RealtimeSubscriber)subscriber;
        var lifecycle = provider.GetRequiredService<WindowLifecycleObserver>();
        lifecycle.EnterForeground();
        await concreteSubscriber.LifecycleTask;

        policy.FireBroadcast(new Dictionary<string, object?>
        {
            ["contract"] = "control-parental.windows",
            ["version"] = 1,
            ["message_type"] = "realtime.hint",
            ["correlation_id"] = "00000000-0000-4000-8000-000000000030",
            ["payload"] = new Dictionary<string, object?> { ["hint_type"] = "sync" },
        });

        for (var attempt = 0; attempt < 100 && ui.TriggerSyncCount == 0; attempt++)
        {
            await Task.Delay(10);
        }

        ui.TriggerSyncCount.Should().Be(1);
        subscriber.Dispose();
        await concreteSubscriber.LifecycleTask;
    }

    private sealed class RecordingRealtimeChannel : IRealtimeChannel
    {
        private EventHandler<Broadcast>? handlers;

        public bool IsSubscribed { get; private set; }

        public event EventHandler<Broadcast>? BroadcastReceived
        {
            add => this.handlers += value;
            remove => this.handlers -= value;
        }

        public Task SubscribeAsync()
        {
            this.IsSubscribed = true;
            return Task.CompletedTask;
        }

        public void Unsubscribe()
        {
            this.IsSubscribed = false;
        }

        public void Dispose()
        {
            this.handlers = null;
        }

        public void FireBroadcast(Dictionary<string, object?> payload) =>
            this.handlers?.Invoke(this, new Broadcast(payload));
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

    private static SupabaseRealtimeChannels CreateSecureChannels(
        string deviceId,
        DateTimeOffset? expiresAt = null)
    {
        var identity = CreateIdentity(deviceId, expiresAt: expiresAt);
        return SupabaseRealtimeComposition.Create(CreateConfiguration(), new TestIdentityAuthority(identity));
    }

    private static SupabaseRealtimeConfiguration CreateConfiguration(string? url = null)
        => new(
            url ?? "https://example.supabase.co",
            "sb_publishable_test",
            TestIssuer,
            TestAudience,
            TestSigningKeyPem,
            TestCertificatePin);

    private static RealtimeIdentitySnapshot CreateIdentity(
        string deviceId,
        long generation = 7,
        DateTimeOffset? expiresAt = null)
        => new(
            CreateSignedAccessToken(deviceId, generation: generation, expiresAt: expiresAt),
            deviceId,
            generation);

    private static string CreateSignedAccessToken(
        string deviceId,
        string issuer = TestIssuer,
        string audience = TestAudience,
        long generation = 7,
        DateTimeOffset? expiresAt = null,
        bool includeNotBefore = true)
    {
        var header = Base64Url(new { alg = "RS256", typ = "JWT" });
        var claims = new Dictionary<string, object?>
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["device_id"] = deviceId,
            ["generation"] = generation,
            ["exp"] = (expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(10)).ToUnixTimeSeconds(),
        };
        if (includeNotBefore)
        {
            claims["nbf"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        }

        var payload = Base64Url(claims);
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{payload}");
        var signature = TestSigningKey.SignData(
            signingInput,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{header}.{payload}.{Base64Url(signature)}";
    }

    private static string CreateAccessToken(
        string deviceId,
        string algorithm = "HS256",
        DateTimeOffset? expiresAt = null)
    {
        var header = Base64Url(new { alg = algorithm, typ = "JWT" });
        var payload = Base64Url(new { device_id = deviceId, exp = (expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(10)).ToUnixTimeSeconds() });
        return $"{header}.{payload}.c2lnbmF0dXJl";
    }

    private static string CreateAccessTokenWithoutDeviceClaim()
    {
        var header = Base64Url(new { alg = "HS256", typ = "JWT" });
        var payload = Base64Url(new { sub = "anonymous", exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() });
        return $"{header}.{payload}.c2lnbmF0dXJl";
    }

    private static string Base64Url(object value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class TestIdentityAuthority : IRealtimeIdentityAuthority
    {
        public TestIdentityAuthority(RealtimeIdentitySnapshot? current)
        {
            this.Current = current;
        }

        public RealtimeIdentitySnapshot? Current { get; }

        public event EventHandler? Changed;

        public void RaiseChanged() => this.Changed?.Invoke(this, EventArgs.Empty);
    }
}
