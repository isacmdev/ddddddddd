// <copyright file="FakeIpcOnboardingStateService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;

/// <summary>
/// T26 PR (P2 onboarding ownership) — In-process <see cref="IIpcOnboardingStateService"/>
/// test double. Mirrors the contract the production <see cref="IpcOnboardingStateService"/>
/// exposes (read, complete, advance, reset) and returns the canonical
/// <see cref="OnboardingState"/> snapshot for every mutation so the ViewModel
/// can refresh its observable surface the same way it does in production.
///
/// Default behaviour: the fake returns a deterministic five-step initial
/// state for <see cref="GetOnboardingStateAsync"/>, and the
/// <see cref="CompleteOnboardingStepAsync"/>, <see cref="AdvanceOnboardingStepAsync"/>,
/// and <see cref="ResetOnboardingStateAsync"/> methods mutate that state in
/// memory the same way the real Service does. Tests can also configure
/// null-returning handlers (via <see cref="FailNextWithUnavailable"/>) to
/// exercise the fail-closed <see cref="ConsentServiceUnavailableException"/>
/// path.
/// </summary>
public sealed class FakeIpcOnboardingStateService : IIpcOnboardingStateService
{
    private OnboardingState state;
    private int nullResponseCountdown;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeIpcOnboardingStateService"/>
    /// class with the canonical five-step initial state.
    /// </summary>
    public FakeIpcOnboardingStateService()
    {
        this.state = CreateInitialState();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeIpcOnboardingStateService"/>
    /// class with a custom seed state.
    /// </summary>
    /// <param name="seed">State to seed the fake with.</param>
    public FakeIpcOnboardingStateService(OnboardingState seed)
    {
        this.state = seed;
    }

    /// <summary>
    /// Gets captured outbound requests so tests can assert the wire payload
    /// (step id, reset reason, etc.) without intercepting IPC traffic.
    /// </summary>
    public List<string> CompletedStepIds { get; } = new();

    /// <summary>
    /// Gets captured reset reasons, in order.
    /// </summary>
    public List<string?> ResetReasons { get; } = new();

    public List<(FunnelEventType Type, string StepId)> FunnelEvents { get; } = new();

    /// <summary>
    /// Marks the next <see cref="nullResponseCountdown"/> IPC calls as
    /// transport failures — the fake throws <see cref="ConsentServiceUnavailableException"/>
    /// instead of returning a snapshot.
    /// </summary>
    /// <param name="count">Number of calls to fail.</param>
    public void FailNextWithUnavailable(int count = 1)
    {
        this.nullResponseCountdown = count;
    }

    /// <inheritdoc />
    public Task<OnboardingState> GetOnboardingStateAsync(CancellationToken ct = default)
    {
        if (this.TryConsumeNullResponse())
        {
            throw new ConsentServiceUnavailableException(
                "Fake: Service did not acknowledge the state read.");
        }

        return Task.FromResult(this.state);
    }

    /// <inheritdoc />
    public Task<OnboardingState> CompleteOnboardingStepAsync(string stepId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stepId))
        {
            throw new ArgumentException(
                "Step id must be a non-empty stable identifier.",
                nameof(stepId));
        }

        if (this.TryConsumeNullResponse())
        {
            throw new ConsentServiceUnavailableException(
                "Fake: Service did not acknowledge the step completion.");
        }

        this.CompletedStepIds.Add(stepId);

        var steps = this.state.Steps.ToList();
        var idx = -1;
        for (int i = 0; i < steps.Count; i++)
        {
            if (string.Equals(steps[i].Id, stepId, StringComparison.Ordinal))
            {
                idx = i;
                break;
            }
        }

        if (idx >= 0 && steps[idx].Status != OnboardingStepStatus.Completed)
        {
            steps[idx] = steps[idx] with { Status = OnboardingStepStatus.Completed };
            this.state = this.state with { Steps = steps };
        }

        return Task.FromResult(this.state);
    }

    /// <inheritdoc />
    public Task<OnboardingState> AdvanceOnboardingStepAsync(CancellationToken ct = default)
    {
        if (this.TryConsumeNullResponse())
        {
            throw new ConsentServiceUnavailableException(
                "Fake: Service did not acknowledge the advance.");
        }

        var nextIndex = this.state.CurrentStepIndex + 1;
        if (nextIndex >= this.state.Steps.Count)
        {
            this.state = this.state with { IsCompleted = true };
            return Task.FromResult(this.state);
        }

        var steps = this.state.Steps.ToList();
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].Index < nextIndex)
            {
                steps[i] = steps[i] with { Status = OnboardingStepStatus.Completed };
            }
            else if (steps[i].Index == nextIndex)
            {
                steps[i] = steps[i] with { Status = OnboardingStepStatus.InProgress };
            }
        }

        this.state = this.state with { CurrentStepIndex = nextIndex, Steps = steps };
        return Task.FromResult(this.state);
    }

    /// <inheritdoc />
    public Task<OnboardingState> ResetOnboardingStateAsync(string? reason = null, CancellationToken ct = default)
    {
        if (this.TryConsumeNullResponse())
        {
            throw new ConsentServiceUnavailableException(
                "Fake: Service did not acknowledge the reset.");
        }

        this.ResetReasons.Add(reason);
        this.state = CreateInitialState();
        return Task.FromResult(this.state);
    }

    public Task<OnboardingState> RecordFunnelEventAsync(FunnelEventType type, string stepId, CancellationToken ct = default)
    {
        if (this.TryConsumeNullResponse())
        {
            throw new ConsentServiceUnavailableException("Fake: Service did not acknowledge the funnel event.");
        }

        if (!this.FunnelEvents.Contains((type, stepId)))
        {
            this.FunnelEvents.Add((type, stepId));
            this.state = this.state with
            {
                Events = this.state.Events.Append(new FunnelEvent(type, stepId, DateTimeOffset.UtcNow)).ToList(),
            };
        }

        return Task.FromResult(this.state);
    }

    public Task<ControlParental.App.UI.ServiceStatusResponse> RequestOrVerifyServiceSetupAsync(CancellationToken ct = default)
        => Task.FromResult(new ControlParental.App.UI.ServiceStatusResponse(true, true, "Ready"));

    private bool TryConsumeNullResponse()
    {
        if (this.nullResponseCountdown <= 0)
        {
            return false;
        }

        this.nullResponseCountdown--;
        return true;
    }

    private static OnboardingState CreateInitialState()
    {
        var steps = new List<OnboardingStep>
        {
            new(0, "pairing", "Pairing", "x", "Pair", OnboardingStepStatus.InProgress),
            new(1, "consent", "Consent", "x", "Consent", OnboardingStepStatus.Locked),
            new(2, "account", "Account", "x", "Account", OnboardingStepStatus.Locked),
            new(3, "service", "Service", "x", "Verify", OnboardingStepStatus.Locked),
            new(4, "demo", "Demo", "x", "Demo", OnboardingStepStatus.Locked, IsFirstWin: true),
            new(5, "managed", "Managed", "x", "Managed", OnboardingStepStatus.Locked),
        };
        return new OnboardingState(0, false, false, steps, Array.Empty<FunnelEvent>());
    }
}
