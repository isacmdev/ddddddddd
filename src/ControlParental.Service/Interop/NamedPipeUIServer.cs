// <copyright file="NamedPipeUIServer.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Interop;

using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ControlParental.Domain;

/// <summary>
/// T26 — Named pipe server for IPC with App.UI.
/// Runs in the Service (LocalSystem, Session 0).
/// Accepts connections from the parent user who launched App.UI (interactive session).
/// Uses InteractiveUserSid ACL instead of child SID used for the agent pipe.
/// </summary>
public sealed class NamedPipeUIServer : IDisposable
{
    private const string PipeNamePrefix = "ControlParental";
    private const string UIPPipeName = "UI";
    private const int BufferSize = 65536;
    private readonly string pipeName;
    private readonly SecurityIdentifier? parentSid;
    private readonly SecurityIdentifier? childSid;
    private readonly CancellationTokenSource internalCts;
    private PipeServerListener? listenerTask;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeUIServer"/> class.
    /// Allows the parent account to be unspecified; the pipe is then restricted to
    /// the LocalSystem and the Administrators group.
    /// </summary>
    public NamedPipeUIServer()
        : this(null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeUIServer"/> class.
    /// </summary>
    /// <param name="parentSid">SID of the parent/adult account that owns App.UI.</param>
    /// <param name="childSid">Optional SID of the child account; the UI pipe is explicitly denied to this SID.</param>
    public NamedPipeUIServer(SecurityIdentifier? parentSid, SecurityIdentifier? childSid)
    {
        this.pipeName = $"{PipeNamePrefix}.{UIPPipeName}";
        this.parentSid = parentSid;
        this.childSid = childSid;
        this.internalCts = new CancellationTokenSource();
    }

    /// <summary>
    /// Creates a PipeSecurity that restricts the UI pipe to LocalSystem and the
    /// parent/administrator account. Guests are explicitly denied.
    /// </summary>
    /// <returns></returns>
    internal static PipeSecurity CreatePipeSecurity()
        => CreatePipeSecurity(null, null);

    /// <summary>
    /// Creates a PipeSecurity that restricts the UI pipe to LocalSystem and the
    /// specified parent account. If no parent SID is provided, the Administrators
    /// group is allowed. Guests and the child SID (when provided) are explicitly denied.
    /// </summary>
    /// <param name="parentSid">SID of the parent/adult account, or null to allow any administrator.</param>
    /// <param name="childSid">SID of the child account, or null.</param>
    /// <returns></returns>
    internal static PipeSecurity CreatePipeSecurity(SecurityIdentifier? parentSid, SecurityIdentifier? childSid)
    {
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);

        // LocalSystem: full control
        var localSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        security.AddAccessRule(new PipeAccessRule(
            localSystemSid,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        if (parentSid != null)
        {
            // Parent/adult account: read/write
            security.AddAccessRule(new PipeAccessRule(
                parentSid,
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));
        }
        else
        {
            // Fallback: any administrator account (used when the parent SID is not yet known)
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.AddAccessRule(new PipeAccessRule(
                adminSid,
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));
        }

        if (childSid != null)
        {
            // Child account must never be able to drive the UI pipe
            security.AddAccessRule(new PipeAccessRule(
                childSid,
                PipeAccessRights.FullControl,
                AccessControlType.Deny));
        }

        // Explicit deny for Guests
        var guestsSid = new SecurityIdentifier(WellKnownSidType.BuiltinGuestsSid, null);
        security.AddAccessRule(new PipeAccessRule(
            guestsSid,
            PipeAccessRights.FullControl,
            AccessControlType.Deny));

        return security;
    }

    /// <summary>
    /// Starts the named pipe server.
    /// </summary>
    /// <param name="messageHandler">Handler for dispatching UI messages to responses.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task StartAsync(UIMessageHandler messageHandler, CancellationToken cancellationToken = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            this.internalCts.Token);

        this.listenerTask = new PipeServerListener(
            this.pipeName,
            this.parentSid,
            this.childSid,
            messageHandler,
            () => this.OnDisconnected(),
            linkedCts.Token);

        await this.listenerTask.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the named pipe server.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task StopAsync()
    {
        this.internalCts.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a response message back to the connected UI client.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default)
    {
        if (this.listenerTask == null)
        {
            return;
        }

        await this.listenerTask.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Event raised when the client disconnects.
    /// </summary>
    public event Action? Disconnected;

    private void OnDisconnected()
    {
        this.Disconnected?.Invoke();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.disposed)
        {
            this.internalCts.Cancel();
            this.listenerTask?.Dispose();
            this.internalCts.Dispose();
            this.disposed = true;
        }
    }

    /// <summary>
    /// Internal listener that accepts connections and handles message dispatch.
    /// </summary>
    private sealed class PipeServerListener : IDisposable
    {
        private readonly string pipeName;
        private readonly SecurityIdentifier? parentSid;
        private readonly SecurityIdentifier? childSid;
        private readonly UIMessageHandler messageHandler;
        private readonly Action onDisconnected;
        private readonly CancellationToken cancellationToken;
        private NamedPipeServerStream? pipeServer;
        private Task? listenerTask;
        private bool disposed;

        public PipeServerListener(
            string pipeName,
            SecurityIdentifier? parentSid,
            SecurityIdentifier? childSid,
            UIMessageHandler messageHandler,
            Action onDisconnected,
            CancellationToken cancellationToken)
        {
            this.pipeName = pipeName;
            this.parentSid = parentSid;
            this.childSid = childSid;
            this.messageHandler = messageHandler;
            this.onDisconnected = onDisconnected;
            this.cancellationToken = cancellationToken;
        }

        public bool IsConnected => this.pipeServer?.IsConnected ?? false;

        public async Task StartAsync()
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var pipeSecurity = CreatePipeSecurity(this.parentSid, this.childSid);

                    this.pipeServer = NamedPipeServerStreamAcl.Create(
                        this.pipeName,
                        PipeDirection.InOut,
                        255,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous,
                        inBufferSize: BufferSize,
                        outBufferSize: BufferSize,
                        pipeSecurity: pipeSecurity,
                        inheritability: HandleInheritability.None);

                    await this.pipeServer.WaitForConnectionAsync(this.cancellationToken).ConfigureAwait(false);

                    // Reject unauthorized clients before handing them off to message dispatch.
                    if (!this.ValidateClient())
                    {
                        this.pipeServer.Dispose();
                        this.pipeServer = null;
                        continue;
                    }

                    // Start reading messages in a background task
                    _ = this.ReadMessagesAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NamedPipeUIServer] IO error: {ex.Message}");
                    this.pipeServer?.Dispose();
                    this.pipeServer = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NamedPipeUIServer] Unexpected error: {ex.Message}");
                    this.pipeServer?.Dispose();
                    this.pipeServer = null;
                }
            }
        }

        public async Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default)
        {
            if (this.pipeServer == null || !this.pipeServer.IsConnected)
            {
                return;
            }

            // T26 PR #14 — source-gen dispatch by runtime type so we avoid
            // the reflection-based JsonSerializer.Serialize(IIpcMessage) call.
            // The App.UI mirror uses the same Domain-side UIMessagesJsonContext
            // catalogue so the wire format is symmetric.
            var typeInfo = UIMessagesJsonContext.Default.GetTypeInfo(message.GetType())!;
            var json = JsonSerializer.Serialize(message, typeInfo);
            var bytes = Encoding.UTF8.GetBytes(json);
            await this.pipeServer.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }

        private async Task ReadMessagesAsync()
        {
            var buffer = new byte[BufferSize];

            try
            {
                while (this.pipeServer?.IsConnected ?? false)
                {
                    var bytesRead = await this.pipeServer.ReadAsync(buffer, this.cancellationToken).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var message = this.DeserializeMessage(json);

                    if (message != null)
                    {
                        // Handle message and send response
                        var response = await this.messageHandler.HandleAsync(message, this.cancellationToken).ConfigureAwait(false);
                        if (response != null)
                        {
                            await this.SendAsync(response, this.cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (IOException)
            {
                // Connection closed
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested
            }

            this.onDisconnected?.Invoke();
        }

        private bool ValidateClient()
        {
            // If no explicit parent/child SIDs are configured, rely on the pipe ACL.
            if (this.parentSid == null && this.childSid == null)
            {
                return true;
            }

            try
            {
                var clientSid = this.pipeServer?.GetImpersonationUserSid();
                if (clientSid == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[NamedPipeUIServer] Could not get client SID.");
                    return false;
                }

                if (this.childSid != null && clientSid.Equals(this.childSid))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NamedPipeUIServer] Rejected child SID '{clientSid.Value}'.");
                    return false;
                }

                if (this.parentSid != null && !clientSid.Equals(this.parentSid))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NamedPipeUIServer] Rejected SID '{clientSid.Value}'; expected parent '{this.parentSid.Value}'.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NamedPipeUIServer] Client validation failed: {ex.Message}");
                return false;
            }
        }

        private IIpcMessage? DeserializeMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // The IPC envelope is always a JSON object. Any other root
                // (array, scalar, etc.) is malformed and must fail closed
                // without crashing the dispatch loop.
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                if (!root.TryGetProperty("MessageType", out var typeElement))
                {
                    return null;
                }

                var messageType = typeElement.GetString();

                // T26 PR #14 — source-gen dispatch via UIMessagesJsonContext.
                // Each branch hands the JSON to JsonSerializer.Deserialize<T>
                // with the matching JsonTypeInfo from the source-gen context,
                // eliminating the reflection-based serializer at the IPC boundary.
                return messageType switch
                {
                    nameof(GetEnforcementLevel) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.GetEnforcementLevel),
                    nameof(EnforcementLevelResponse) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.EnforcementLevelResponse),
                    nameof(GetOnboardingState) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.GetOnboardingState),
                    nameof(OnboardingStateResponse) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.OnboardingStateResponse),
                    nameof(RecordOnboardingStepCompleted) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.RecordOnboardingStepCompleted),
                    nameof(StepCompletedResponse) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.StepCompletedResponse),
                    nameof(RecordFunnelEvent) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.RecordFunnelEvent),
                    nameof(ShowOverlayCommand) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.ShowOverlayCommand),
                    nameof(HideOverlayCommand) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.HideOverlayCommand),
                    nameof(GetUsageState) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.GetUsageState),
                    nameof(UsageStateResponse) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.UsageStateResponse),
                    nameof(TriggerSync) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.TriggerSync),
                    nameof(PairDevice) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.PairDevice),
                    nameof(PairDeviceResponse) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.PairDeviceResponse),
                    nameof(ListAccounts) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.ListAccounts),
                    nameof(AccountList) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.AccountList),
                    nameof(CreateAccount) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.CreateAccount),
                    nameof(CreateAccountResponse) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.CreateAccountResponse),
                    nameof(ConvertAccount) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.ConvertAccount),
                    nameof(ConvertAccountResponse) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.ConvertAccountResponse),
                    nameof(GetServiceStatus) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.GetServiceStatus),
                    nameof(ServiceStatusResponse) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.ServiceStatusResponse),
                    nameof(GetConsentStatus) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.GetConsentStatus),
                    nameof(ConsentStatusSnapshot) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.ConsentStatusSnapshot),
                    nameof(GrantConsent) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.GrantConsent),
                    nameof(AdvanceOnboardingStep) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.AdvanceOnboardingStep),
                    nameof(ResetOnboardingState) => JsonSerializer.Deserialize(json, UIMessagesJsonContext.Default.ResetOnboardingState),
                    _ => null,
                };
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NamedPipeUIServer] Failed to deserialize message: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.pipeServer?.Dispose();
                this.disposed = true;
            }
        }
    }
}
