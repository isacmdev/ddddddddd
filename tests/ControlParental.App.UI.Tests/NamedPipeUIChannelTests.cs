namespace ControlParental.App.UI.Tests;

using System.Text;
using System.Text.Json;
using System.IO.Pipes;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Xunit;
using AppEnforcementLevelResponse = ControlParental.App.UI.EnforcementLevelResponse;
using AppEnforcementLevelCheck = ControlParental.App.UI.EnforcementLevelCheck;

public sealed class NamedPipeUIChannelTests
{
    [Fact]
    public async Task QueryAsync_UsesDomainContextForWnsRequestAndResponse()
    {
        var response = new WnsRegistrationResult("op", WnsRegistrationStatus.Accepted, "corr");
        var transport = new RecordingTransport(JsonSerializer.SerializeToUtf8Bytes(
            response, ControlParental.Domain.UIMessagesJsonContext.Default.WnsRegistrationResult));
        var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));

        var result = await channel.QueryAsync<RegisterWnsChannel, WnsRegistrationResult>(
            new("op", "https://db5.notify.windows.com/?token=x", "wns", DateTimeOffset.UtcNow.AddDays(1)));

        Assert.NotNull(result);
        Assert.Equal(response.OperationId, result!.OperationId);
        Assert.Equal(response.Status, result.Status);
        Assert.Equal(response.CorrelationId, result.CorrelationId);
        Assert.Contains("RegisterWnsChannel", Encoding.UTF8.GetString(transport.Writes.Single()));
        Assert.Equal(1, transport.ConnectCount);
        Assert.Equal(1, transport.WriteCount);
        Assert.Equal(1, transport.ReadCount);
    }

    [Fact]
    public async Task QueryAsync_UsesLegacyContextForExistingUiMessages()
    {
        var response = new AppEnforcementLevelResponse(
            "Standard", Array.Empty<AppEnforcementLevelCheck>());
        var transport = new RecordingTransport(JsonSerializer.SerializeToUtf8Bytes(
            response,
            ControlParental.App.UI.Interop.UIMessagesJsonContext.Default.GetTypeInfo(
                typeof(AppEnforcementLevelResponse))!));
        var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));

        var result = await channel.QueryAsync<
            ControlParental.App.UI.GetEnforcementLevel,
            ControlParental.App.UI.EnforcementLevelResponse>(new());

        Assert.NotNull(result);
        Assert.Equal(response.Level, result!.Level);
        Assert.Equal(response.MessageType, result.MessageType);
        Assert.Equal(response.Checks.Count, result.Checks.Count);
        Assert.True(response.Checks.Zip(result.Checks).All(pair =>
            pair.First.CheckName == pair.Second.CheckName
            && pair.First.IsPassing == pair.Second.IsPassing
            && pair.First.Details == pair.Second.Details));
        Assert.Contains("GetEnforcementLevel", Encoding.UTF8.GetString(transport.Writes.Single()));
    }

    [Fact]
    public async Task SendAsync_WritesDomainMessageOnce()
    {
        var transport = new RecordingTransport(Array.Empty<byte>());
        var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));

        await channel.SendAsync(new RegisterWnsChannel(
            "op", "https://db5.notify.windows.com/?token=x", "wns", DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(1, transport.ConnectCount);
        Assert.Equal(1, transport.WriteCount);
        Assert.Equal(0, transport.ReadCount);
        Assert.Contains("RegisterWnsChannel", Encoding.UTF8.GetString(transport.Writes.Single()));
    }

    [Fact]
    public async Task SendAsync_WritesLegacyMessageOnce()
    {
        var transport = new RecordingTransport(Array.Empty<byte>());
        var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));

        await channel.SendAsync(new ControlParental.App.UI.GetEnforcementLevel());

        Assert.Equal(1, transport.ConnectCount);
        Assert.Equal(1, transport.WriteCount);
        Assert.Equal(0, transport.ReadCount);
        Assert.Contains("GetEnforcementLevel", Encoding.UTF8.GetString(transport.Writes.Single()));
    }

    [Fact]
    public async Task QueryAsync_TransportIOExceptionFailsClosed()
    {
        var transport = new RecordingTransport(Array.Empty<byte>())
        {
            ConnectException = new IOException("pipe unavailable"),
        };
        var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));

        var result = await channel.QueryAsync<RegisterWnsChannel, WnsRegistrationResult>(
            new("op", "https://db5.notify.windows.com/?token=x", "wns", DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Null(result);
        Assert.Equal(1, transport.ConnectCount);
        Assert.Empty(transport.Writes);
        Assert.Equal(0, transport.ReadCount);
    }

    [Fact]
    public async Task QueryAsync_UsesRealNamedPipeTransportEndToEnd()
    {
        var pipeName = $"ControlParental.Tests.{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var serverCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var expected = new WnsRegistrationResult("op", WnsRegistrationStatus.Accepted, "corr");

        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(serverCts.Token).ConfigureAwait(false);
            var requestBuffer = new byte[65536];
            var bytesRead = await server.ReadAsync(requestBuffer, serverCts.Token).ConfigureAwait(false);
            var request = JsonSerializer.Deserialize(
                requestBuffer.AsSpan(0, bytesRead),
                ControlParental.Domain.UIMessagesJsonContext.Default.RegisterWnsChannel);

            Assert.NotNull(request);
            Assert.Equal("op", request!.OperationId);
            Assert.Equal("wns", request.Channel);

            var response = JsonSerializer.SerializeToUtf8Bytes(
                expected,
                ControlParental.Domain.UIMessagesJsonContext.Default.WnsRegistrationResult);
            await server.WriteAsync(response, serverCts.Token).ConfigureAwait(false);
            await server.FlushAsync(serverCts.Token).ConfigureAwait(false);
        });

        using var channel = new NamedPipeUIChannel(pipeName, TimeSpan.FromSeconds(1));
        var result = await channel.QueryAsync<RegisterWnsChannel, WnsRegistrationResult>(
            new("op", "https://db5.notify.windows.com/?token=x", "wns", DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(expected, result);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
    }

    [Fact]
    public void LegacyContextRegistersActiveAppUiMessages()
    {
        var activeTypes = new[]
        {
            typeof(ControlParental.App.UI.GetEnforcementLevel),
            typeof(ControlParental.App.UI.EnforcementLevelResponse),
            typeof(ControlParental.App.UI.GetOnboardingState),
            typeof(ControlParental.App.UI.OnboardingStateResponse),
            typeof(ControlParental.App.UI.RecordOnboardingStepCompleted),
            typeof(ControlParental.App.UI.StepCompletedResponse),
            typeof(ControlParental.App.UI.RecordFunnelEvent),
            typeof(ControlParental.App.UI.ShowOverlayCommand),
            typeof(ControlParental.App.UI.HideOverlayCommand),
            typeof(ControlParental.App.UI.PairDevice),
            typeof(ControlParental.App.UI.PairDeviceResponse),
            typeof(ControlParental.App.UI.ListAccounts),
            typeof(ControlParental.App.UI.AccountList),
            typeof(ControlParental.App.UI.CreateAccount),
            typeof(ControlParental.App.UI.CreateAccountResponse),
            typeof(ControlParental.App.UI.ConvertAccount),
            typeof(ControlParental.App.UI.ConvertAccountResponse),
            typeof(ControlParental.App.UI.GetServiceStatus),
            typeof(ControlParental.App.UI.ServiceStatusResponse),
            typeof(ControlParental.App.UI.GetConsentStatus),
            typeof(ControlParental.App.UI.ConsentStatusSnapshot),
            typeof(ControlParental.App.UI.GrantConsent),
            typeof(ControlParental.App.UI.AdvanceOnboardingStep),
            typeof(ControlParental.App.UI.ResetOnboardingState),
        };

        Assert.All(activeTypes, type =>
            Assert.NotNull(ControlParental.App.UI.Interop.UIMessagesJsonContext.Default.GetTypeInfo(type)));
    }

    [Fact]
    public async Task QueryAsync_EmptyOrMalformedResponseFailsClosed()
    {
        foreach (var payload in new[] { Array.Empty<byte>(), Encoding.UTF8.GetBytes("{bad") })
        {
            var transport = new RecordingTransport(payload);
            var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));

            var result = await channel.QueryAsync<RegisterWnsChannel, WnsRegistrationResult>(
                new("op", "https://db5.notify.windows.com/?token=x", "wns", DateTimeOffset.UtcNow.AddDays(1)));

            Assert.Null(result);
        }
    }

    [Fact]
    public async Task QueryAsync_PropagatesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        var transport = new RecordingTransport(Array.Empty<byte>());
        var channel = new NamedPipeUIChannel(() => transport, TimeSpan.FromSeconds(1));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => channel.QueryAsync<
            RegisterWnsChannel, WnsRegistrationResult>(
            new("op", "https://db5.notify.windows.com/?token=x", "wns", DateTimeOffset.UtcNow.AddDays(1)), cts.Token));
        Assert.True(cts.IsCancellationRequested);
        Assert.True(transport.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task QueryAsync_InternalTimeoutFailsClosed()
    {
        var transport = new RecordingTransport(Array.Empty<byte>()) { ConnectException = new TaskCanceledException() };
        var channel = new NamedPipeUIChannel(() => transport, TimeSpan.Zero);

        var result = await channel.QueryAsync<RegisterWnsChannel, WnsRegistrationResult>(
            new("op", "https://db5.notify.windows.com/?token=x", "wns", DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Null(result);
    }

    private sealed class RecordingTransport(byte[] response) : IUITransport
    {
        public int ConnectCount { get; private set; }
        public int WriteCount { get; private set; }
        public int ReadCount { get; private set; }
        public List<byte[]> Writes { get; } = new();
        public CancellationToken CancellationToken { get; private set; }
        public TimeSpan Timeout { get; private set; }
        public Exception? ConnectException { get; init; }

        public Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            this.ConnectCount++;
            this.Timeout = timeout;
            this.CancellationToken = cancellationToken;
            return this.ConnectException is null
                ? Task.CompletedTask
                : Task.FromException(this.ConnectException);
        }

        public Task WriteAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            this.WriteCount++;
            this.CancellationToken = cancellationToken;
            this.Writes.Add(buffer);
            return cancellationToken.IsCancellationRequested
                ? Task.FromCanceled(cancellationToken)
                : Task.CompletedTask;
        }

        public Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            this.ReadCount++;
            this.CancellationToken = cancellationToken;
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<int>(cancellationToken);
            }

            response.CopyTo(buffer, 0);
            return Task.FromResult(response.Length);
        }

        public void Dispose()
        {
        }
    }
}
