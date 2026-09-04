// <copyright file="ProductionBackendIdentityCompositionTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Net;
using System.Text;
using ControlParental.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

public sealed class ProductionBackendIdentityCompositionTests : IDisposable
{
    private readonly string tempPath = Path.Combine(Path.GetTempPath(), $"identity-composition-{Guid.NewGuid():N}");

    [Fact]
    public async Task ProductionComposition_RestoresDefinitiveGenerationBeforeHostedRemoteWorkStarts()
    {
        var store = this.CreateStore();
        var snapshot = new BackendIdentityCredentialSnapshot(
            17,
            "device-restored",
            "parent-restored",
            "access-restored",
            "refresh-restored",
            DateTimeOffset.UtcNow.AddHours(1));
        Assert.True((await store.WriteIdentityAsync(snapshot)).Success);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<ITimeProvider, ControlParental.Service.TimeProvider>();
        builder.Services.AddSingleton<ISecretStore>(store);
        Program.ConfigureBackendIdentityServices(
            builder.Services,
            new SupabaseConfig("https://example.supabase.co", "publishable-key"),
            new TlsPinningConfig(null),
            () => new HttpClient(new RejectingHandler()));

        using var host = builder.Build();
        await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));

        var coordinator = host.Services.GetRequiredService<IBackendIdentityCoordinator>();
        Assert.Equal(BackendIdentityPhase.DefinitiveSession, coordinator.CurrentState.Phase);
        Assert.Equal(17, coordinator.CurrentState.Generation);
        Assert.Equal("device-restored", coordinator.CurrentState.DeviceId);

        await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ProductionComposition_ExposesOneDefinitiveAuthorityAndNoLegacyBackendConstructor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITimeProvider, ControlParental.Service.TimeProvider>();
        services.AddSingleton<ISecretStore>(this.CreateStore());

        Program.ConfigureBackendIdentityServices(
            services,
            new SupabaseConfig("https://example.supabase.co", "publishable-key"),
            new TlsPinningConfig(null),
            () => new HttpClient(new RejectingHandler()));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var coordinator = provider.GetRequiredService<IBackendIdentityCoordinator>();
        using var scope = provider.CreateScope();

        Assert.Same(coordinator, provider.GetRequiredService<IBackendIdentityCoordinator>());
        Assert.IsType<BackendClient>(provider.GetRequiredService<IBackendClient>());
        Assert.IsType<PairingService>(scope.ServiceProvider.GetRequiredService<IPairingService>());
        Assert.DoesNotContain(
            typeof(BackendClient).GetConstructors(),
            constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(IDeviceAuthenticator)));
        Assert.DoesNotContain(typeof(IBackendClient).GetMethods(), method => method.Name == "PairAsync");
    }

    public void Dispose()
    {
        if (Directory.Exists(this.tempPath))
        {
            Directory.Delete(this.tempPath, recursive: true);
        }
    }

    private SecretStore CreateStore()
        => new(this.tempPath, new PassThroughProtector(), new NoOpAccessPolicy());

    private sealed class PassThroughProtector : ICredentialProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext.ToArray();

        public byte[] Unprotect(byte[] protectedData) => protectedData.ToArray();
    }

    private sealed class NoOpAccessPolicy : ICredentialFileAccessPolicy
    {
        public void Apply(string path)
        {
        }
    }

    private sealed class RejectingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
            });
    }
}
