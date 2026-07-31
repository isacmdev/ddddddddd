// <copyright file="DemoStepViewModel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;

/// <summary>
/// T26 — ViewModel for the demo overlay step.
/// Routes the demo through the Service and SessionAgent IPC channels.
/// </summary>
public sealed partial class DemoStepViewModel : ObservableObject
{
    private readonly IUIChannel? uiChannel;
    private readonly Action? onDemoCompleted;
    private readonly SynchronizationContext? synchronizationContext;
    private readonly TimeSpan overlayDuration;

    [ObservableProperty]
    private bool isDemoRunning;

    [ObservableProperty]
    private string countdownText = "3";

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoStepViewModel"/> class.
    /// </summary>
    /// <param name="uiChannel">IPC channel for sending overlay and funnel commands.</param>
    /// <param name="onDemoCompleted">Callback when the demo succeeds.</param>
    /// <param name="overlayDuration">Visible duration; defaults to three seconds.</param>
    public DemoStepViewModel(
        IUIChannel? uiChannel = null,
        Action? onDemoCompleted = null,
        TimeSpan? overlayDuration = null)
    {
        this.uiChannel = uiChannel;
        this.onDemoCompleted = onDemoCompleted;
        this.synchronizationContext = SynchronizationContext.Current;
        this.overlayDuration = overlayDuration ?? TimeSpan.FromSeconds(3);
    }

    /// <summary>
    /// Shows the SessionAgent overlay, waits for the demo window, hides it, and
    /// records the first-win funnel event. Completion is emitted only after all
    /// three IPC operations succeed.
    /// </summary>
    [RelayCommand]
    private async Task RunDemoAsync(CancellationToken ct = default)
    {
        var uiContext = this.synchronizationContext ?? SynchronizationContext.Current;
        this.ApplyBeginOnUiThread(uiContext);

        DemoStepOutcome outcome;
        try
        {
            outcome = await this.RunDemoCoreAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            outcome = new DemoStepOutcome(false, "La demostración fue cancelada.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            outcome = new DemoStepOutcome(false, $"No pudimos ejecutar la demostración: {ex.Message}");
        }

        await this.ApplyOutcomeOnUiThreadAsync(uiContext, outcome).ConfigureAwait(false);
    }

    private void ApplyBeginOnUiThread(SynchronizationContext? uiContext)
    {
        if (ReferenceEquals(uiContext, SynchronizationContext.Current))
        {
            this.IsDemoRunning = true;
            this.IsCompleted = false;
            this.HasError = false;
            this.ErrorMessage = string.Empty;
            this.CountdownText = "3";
            return;
        }

        uiContext.Post(
            _ =>
            {
                this.IsDemoRunning = true;
                this.IsCompleted = false;
                this.HasError = false;
                this.ErrorMessage = string.Empty;
                this.CountdownText = "3";
            },
            null);
    }

    private async Task<DemoStepOutcome> RunDemoCoreAsync(CancellationToken ct)
    {
        if (this.uiChannel is null)
        {
            throw new InvalidOperationException("IPC channel missing for demo");
        }

        await this.uiChannel.SendAsync(
            new ShowOverlayCommand("demo"),
            ct).ConfigureAwait(false);

        await Task.Delay(this.overlayDuration, ct).ConfigureAwait(false);

        await this.uiChannel.SendAsync(new HideOverlayCommand(), ct).ConfigureAwait(false);
        await this.uiChannel.SendAsync(
            new RecordFunnelEvent(FunnelEventType.OnboardingFirstWin.ToString()),
            ct).ConfigureAwait(false);

        return new DemoStepOutcome(true, null);
    }

    private Task ApplyOutcomeOnUiThreadAsync(
        SynchronizationContext? uiContext,
        DemoStepOutcome outcome)
    {
        if (ReferenceEquals(uiContext, SynchronizationContext.Current))
        {
            this.ApplyOutcome(outcome);
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            uiContext.Post(
                _ =>
                {
                    try
                    {
                        this.ApplyOutcome(outcome);
                        completion.SetResult();
                    }
                    catch (Exception ex)
                    {
                        completion.SetException(ex);
                    }
                },
                null);
        }
        catch (Exception ex)
        {
            completion.SetException(ex);
        }

        return completion.Task;
    }

    private void ApplyOutcome(DemoStepOutcome outcome)
    {
        try
        {
            if (outcome.Succeeded)
            {
                this.CountdownText = "0";
                this.IsCompleted = true;
                this.onDemoCompleted?.Invoke();
                return;
            }

            this.HasError = true;
            this.ErrorMessage = outcome.FailureMessage
                ?? "No pudimos ejecutar la demostración.";
        }
        finally
        {
            this.IsDemoRunning = false;
        }
    }

    private sealed record DemoStepOutcome(bool Succeeded, string? FailureMessage);
}
