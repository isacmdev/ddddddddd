// <copyright file="StatusViewModelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

#pragma warning disable SA1636

// <copyright file="StatusViewModelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>
namespace ControlParental.App.UI.Tests;

using ControlParental.Domain;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name
#pragma warning disable CA1707, SA1600, SA1636, SA1201, CS1591, CS0067

/// <summary>
/// T27 — Unit tests for StatusViewModel.
/// </summary>
public sealed class StatusViewModelTests : IDisposable
{
    private readonly FakeUIPipeClient pipeClient;
    private readonly FakeRealtimeSubscriber realtimeSubscriber;
    private readonly StatusViewModel viewModel;

    public StatusViewModelTests()
    {
        this.pipeClient = new FakeUIPipeClient();
        this.realtimeSubscriber = new FakeRealtimeSubscriber();
        this.viewModel = new StatusViewModel(this.pipeClient, this.realtimeSubscriber);
    }

    public void Dispose()
    {
        this.viewModel.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_PopulatesStateFromPipe()
    {
        // Arrange
        var state = new UsageStateResponse(
            MinutesRemaining: 45,
            CurrentAppId: "chrome.exe",
            IsPaused: false,
            ActiveGrants: Array.Empty<GrantInfo>(),
            CurrentLevel: EnforcementLevel.Standard,
            ActiveIssues: Array.Empty<ActiveIssue>());
        this.pipeClient.SetState(state);

        // Act
        await this.viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert
        this.viewModel.MinutesRemaining.Should().Be(45);
        this.viewModel.CurrentAppId.Should().Be("chrome.exe");
        this.viewModel.IsPaused.Should().BeFalse();
        this.viewModel.CurrentLevel.Should().Be(EnforcementLevel.Standard);
        this.viewModel.HasActiveIssues.Should().BeFalse();
        this.viewModel.StatusSummary.Should().Be("Te quedan 45 minutos");
    }

    [Fact]
    public async Task InitializeAsync_WhenNoIssues_HasActiveIssuesIsFalse()
    {
        // Arrange
        this.pipeClient.SetState(NewState(activeIssues: Array.Empty<ActiveIssue>()));

        // Act
        await this.viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert
        this.viewModel.HasActiveIssues.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenIssues_HasActiveIssuesIsTrue()
    {
        // Arrange
        this.pipeClient.SetState(NewState(
            activeIssues: new[]
            {
                new ActiveIssue(
                    Type: EnforcementIssueType.ServiceNotRunning,
                    Severity: EnforcementIssueSeverity.Critical,
                    Description: "Service is not running"),
            }));

        // Act
        await this.viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert
        this.viewModel.HasActiveIssues.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenNoGrants_ActiveGrantsIsEmpty()
    {
        // Arrange
        this.pipeClient.SetState(NewState(activeGrants: Array.Empty<GrantInfo>()));

        // Act
        await this.viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert — observable behavior: the ActiveGrants collection is empty.
        // (StatusViewModel no longer exposes a derived HasNoGrants boolean —
        // callers inspect the collection directly.)
        this.viewModel.ActiveGrants.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_WhenGrants_GrantsAreListed()
    {
        // Arrange
        var grants = new[]
        {
            new GrantInfo(
                Scope: "device",
                MinutesRemaining: 30,
                Source: GrantSource.ExtraTime,
                ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(30)),
        };
        this.pipeClient.SetState(NewState(activeGrants: grants));

        // Act
        await this.viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert
        this.viewModel.ActiveGrants.Should().HaveCount(1);
        this.viewModel.ActiveGrants[0].Scope.Should().Be("device");
    }

    [Fact]
    public async Task StatusSummary_WhenUnknownLevel_ShowsUnavailable()
    {
        // Arrange
        this.pipeClient.SetState(NewState(currentLevel: EnforcementLevel.Unknown));

        // Act
        await this.viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert
        this.viewModel.StatusSummary.Should().Be("Protección no disponible");
    }

    [Fact]
    public async Task StatusSummary_WhenPaused_ShowsPaused()
    {
        // Arrange
        this.pipeClient.SetState(NewState(isPaused: true, minutesRemaining: 30));

        // Act
        await this.viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert
        this.viewModel.StatusSummary.Should().Be("Sesión pausada");
    }

    [Fact]
    public async Task StatusSummary_WhenNoLimit_ShowsNoLimit()
    {
        // Arrange
        this.pipeClient.SetState(NewState(minutesRemaining: null));

        // Act
        await this.viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert
        this.viewModel.StatusSummary.Should().Be("Sin límite de tiempo");
    }

    [Fact]
    public async Task StatusSummary_WhenExpired_ShowsExpired()
    {
        // Arrange
        this.pipeClient.SetState(NewState(minutesRemaining: 0));

        // Act
        await this.viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert
        this.viewModel.StatusSummary.Should().Be("Tiempo agotado");
    }

    [Fact]
    public async Task PolicyChanged_TriggersRefresh()
    {
        // Arrange
        await this.viewModel.InitializeAsync().ConfigureAwait(false);
        this.pipeClient.SetState(NewState(minutesRemaining: 99));

        // Act
        this.realtimeSubscriber.RaisePolicyChanged(new PolicyChangedEventArgs { NewVersion = 5 });

        // Wait a moment for the async handler to complete
        await Task.Delay(50).ConfigureAwait(false);

        // Assert
        this.viewModel.MinutesRemaining.Should().Be(99);
    }

    [Fact]
    public void Constructor_WithNullPipeClient_Throws()
    {
        // Act
        Action act = () => GC.KeepAlive(new StatusViewModel(null!, this.realtimeSubscriber));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRealtimeSubscriber_Throws()
    {
        // Act
        Action act = () => GC.KeepAlive(new StatusViewModel(this.pipeClient, null!));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static UsageStateResponse NewState(
        int? minutesRemaining = 30,
        string? currentAppId = "chrome.exe",
        bool isPaused = false,
        GrantInfo[]? activeGrants = null,
        EnforcementLevel currentLevel = EnforcementLevel.Standard,
        ActiveIssue[]? activeIssues = null) => new(
            MinutesRemaining: minutesRemaining,
            CurrentAppId: currentAppId,
            IsPaused: isPaused,
            ActiveGrants: activeGrants ?? Array.Empty<GrantInfo>(),
            CurrentLevel: currentLevel,
            ActiveIssues: activeIssues ?? Array.Empty<ActiveIssue>());

    private sealed class FakeUIPipeClient : IUIPipeClient
    {
        private UsageStateResponse state = new(
            MinutesRemaining: null,
            CurrentAppId: null,
            IsPaused: false,
            ActiveGrants: Array.Empty<GrantInfo>(),
            CurrentLevel: EnforcementLevel.Unknown,
            ActiveIssues: Array.Empty<ActiveIssue>());

        public void SetState(UsageStateResponse state) => this.state = state;

        public Task<UsageStateResponse> GetUsageStateAsync(CancellationToken ct = default)
            => Task.FromResult(this.state);

        public void Dispose()
        {
        }
    }

    private sealed class FakeRealtimeSubscriber : IRealtimeSubscriber
    {
        public bool IsConnected => false;

        public event EventHandler<PolicyChangedEventArgs>? PolicyChanged;

        public event EventHandler<GrantsChangedEventArgs>? GrantsChanged;

        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        public void RaisePolicyChanged(PolicyChangedEventArgs args)
            => this.PolicyChanged?.Invoke(this, args);

        public void Dispose()
        {
        }
    }
}
