// <copyright file="NamedPipeUIServerHostedAdapter.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Interop;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// P0 — Hosted lifecycle owner for <see cref="NamedPipeUIServer"/>.
/// Bridges the pipe server into <see cref="IHostedService"/> so the
/// generic host can start the listener when the Service comes online
/// and stop it gracefully on shutdown.
///
/// The pipe listener loop
/// (<see cref="NamedPipeUIServer"/>.StartAsync → PipeServerListener.WaitForConnectionAsync)
/// blocks until cancellation is signalled, so this adapter moves the
/// start call to a background task and returns immediately from
/// <see cref="StartAsync"/>. The cancellation chain — host stopping
/// token, internal CTS, and the inner server's stop method — is
/// defensive: every entry point is idempotent and exceptions from the
/// run task are swallowed into a log entry so shutdown can complete
/// even if the listener process is in a degraded state.
///
/// <para>
/// <see cref="StopAsync"/> waits for the listener run task to actually
/// drain before returning, bounded by a 5-second drain ceiling. We do
/// not pass the host stopping token directly to the wait because an
/// already-cancelled stopping token (a forced shutdown) would
/// otherwise return immediately without observing whether the listener
/// finished; instead, the host stopping token is linked as an
/// early-exit signal when it is still active.
/// </para>
/// </summary>
public sealed class NamedPipeUIServerHostedAdapter : IHostedService, IDisposable
{
    private readonly NamedPipeUIServer server;
    private readonly UIMessageHandler messageHandler;
    private readonly ILogger<NamedPipeUIServerHostedAdapter> logger;
    private readonly CancellationTokenSource internalCts = new();
    private Task? runTask;
    private int stopped;
    private int disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeUIServerHostedAdapter"/> class.
    /// </summary>
    /// <param name="server">The pipe server to host.</param>
    /// <param name="messageHandler">The message handler used by the listener loop.</param>
    /// <param name="logger">Logger for shutdown errors and lifecycle diagnostics.</param>
    public NamedPipeUIServerHostedAdapter(
        NamedPipeUIServer server,
        UIMessageHandler messageHandler,
        ILogger<NamedPipeUIServerHostedAdapter> logger)
    {
        this.server = server ?? throw new ArgumentNullException(nameof(server));
        this.messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // The pipe server's listener loop blocks on WaitForConnectionAsync
        // until cancellation is signalled. Move it to a background task so
        // the host can keep starting the rest of the pipeline without
        // waiting on the pipe to receive a connection.
        this.runTask = Task.Run(
            async () =>
            {
                try
                {
                    await this.server
                        .StartAsync(this.messageHandler, this.internalCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when internalCts (or the host stopping token)
                    // is signalled during shutdown.
                }
                catch (Exception ex)
                {
                    this.logger.LogError(
                        ex,
                        "[NamedPipeUIServerHostedAdapter] Listener loop exited with an unhandled error.");
                }
            },
            CancellationToken.None);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Idempotent guard: only the first call cancels the internal loop
        // and waits for the run task to drain. Subsequent calls are no-ops
        // so the host can shut down multiple hosted services in any order
        // without double-cancelling the listener.
        if (Interlocked.Exchange(ref this.stopped, 1) != 0)
        {
            return;
        }

        // Tell the inner server to stop accepting / processing first; the
        // underlying internalCts.Cancel() inside NamedPipeUIServer.StopAsync()
        // is what unblocks WaitForConnectionAsync. Wrap in try/catch so we
        // never propagate a transient shutdown error up to the host.
        try
        {
            await this.server.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(
                ex,
                "[NamedPipeUIServerHostedAdapter] Inner server StopAsync threw.");
        }

        this.internalCts.Cancel();

        if (this.runTask is { } task)
        {
            // Observe the run task to actual completion before returning. We
            // deliberately do NOT pass cancellationToken to WaitAsync: an
            // already-cancelled host stopping token would cause WaitAsync to
            // throw OperationCanceledException immediately and leave the
            // listener still draining. Instead, we wait with a bounded drain
            // budget and link the host stopping token as an early-exit signal
            // — but only when the token is still active, since registering an
            // already-cancelled token would fire the callback immediately and
            // defeat the bounded drain.
            using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var registration = cancellationToken.IsCancellationRequested
                ? default
                : cancellationToken.Register(
                    static state => ((CancellationTokenSource)state!).Cancel(),
                    drainCts);

            try
            {
                await task.WaitAsync(drainCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                this.logger.LogWarning(
                    "[NamedPipeUIServerHostedAdapter] Listener task did not drain before the shutdown drain budget expired.");
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "[NamedPipeUIServerHostedAdapter] Listener run task ended with an unhandled error.");
            }
            finally
            {
                registration.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        // Best-effort drain if a test (or caller) disposed without calling
        // StopAsync first — we must not free internalCts while the run task
        // is still observing its token.
        if (this.runTask is { } task && !task.IsCompleted)
        {
            try
            {
                this.internalCts.Cancel();
                task.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Best-effort cleanup; do not propagate from Dispose.
            }
        }

        this.internalCts.Dispose();
        this.server.Dispose();
    }

    /// <summary>
    /// Gets a value indicating whether test-only accessor: returns <c>true</c> when the listener run task has
    /// been observed to complete, or <c>true</c> when no run task has been
    /// started yet. Used by lifecycle tests to prove that
    /// <see cref="StopAsync"/> actually waits for the listener to drain,
    /// including when the host stopping token is already cancelled.
    /// </summary>
    internal bool IsRunTaskCompleted => this.runTask?.IsCompleted ?? true;
}
