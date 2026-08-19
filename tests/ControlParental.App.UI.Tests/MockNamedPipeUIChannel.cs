// <copyright file="MockNamedPipeUIChannel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System.Collections.Concurrent;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 PR #4 (Fase 5) — In-process <see cref="IUIChannel"/> test double.
///
/// The App.UI previously used a JSON-file store (<see cref="OnboardingStateStore"/>)
/// for onboarding state; after the architectural flip the only path for state
/// persistence is the IPC channel to the Service. Tests that used to drive a
/// real (or "testable") local store now drive this in-memory channel instead —
/// exactly mirroring production traffic without spinning up a real named pipe.
///
/// Usage: tests register query/send handlers up-front. The double raises
/// <see cref="QueryInvoked"/> on every <c>GetOnboardingState</c> call so tests
/// can assert the VM actually issued an IPC read instead of touching the disk.
///
/// T26 PR #5 (Fase 3) — extended with a <see cref="PairDevice"/> handler so
/// pairing tests can drive the IPC channel without spinning up a real pipe.
/// </summary>
public sealed class MockNamedPipeUIChannel : IUIChannel
{
    private readonly ConcurrentQueue<object> sentMessages = new();
    private readonly Dictionary<Type, object> queryHandlers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MockNamedPipeUIChannel"/>
    /// class with the canonical onboarding state already wired up so the
    /// default <see cref="OnboardingStateStore"/> behaviour (initial state) is
    /// preserved.
    /// </summary>
    public MockNamedPipeUIChannel()
    {
        // Seed the default onboarding state — equivalent to what the deleted
        // OnboardingStateStore.CreateInitialState produced. The VM hits this
        // on every InitializeAsync; tests that need a different state replace
        // it via OnGetOnboardingState(...) before constructing the VM.
        this.OnGetOnboardingState(() => BuildDefaultState());
    }

    /// <summary>
    /// Gets all messages sent via <see cref="SendAsync{T}"/>. Inspect this queue
    /// to verify what the VM emitted over the wire (e.g. funnel events,
    /// step completions).
    /// </summary>
    public IReadOnlyCollection<object> SentMessages => this.sentMessages;

    /// <summary>
    /// Raised once per <c>GetOnboardingState</c> query — useful to assert the
    /// number of times the VM refreshed its in-memory snapshot.
    /// </summary>
    public event Action? QueryInvoked;

    /// <summary>
    /// Gets captured by the most recent <see cref="PairDevice"/> query so pairing
    /// tests can assert what the VM actually sent on the wire (code, age band,
    /// etc.) without intercepting the IPC. Cleared on every query so a stale
    /// value never leaks across tests.
    /// </summary>
    public ControlParental.App.UI.PairDevice? LastPairDeviceRequest { get; private set; }

    /// <summary>
    /// Replaces the state the channel returns for <see cref="GetOnboardingState"/>.
    /// </summary>
    /// <param name="state">The state to return on the next query.</param>
    public void OnGetOnboardingState(ControlParental.Domain.OnboardingState state)
    {
        this.OnGetOnboardingState(() => state);
    }

    /// <summary>
    /// Registers a factory that produces the state on demand. Each call to
    /// <c>QueryAsync&lt;GetOnboardingState, ...&gt;</c> invokes the factory
    /// so the test can simulate server-side mutations between calls.
    /// </summary>
    /// <param name="factory">Factory invoked on each query.</param>
    public void OnGetOnboardingState(Func<ControlParental.Domain.OnboardingState> factory)
    {
        this.queryHandlers[typeof(ControlParental.App.UI.GetOnboardingState)] = factory;
    }

    /// <summary>
    /// T26 PR #5 (Fase 3) — registers a factory that produces the
    /// <see cref="PairDeviceResponse"/> for every <c>QueryAsync&lt;PairDevice,
    /// PairDeviceResponse&gt;</c> call. The factory receives the outbound
    /// <see cref="PairDevice"/> so the test can inspect the wire payload
    /// (code, age band) before returning a status-driven response.
    /// </summary>
    /// <param name="factory">Factory invoked on each pairing query.</param>
    public void OnPairDevice(Func<ControlParental.App.UI.PairDevice, ControlParental.App.UI.PairDeviceResponse> factory)
    {
        this.queryHandlers[typeof(ControlParental.App.UI.PairDevice)] = factory;
    }

    /// <summary>
    /// T26 PR #11 — registers a factory that produces the
    /// <see cref="OnboardingStateResponse"/> for every
    /// <c>QueryAsync&lt;AdvanceOnboardingStep, OnboardingStateResponse&gt;</c> call.
    /// Tests can inspect the outbound <see cref="AdvanceOnboardingStep"/> (currently
    /// no payload) before returning the post-advance snapshot.
    /// </summary>
    /// <param name="factory">Factory invoked on each advance query.</param>
    public void OnAdvanceOnboardingStep(Func<ControlParental.Domain.OnboardingState> factory)
    {
        this.queryHandlers[typeof(ControlParental.App.UI.AdvanceOnboardingStep)] = factory;
        this.LastAdvanceRequest = null;
    }

    /// <summary>
    /// T26 PR #11 — registers a factory that produces the
    /// <see cref="OnboardingStateResponse"/> for every
    /// <c>QueryAsync&lt;ResetOnboardingState, OnboardingStateResponse&gt;</c> call.
    /// The factory receives the outbound <see cref="ResetOnboardingState"/> so the
    /// test can inspect the supplied reason (audit-trail concern).
    /// </summary>
    /// <param name="factory">Factory invoked on each reset query.</param>
    public void OnResetOnboardingState(Func<ControlParental.App.UI.ResetOnboardingState, ControlParental.Domain.OnboardingState> factory)
    {
        this.queryHandlers[typeof(ControlParental.App.UI.ResetOnboardingState)] = factory;
        this.LastResetRequest = null;
    }

    /// <summary>
    /// T26 PR (P2 onboarding ownership) — registers a factory that produces the
    /// <see cref="OnboardingStateResponse"/> for every
    /// <c>QueryAsync&lt;RecordOnboardingStepCompleted, OnboardingStateResponse&gt;</c> call.
    /// The factory receives the outbound <see cref="RecordOnboardingStepCompleted"/>
    /// so the test can inspect the supplied step id.
    /// </summary>
    /// <param name="factory">Factory invoked on each step-completion query.</param>
    public void OnRecordOnboardingStepCompleted(
        Func<ControlParental.App.UI.RecordOnboardingStepCompleted, ControlParental.Domain.OnboardingState> factory)
    {
        this.queryHandlers[typeof(ControlParental.App.UI.RecordOnboardingStepCompleted)] = factory;
        this.LastRecordOnboardingStepRequest = null;
    }

    /// <summary>
    /// Gets t26 PR (P2 onboarding ownership) — captures the most recent
    /// <see cref="RecordOnboardingStepCompleted"/> request so tests can assert
    /// the step id was actually sent on the wire.
    /// </summary>
    public ControlParental.App.UI.RecordOnboardingStepCompleted? LastRecordOnboardingStepRequest { get; private set; }

    /// <summary>
    /// Gets t26 PR #11 — captures the most recent <see cref="AdvanceOnboardingStep"/>
    /// request so tests can assert the message was actually sent on the wire.
    /// Cleared by <see cref="OnAdvanceOnboardingStep"/> registration so a stale
    /// value never leaks across tests.
    /// </summary>
    public ControlParental.App.UI.AdvanceOnboardingStep? LastAdvanceRequest { get; private set; }

    /// <summary>
    /// Gets t26 PR #11 — captures the most recent <see cref="ResetOnboardingState"/>
    /// request so tests can assert the supplied reason was actually sent on the wire.
    /// </summary>
    public ControlParental.App.UI.ResetOnboardingState? LastResetRequest { get; private set; }

    /// <inheritdoc />
    public Task<TResponse?> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken ct = default)
        where TQuery : ControlParental.Domain.IUIMessage
        where TResponse : class, ControlParental.Domain.IUIMessage
    {
        if (query is ControlParental.App.UI.GetOnboardingState)
        {
            this.QueryInvoked?.Invoke();
            if (this.queryHandlers.TryGetValue(typeof(ControlParental.App.UI.GetOnboardingState), out var handler)
                && handler is Func<ControlParental.Domain.OnboardingState> factory)
            {
                var snapshot = factory();

                // TResponse is wired to OnboardingStateResponse(State Domain.OnboardingState)
                // by the test — we build it dynamically to keep the mock decoupled
                // from the concrete response type. Note: we must use the App.UI
                // envelope so the runtime type matches what the caller expects.
                var response = (TResponse)(object)new ControlParental.App.UI.OnboardingStateResponse(snapshot);
                return Task.FromResult<TResponse?>(response);
            }
        }

        if (query is ControlParental.App.UI.PairDevice pairRequest)
        {
            this.LastPairDeviceRequest = pairRequest;
            if (this.queryHandlers.TryGetValue(typeof(ControlParental.App.UI.PairDevice), out var handler)
                && handler is Func<ControlParental.App.UI.PairDevice, ControlParental.App.UI.PairDeviceResponse> pairFactory)
            {
                var response = pairFactory(pairRequest);
                return Task.FromResult<TResponse?>((TResponse)(object)response);
            }

            // No pairing handler registered — return a null response so the
            // VM surfaces its IPC-unavailable copy (matches a Service that
            // declined to respond).
            return Task.FromResult<TResponse?>(null);
        }

        if (query is ControlParental.App.UI.AdvanceOnboardingStep advanceRequest)
        {
            this.LastAdvanceRequest = advanceRequest;
            if (this.queryHandlers.TryGetValue(typeof(ControlParental.App.UI.AdvanceOnboardingStep), out var handler)
                && handler is Func<ControlParental.Domain.OnboardingState> advanceFactory)
            {
                var snapshot = advanceFactory();
                return Task.FromResult<TResponse?>(
                    (TResponse)(object)new ControlParental.App.UI.OnboardingStateResponse(snapshot));
            }

            return Task.FromResult<TResponse?>(null);
        }

        if (query is ControlParental.App.UI.ResetOnboardingState resetRequest)
        {
            this.LastResetRequest = resetRequest;
            if (this.queryHandlers.TryGetValue(typeof(ControlParental.App.UI.ResetOnboardingState), out var handler)
                && handler is Func<ControlParental.App.UI.ResetOnboardingState, ControlParental.Domain.OnboardingState> resetFactory)
            {
                var snapshot = resetFactory(resetRequest);
                return Task.FromResult<TResponse?>(
                    (TResponse)(object)new ControlParental.App.UI.OnboardingStateResponse(snapshot));
            }

            return Task.FromResult<TResponse?>(null);
        }

        if (query is ControlParental.App.UI.RecordOnboardingStepCompleted recordRequest)
        {
            this.LastRecordOnboardingStepRequest = recordRequest;
            if (this.queryHandlers.TryGetValue(typeof(ControlParental.App.UI.RecordOnboardingStepCompleted), out var handler)
                && handler is Func<ControlParental.App.UI.RecordOnboardingStepCompleted, ControlParental.Domain.OnboardingState> recordFactory)
            {
                var snapshot = recordFactory(recordRequest);
                return Task.FromResult<TResponse?>(
                    (TResponse)(object)new ControlParental.App.UI.OnboardingStateResponse(snapshot));
            }

            return Task.FromResult<TResponse?>(null);
        }

        return Task.FromResult<TResponse?>(null);
    }

    /// <inheritdoc />
    public Task SendAsync<T>(T message, CancellationToken ct = default)
        where T : ControlParental.Domain.IUIMessage
    {
        this.sentMessages.Enqueue(message!);
        return Task.CompletedTask;
    }

    private static ControlParental.Domain.IUIMessage AsUIMessage<T>(T message)
        where T : ControlParental.Domain.IUIMessage
    {
        return message!;
    }

    private static object AsAppUIMessage<T>(T message)
        where T : ControlParental.Domain.IUIMessage
    {
        return message!;
    }

    private static ControlParental.Domain.OnboardingState BuildDefaultState()
    {
        var steps = new List<Domain.OnboardingStep>
        {
            new(0, "pairing", "Emparejar dispositivo", "Pedile a tu tutor el código de emparejamiento.", "Emparejar", Domain.OnboardingStepStatus.Pending),
            new(1, "consent", "Consentimiento", "Antes de continuar, necesitamos tu consentimiento.", "Dar consentimiento", Domain.OnboardingStepStatus.Locked),
            new(2, "account", "Cuenta del menor", "Creá una cuenta estándar para el menor.", "Crear cuenta", Domain.OnboardingStepStatus.Locked),
            new(3, "demo", "Probemos tu protección", "Veamos cómo funciona la protección.", "Probar", Domain.OnboardingStepStatus.Locked, IsFirstWin: true),
            new(4, "managed", "Subir el nivel", "Activá la capa preventiva MANAGED.", "Activar", Domain.OnboardingStepStatus.Locked),
        };
        return new ControlParental.Domain.OnboardingState(0, false, false, steps, Array.Empty<Domain.FunnelEvent>());
    }
}
