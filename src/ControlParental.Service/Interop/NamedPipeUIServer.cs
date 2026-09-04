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
using ControlParental.Domain.WireContracts;
using ControlParental.Domain.WireContracts.Models;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<NamedPipeUIServer> logger;
    private PipeServerListener? listenerTask;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeUIServer"/> class.
    /// Allows the parent account to be unspecified; the pipe is then restricted to
    /// the LocalSystem and the Administrators group.
    /// </summary>
    public NamedPipeUIServer()
        : this(null, null, Microsoft.Extensions.Logging.Abstractions.NullLogger<NamedPipeUIServer>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeUIServer"/> class.
    /// </summary>
    /// <param name="parentSid">SID of the parent/adult account that owns App.UI.</param>
    /// <param name="childSid">Optional SID of the child account; the UI pipe is explicitly denied to this SID.</param>
    public NamedPipeUIServer(SecurityIdentifier? parentSid, SecurityIdentifier? childSid)
        : this(parentSid, childSid, Microsoft.Extensions.Logging.Abstractions.NullLogger<NamedPipeUIServer>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance with an injected logger.
    /// </summary>
    public NamedPipeUIServer(
        SecurityIdentifier? parentSid,
        SecurityIdentifier? childSid,
        ILogger<NamedPipeUIServer> logger)
        : this(parentSid, childSid, logger, $"{PipeNamePrefix}.{UIPPipeName}")
    {
    }

    /// <summary>
    /// Initializes a testable instance with an isolated pipe name.
    /// </summary>
    internal NamedPipeUIServer(
        SecurityIdentifier? parentSid,
        SecurityIdentifier? childSid,
        ILogger<NamedPipeUIServer> logger,
        string pipeName)
    {
        this.pipeName = string.IsNullOrWhiteSpace(pipeName)
            ? throw new ArgumentException("Pipe name is required.", nameof(pipeName))
            : pipeName;
        this.parentSid = parentSid;
        this.childSid = childSid;
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            linkedCts.Token,
            null,
            null,
            this.logger);

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
    internal sealed class PipeServerListener : IDisposable
    {
        private readonly string pipeName;
        private readonly SecurityIdentifier? parentSid;
        private readonly SecurityIdentifier? childSid;
        private readonly UIMessageHandler messageHandler;
        private readonly Action onDisconnected;
        private readonly CancellationToken cancellationToken;
        private IUiPipeServer? pipeServer;
        private Task? listenerTask;
        private bool disposed;

        public PipeServerListener(
            string pipeName,
            SecurityIdentifier? parentSid,
            SecurityIdentifier? childSid,
            UIMessageHandler messageHandler,
            Action onDisconnected,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<IUiPipeServer>>? pipeFactory = null,
            Func<TimeSpan, CancellationToken, Task>? delay = null,
            ILogger? logger = null)
        {
            this.pipeName = pipeName;
            this.parentSid = parentSid;
            this.childSid = childSid;
            this.messageHandler = messageHandler;
            this.onDisconnected = onDisconnected;
            this.cancellationToken = cancellationToken;
            this.pipeFactory = pipeFactory ?? this.CreatePipeAsync;
            this.delay = delay ?? ((interval, token) => Task.Delay(interval, token));
            this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }

        public bool IsConnected => this.pipeServer?.IsConnected ?? false;

        internal bool IsReady { get; private set; }

        public async Task StartAsync()
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                this.IsReady = false;
                try
                {
                    var connection = await this.pipeFactory(this.cancellationToken).ConfigureAwait(false);
                    this.pipeServer = connection;
                    var connectionTask = connection.WaitForConnectionAsync(this.cancellationToken);
                    this.IsReady = true;
                    await connectionTask.ConfigureAwait(false);
                    this.IsReady = false;

                    // Reject unauthorized clients before handing them off to message dispatch.
                    if (!this.ValidateClient(connection))
                    {
                        this.ClosePipe();
                        continue;
                    }

                    // Own the accepted connection until its complete read/handle/write
                    // lifecycle has drained. This also prevents responses from being
                    // routed through a later accepted connection.
                    await this.ReadMessagesAsync(connection).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    this.IsReady = false;
                    break;
                }
                catch (Exception ex)
                {
                    this.IsReady = false;
                    this.logger.LogError(
                        ex,
                        "[NamedPipeUIServer] Pipe server creation/listening failed ({ExceptionType}).",
                        ex.GetType().Name);
                    this.ClosePipe();
                    if (!this.cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await this.delay(TimeSpan.FromMilliseconds(250), this.cancellationToken).ConfigureAwait(false);
                            await Task.Yield();
                        }
                        catch (OperationCanceledException) when (this.cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                }
            }
        }

        public async Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default)
        {
            var connection = this.pipeServer;
            if (connection == null || !connection.IsConnected)
            {
                return;
            }

            await SendAsync(connection, message, cancellationToken).ConfigureAwait(false);
        }

        private static Task SendAsync(
            IUiPipeServer connection,
            IIpcMessage message,
            CancellationToken cancellationToken)
        {
            // T26 PR #14 — source-gen dispatch by runtime type so we avoid
            // the reflection-based JsonSerializer.Serialize(IIpcMessage) call.
            var typeInfo = UIMessagesJsonContext.Default.GetTypeInfo(message.GetType())!;
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, typeInfo));
            return bytes.Length <= BufferSize
                ? connection.WriteAsync(bytes, cancellationToken)
                : Task.CompletedTask;
        }

        private async Task ReadMessagesAsync(IUiPipeServer connection)
        {
            var buffer = new byte[BufferSize];
            var messageBytes = new MemoryStream();

            try
            {
                while (connection.IsConnected)
                {
                    var bytesRead = await connection.ReadAsync(buffer, this.cancellationToken).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    messageBytes.Write(buffer, 0, bytesRead);
                    if (messageBytes.Length > BufferSize || !connection.IsMessageComplete)
                    {
                        if (messageBytes.Length > BufferSize)
                        {
                            break;
                        }

                        continue;
                    }

                    var json = Encoding.UTF8.GetString(messageBytes.GetBuffer(), 0, checked((int)messageBytes.Length));
                    messageBytes.SetLength(0);
                    var message = this.DeserializeMessage(json);

                    if (message != null)
                    {
                        // Handle message and send response
                        var response = await this.messageHandler.HandleAuthenticatedAsync(message, this.cancellationToken).ConfigureAwait(false);
                        if (response != null)
                        {
                            await SendAsync(connection, response, this.cancellationToken).ConfigureAwait(false);
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
            finally
            {
                this.IsReady = false;
                this.onDisconnected?.Invoke();
                if (ReferenceEquals(this.pipeServer, connection))
                {
                    this.pipeServer = null;
                }

                connection.Dispose();
            }
        }

        private bool ValidateClient(IUiPipeServer connection)
        {
            // If no explicit parent/child SIDs are configured, rely on the pipe ACL.
            if (this.parentSid == null && this.childSid == null)
            {
                return true;
            }

            try
            {
                var clientSid = connection.GetImpersonationUserSid();
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

                // Versioned wire envelopes are not UI commands. Validate them at
                // this boundary and never pass opaque hints, policy, or integrity
                // data to the authenticated UI handler.
                if (root.TryGetProperty("message_type", out var versionedType)
                    || root.TryGetProperty("contract", out _))
                {
                    object? descriptor = versionedType.ValueKind == JsonValueKind.String
                        ? versionedType.GetString() switch
                        {
                            "policy.snapshot" => (object)WireContractCatalog.PolicySnapshot,
                            "integrity.verdict" => WireContractCatalog.IntegrityVerdict,
                            "wns.hint" => WireContractCatalog.WnsHint,
                            "realtime.hint" => WireContractCatalog.RealtimeHint,
                            _ => null,
                        }
                        : null;
                    if (descriptor is ContractDescriptor<PolicySnapshotWire> policyDescriptor)
                    {
                        _ = WireContractCodec.DecodeAndValidate(
                            Encoding.UTF8.GetBytes(json), policyDescriptor).IsValid;
                    }
                    else if (descriptor is ContractDescriptor<IntegrityVerdictWire> integrityDescriptor)
                    {
                        _ = WireContractCodec.DecodeAndValidate(
                            Encoding.UTF8.GetBytes(json), integrityDescriptor).IsValid;
                    }
                    else if (descriptor is ContractDescriptor<HintWire> hintDescriptor)
                    {
                        _ = WireContractCodec.DecodeAndValidate(
                            Encoding.UTF8.GetBytes(json), hintDescriptor).IsValid;
                    }

                    return null;
                }

                if (!root.TryGetProperty("MessageType", out var typeElement)
                    || typeElement.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                var messageType = typeElement.GetString();

                if (messageType == nameof(GetRealtimeIdentity)
                    && (!root.TryGetProperty("ContractVersion", out var version)
                        || version.ValueKind != JsonValueKind.Number
                        || !version.TryGetInt32(out var contractVersion)
                        || contractVersion != 1
                        || !root.TryGetProperty("CorrelationId", out var correlation)
                        || correlation.ValueKind != JsonValueKind.String
                        || !Guid.TryParseExact(correlation.GetString(), "D", out var correlationId)
                        || correlationId == Guid.Empty
                        || !string.Equals(
                            correlation.GetString(),
                            correlationId.ToString("D"),
                            StringComparison.Ordinal)))
                {
                    return null;
                }

                if (messageType == nameof(RegisterWnsChannel)
                    && (!root.TryGetProperty("ChannelUri", out var channelUri)
                        || channelUri.ValueKind != JsonValueKind.String
                        || Encoding.UTF8.GetByteCount(channelUri.GetString() ?? string.Empty) > 2048))
                {
                    return null;
                }

                // T26 PR #14 — source-gen dispatch via UIMessagesJsonContext.
                // Each branch hands the JSON to JsonSerializer.Deserialize<T>
                // with the matching JsonTypeInfo from the source-gen context,
                // eliminating the reflection-based serializer at the IPC boundary.
                return messageType switch
                {
                    nameof(GetEnforcementLevel) => root.Deserialize(UIMessagesJsonContext.Default.GetEnforcementLevel),
                    nameof(EnforcementLevelResponse) => root.Deserialize(UIMessagesJsonContext.Default.EnforcementLevelResponse),
                    nameof(GetOnboardingState) => root.Deserialize(UIMessagesJsonContext.Default.GetOnboardingState),
                    nameof(OnboardingStateResponse) => root.Deserialize(UIMessagesJsonContext.Default.OnboardingStateResponse),
                    nameof(RecordOnboardingStepCompleted) => root.Deserialize(UIMessagesJsonContext.Default.RecordOnboardingStepCompleted),
                    nameof(StepCompletedResponse) => root.Deserialize(UIMessagesJsonContext.Default.StepCompletedResponse),
                    nameof(RecordFunnelEvent) => root.Deserialize(UIMessagesJsonContext.Default.RecordFunnelEvent),
                    nameof(ShowOverlayCommand) => root.Deserialize(UIMessagesJsonContext.Default.ShowOverlayCommand),
                    nameof(HideOverlayCommand) => root.Deserialize(UIMessagesJsonContext.Default.HideOverlayCommand),
                    nameof(GetUsageState) => root.Deserialize(UIMessagesJsonContext.Default.GetUsageState),
                    nameof(UsageStateResponse) => root.Deserialize(UIMessagesJsonContext.Default.UsageStateResponse),
                    nameof(TriggerSync) => root.Deserialize(UIMessagesJsonContext.Default.TriggerSync),
                    nameof(GetRealtimeIdentity) => root.Deserialize(UIMessagesJsonContext.Default.GetRealtimeIdentity),
                    nameof(PairDevice) => root.Deserialize(UIMessagesJsonContext.Default.PairDevice),
                    nameof(PairDeviceResponse) => root.Deserialize(UIMessagesJsonContext.Default.PairDeviceResponse),
                    nameof(ListAccounts) => root.Deserialize(UIMessagesJsonContext.Default.ListAccounts),
                    nameof(AccountList) => root.Deserialize(UIMessagesJsonContext.Default.AccountList),
                    nameof(CreateAccount) => root.Deserialize(UIMessagesJsonContext.Default.CreateAccount),
                    nameof(CreateAccountResponse) => root.Deserialize(UIMessagesJsonContext.Default.CreateAccountResponse),
                    nameof(ConvertAccount) => root.Deserialize(UIMessagesJsonContext.Default.ConvertAccount),
                    nameof(ConvertAccountResponse) => root.Deserialize(UIMessagesJsonContext.Default.ConvertAccountResponse),
                    nameof(GetServiceStatus) => root.Deserialize(UIMessagesJsonContext.Default.GetServiceStatus),
                    nameof(ServiceStatusResponse) => root.Deserialize(UIMessagesJsonContext.Default.ServiceStatusResponse),
                    nameof(GetConsentStatus) => root.Deserialize(UIMessagesJsonContext.Default.GetConsentStatus),
                    nameof(ConsentStatusSnapshot) => root.Deserialize(UIMessagesJsonContext.Default.ConsentStatusSnapshot),
                    nameof(GrantConsent) => root.Deserialize(UIMessagesJsonContext.Default.GrantConsent),
                    nameof(AdvanceOnboardingStep) => root.Deserialize(UIMessagesJsonContext.Default.AdvanceOnboardingStep),
                    nameof(ResetOnboardingState) => root.Deserialize(UIMessagesJsonContext.Default.ResetOnboardingState),
                    nameof(RegisterWnsChannel) => root.Deserialize(UIMessagesJsonContext.Default.RegisterWnsChannel),
                    nameof(WnsRegistrationResult) => root.Deserialize(UIMessagesJsonContext.Default.WnsRegistrationResult),
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
                this.IsReady = false;
                this.ClosePipe();
                this.disposed = true;
            }
        }

        private readonly Func<CancellationToken, Task<IUiPipeServer>> pipeFactory;
        private readonly Func<TimeSpan, CancellationToken, Task> delay;
        private readonly ILogger logger;

        private Task<IUiPipeServer> CreatePipeAsync(CancellationToken cancellationToken)
        {
            var pipeSecurity = CreatePipeSecurity(this.parentSid, this.childSid);
            var pipe = NamedPipeServerStreamAcl.Create(
                this.pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous,
                inBufferSize: BufferSize,
                outBufferSize: BufferSize,
                pipeSecurity: pipeSecurity,
                inheritability: HandleInheritability.None);
            return Task.FromResult<IUiPipeServer>(new UiPipeServer(pipe));
        }

        private void ClosePipe()
        {
            this.pipeServer?.Dispose();
            this.pipeServer = null;
            this.IsReady = false;
        }
    }

    internal interface IUiPipeServer : IDisposable
    {
        bool IsConnected { get; }
        bool IsMessageComplete => true;
        Task WaitForConnectionAsync(CancellationToken cancellationToken);
        Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken);
        Task WriteAsync(byte[] buffer, CancellationToken cancellationToken);
        SecurityIdentifier GetImpersonationUserSid();
    }

    private sealed class UiPipeServer : IUiPipeServer
    {
        private readonly NamedPipeServerStream pipe;

        public UiPipeServer(NamedPipeServerStream pipe) => this.pipe = pipe;
        public bool IsConnected => this.pipe.IsConnected;
        public bool IsMessageComplete => this.pipe.IsMessageComplete;
        public Task WaitForConnectionAsync(CancellationToken token) => this.pipe.WaitForConnectionAsync(token);
        public Task<int> ReadAsync(byte[] buffer, CancellationToken token) => this.pipe.ReadAsync(buffer, token).AsTask();
        public Task WriteAsync(byte[] buffer, CancellationToken token) => this.pipe.WriteAsync(buffer, token).AsTask();
        public SecurityIdentifier GetImpersonationUserSid() => this.pipe.GetImpersonationUserSid();
        public void Dispose() => this.pipe.Dispose();
    }
}
