// <copyright file="UIMessageHandler.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// T26 — Handles UI messages from App.UI via NamedPipeUIServer.
/// Dispatches messages to the appropriate handlers.
/// </summary>
public sealed class UIMessageHandler
{
    private readonly OnboardingStateService onboardingStateService;
    private readonly EnforcementLevelQueryHandler enforcementLevelQueryHandler;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<UIMessageHandler> logger;
    private readonly IWnsRegistrationCoordinator? wnsRegistrationCoordinator;
    private Func<IIpcMessage, CancellationToken, Task>? sendToAgentAsync;

    public UIMessageHandler(
        OnboardingStateService onboardingStateService,
        EnforcementLevelQueryHandler enforcementLevelQueryHandler,
        IServiceScopeFactory scopeFactory,
        ILogger<UIMessageHandler> logger,
        IWnsRegistrationCoordinator? wnsRegistrationCoordinator = null)
    {
        this.onboardingStateService = onboardingStateService;
        this.enforcementLevelQueryHandler = enforcementLevelQueryHandler;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        this.wnsRegistrationCoordinator = wnsRegistrationCoordinator;
    }

    /// <summary>
    /// Sets the overlay sender function. Called by ControlParentalService after SessionManager is created.
    /// </summary>
    /// <param name="sendToAgentAsync">Function to send messages to the Session Agent.</param>
    public void SetOverlaySender(Func<IIpcMessage, CancellationToken, Task> sendToAgentAsync)
    {
        this.sendToAgentAsync = sendToAgentAsync;
    }

    /// <summary>
    /// Handles a UI message and returns the response.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<IIpcMessage> HandleAsync(IIpcMessage message, CancellationToken ct = default)
        => await this.HandleCoreAsync(message, isAuthenticatedPipeClient: false, ct).ConfigureAwait(false);

    /// <summary>Handles a message only after the named-pipe server authenticated the caller SID.</summary>
    public async Task<IIpcMessage> HandleAuthenticatedAsync(IIpcMessage message, CancellationToken ct = default)
        => await this.HandleCoreAsync(message, isAuthenticatedPipeClient: true, ct).ConfigureAwait(false);

    private async Task<IIpcMessage> HandleCoreAsync(IIpcMessage message, bool isAuthenticatedPipeClient, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        switch (message)
        {
            case GetEnforcementLevel:
                return await this.enforcementLevelQueryHandler.HandleAsync(ct).ConfigureAwait(false);

            case GetOnboardingState:
                var state = await this.onboardingStateService.GetStateAsync(ct).ConfigureAwait(false);
                return new OnboardingStateResponse(state);

            case RecordOnboardingStepCompleted rec:
                // T26 PR (P2 onboarding ownership) — the canonical Service
                // snapshot is the only response shape the App.UI accepts.
                // RecordStepCompletedAsync is idempotent and returns the
                // post-completion snapshot (or the unchanged snapshot on
                // duplicate / unknown step ids).
                var completed = await this.onboardingStateService.RecordStepCompletedAsync(rec.StepId, ct).ConfigureAwait(false);
                return new OnboardingStateResponse(completed);

            case RecordFunnelEvent rec:
                var eventState = await this.onboardingStateService.RecordFunnelEventAsync(rec.EventName, rec.StepId, ct).ConfigureAwait(false);
                return new OnboardingStateResponse(eventState);

            case ShowOverlayCommand cmd:
                await this.HandleShowOverlayAsync(cmd, ct).ConfigureAwait(false);
                return new StepCompletedResponse(true);

            case HideOverlayCommand:
                await this.HandleHideOverlayAsync(ct).ConfigureAwait(false);
                return new StepCompletedResponse(true);

            case PairDevice req:
                return await this.HandlePairDeviceAsync(req, ct).ConfigureAwait(false);

            case ListAccounts:
                return await this.HandleListAccountsAsync(ct).ConfigureAwait(false);

            case CreateAccount req:
                return await this.HandleCreateAccountAsync(req, ct).ConfigureAwait(false);

            case ConvertAccount req:
                return await this.HandleConvertAccountAsync(req, ct).ConfigureAwait(false);

            case GetServiceStatus:
                return await this.HandleGetServiceStatusAsync(ct).ConfigureAwait(false);

            case GetConsentStatus:
                return await this.HandleGetConsentStatusAsync(ct).ConfigureAwait(false);

            case GrantConsent grant:
                return await this.HandleGrantConsentAsync(grant, ct).ConfigureAwait(false);

            case AdvanceOnboardingStep:
                return await this.HandleAdvanceOnboardingStepAsync(ct).ConfigureAwait(false);

            case ResetOnboardingState reset:
                return await this.HandleResetOnboardingStateAsync(reset, ct).ConfigureAwait(false);

            case RegisterWnsChannel register:
                if (!isAuthenticatedPipeClient || this.wnsRegistrationCoordinator is null)
                {
                    return new WnsRegistrationResult("invalid", WnsRegistrationStatus.Denied, "IPC-DENIED");
                }

                return await this.wnsRegistrationCoordinator.RegisterAsync(register, ct).ConfigureAwait(false);

            default:
                System.Diagnostics.Debug.WriteLine(
                    $"[UIMessageHandler] Unknown message type: {message.MessageType}");
                return new StepCompletedResponse(false);
        }
    }

    /// <summary>
    /// T25/T26 — Reads the current consent status from the Service-side
    /// SQLite store (via <see cref="IConsentService"/>).
    /// </summary>
    private async Task<IIpcMessage> HandleGetConsentStatusAsync(CancellationToken ct)
    {
        using var scope = this.scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<IConsentService>();
        var record = await consentService.GetConsentStatusAsync(ct).ConfigureAwait(false);
        return new ConsentStatusSnapshot(
            IsGranted: record.Status == ConsentStatus.Granted,
            GrantedAt: record.GrantedAt,
            GrantedByDeviceId: record.GrantedByDeviceId);
    }

    /// <summary>
    /// T25/T26 — Persists a consent grant via the Service-side SQLite store
    /// and returns the post-write snapshot.
    /// </summary>
    private async Task<IIpcMessage> HandleGrantConsentAsync(GrantConsent grant, CancellationToken ct)
    {
        using var scope = this.scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<IConsentService>();
        await consentService.GrantConsentAsync(grant.GrantedByDeviceId, ct).ConfigureAwait(false);
        var record = await consentService.GetConsentStatusAsync(ct).ConfigureAwait(false);
        return new ConsentStatusSnapshot(
            IsGranted: record.Status == ConsentStatus.Granted,
            GrantedAt: record.GrantedAt,
            GrantedByDeviceId: record.GrantedByDeviceId);
    }

    /// <summary>
    /// T26 PR #11 — Advances the onboarding state machine by one step. Reads the
    /// current snapshot to compute the next index (clamped to <c>Steps.Count</c>
    /// so <see cref="OnboardingStateService.AdvanceAsync"/> flips
    /// <see cref="OnboardingState.IsCompleted"/> at the end), persists, logs at
    /// Information level, and returns the resulting snapshot.
    /// </summary>
    private async Task<IIpcMessage> HandleAdvanceOnboardingStepAsync(CancellationToken ct)
    {
        var current = await this.onboardingStateService.GetStateAsync(ct).ConfigureAwait(false);
        var nextIndex = current.CurrentStepIndex + 1;
        var advanced = await this.onboardingStateService.AdvanceAsync(nextIndex, ct).ConfigureAwait(false);
        this.logger.LogInformation(
            "Onboarding advanced from index {From} to {To}. IsCompleted={IsCompleted}.",
            current.CurrentStepIndex,
            advanced.CurrentStepIndex,
            advanced.IsCompleted);
        return new OnboardingStateResponse(advanced);
    }

    /// <summary>
    /// T26 PR #11 — Resets the onboarding state machine to its initial state.
    /// The optional <see cref="ResetOnboardingState.Reason"/> is logged at Warning
    /// level so destructive resets surface in the Service log; missing/whitespace
    /// reasons log as "(no reason provided)" so the log entry stays structured.
    /// Returns the fresh initial snapshot.
    /// </summary>
    private async Task<IIpcMessage> HandleResetOnboardingStateAsync(ResetOnboardingState reset, CancellationToken ct)
    {
        var reason = string.IsNullOrWhiteSpace(reset.Reason) ? "(no reason provided)" : reset.Reason;
        this.logger.LogWarning(
            "Onboarding state reset requested. Reason: {Reason}.",
            reason);
        var fresh = await this.onboardingStateService.ResetAsync(ct).ConfigureAwait(false);
        return new OnboardingStateResponse(fresh);
    }

    private async Task<IIpcMessage> HandlePairDeviceAsync(PairDevice req, CancellationToken ct)
    {
        if (!AgeBandExtensions.TryParse(req.AgeBand, out var ageBand))
        {
            return new PairDeviceResponse(
                Success: false,
                DeviceId: null,
                ParentId: null,
                PolicyVersion: 0,
                Status: PairingStatus.Error,
                ErrorMessage: "Age band inválido. Seleccioná una opción.");
        }

        // IPairingService is scoped, so resolve from a scope
        using var scope = this.scopeFactory.CreateScope();
        var pairingService = scope.ServiceProvider.GetRequiredService<IPairingService>();

        var result = await pairingService.PairAsync(req.Code, ageBand, ct).ConfigureAwait(false);

        return new PairDeviceResponse(
            Success: result.Success,
            DeviceId: result.DeviceId,
            ParentId: result.ParentId,
            PolicyVersion: result.PolicyVersion,
            Status: result.Status,
            ErrorMessage: result.ErrorMessage);
    }

    private async Task<IIpcMessage> HandleListAccountsAsync(CancellationToken ct)
    {
        using var scope = this.scopeFactory.CreateScope();
        var accountManager = scope.ServiceProvider.GetRequiredService<IAccountManager>();

        var usernames = await accountManager.GetAccountsAsync(ct).ConfigureAwait(false);
        var accounts = new List<AccountInfo>();

        foreach (var username in usernames)
        {
            var isStandard = await accountManager.IsAccountStandardAsync(username, ct).ConfigureAwait(false);
            accounts.Add(new AccountInfo(
                Username: username,
                Type: isStandard ? "Standard" : "Administrator",
                IsStandard: isStandard));
        }

        return new AccountList(accounts);
    }

    private async Task<IIpcMessage> HandleCreateAccountAsync(CreateAccount req, CancellationToken ct)
    {
        using var scope = this.scopeFactory.CreateScope();
        var accountManager = scope.ServiceProvider.GetRequiredService<IAccountManager>();

        var result = await accountManager.CreateStandardAccountAsync(req.Username, req.Password, ct).ConfigureAwait(false);

        return new CreateAccountResponse(
            Success: result.Success,
            ErrorMessage: result.ErrorMessage,
            RequiresElevation: result.RequiresElevation);
    }

    private async Task<IIpcMessage> HandleConvertAccountAsync(ConvertAccount req, CancellationToken ct)
    {
        using var scope = this.scopeFactory.CreateScope();
        var accountManager = scope.ServiceProvider.GetRequiredService<IAccountManager>();

        var result = await accountManager.ConvertToStandardAsync(req.Username, ct).ConfigureAwait(false);

        return new ConvertAccountResponse(
            Success: result.Success,
            ErrorMessage: result.RequiresElevation
                ? $"Necesitás permisos de administrador. {result.ErrorMessage}"
                : result.ErrorMessage);
    }

    private async Task<IIpcMessage> HandleGetServiceStatusAsync(CancellationToken ct)
    {
        var snapshot = await this.enforcementLevelQueryHandler.HandleAsync(ct).ConfigureAwait(false);
        var serviceCheck = snapshot.Checks.FirstOrDefault(
            check => string.Equals(check.CheckName, "service_running", StringComparison.Ordinal));
        var agentCheck = snapshot.Checks.FirstOrDefault(
            check => string.Equals(check.CheckName, "agent_emitting", StringComparison.Ordinal));
        var isRunning = serviceCheck?.IsPassing == true;
        var isReady = isRunning && agentCheck?.IsPassing == true;
        var description = isReady
            ? "Protection service and session agent are active."
            : string.Join(" ", new[] { serviceCheck?.Details, agentCheck?.Details }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return new ServiceStatusResponse(IsInstalled: isRunning, IsRunning: isReady, description);
    }

    private async Task HandleShowOverlayAsync(ShowOverlayCommand cmd, CancellationToken ct)
    {
        if (this.sendToAgentAsync == null)
        {
            System.Diagnostics.Debug.WriteLine("[UIMessageHandler] Overlay sender not set yet.");
            return;
        }

        var overlayMessage = new ShowOverlay(cmd.Reason, cmd.CtaLabel);
        await this.sendToAgentAsync(overlayMessage, ct).ConfigureAwait(false);
    }

    private async Task HandleHideOverlayAsync(CancellationToken ct)
    {
        if (this.sendToAgentAsync == null)
        {
            System.Diagnostics.Debug.WriteLine("[UIMessageHandler] Overlay sender not set yet.");
            return;
        }

        var hideMessage = new HideOverlay();
        await this.sendToAgentAsync(hideMessage, ct).ConfigureAwait(false);
    }
}
