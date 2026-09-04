// <copyright file="WnsRegistrationViewModel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlParental.Domain;

/// <summary>
/// Narrow application seam for registering a WNS channel through the Service.
/// </summary>
public interface IWnsRegistrationPort
{
    /// <summary>Registers a channel intent and returns its typed outcome.</summary>
    Task<WnsRegistrationResult?> RegisterChannelAsync(
        string channelUri,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
}

/// <summary>Safe, typed states rendered by the WNS registration UI.</summary>
public enum WnsRegistrationDisplayStatus
{
    Idle,
    Accepted,
    PendingOffline,
    Denied,
    Retryable,
    Unavailable,
}

/// <summary>Application view model for the WNS registration action.</summary>
public sealed class WnsRegistrationViewModel : ObservableObject
{
    private const string IdleText = "Not registered";
    private const string AcceptedText = "Registration accepted";
    private const string PendingOfflineText = "Registration pending";
    private const string DeniedText = "Registration denied";
    private const string RetryableText = "Registration can be retried";
    private const string UnavailableText = "Registration unavailable";

    private readonly IWnsRegistrationPort port;
    private readonly IWnsChannelProvider? channelProvider;
    private int registrationInProgress;
    private WnsRegistrationDisplayStatus status = WnsRegistrationDisplayStatus.Idle;
    private bool isBusy;

    public WnsRegistrationViewModel(IWnsRegistrationPort port)
    {
        this.port = port ?? throw new ArgumentNullException(nameof(port));
    }

    public WnsRegistrationViewModel(IWnsRegistrationPort port, IWnsChannelProvider channelProvider)
        : this(port)
    {
        this.channelProvider = channelProvider ?? throw new ArgumentNullException(nameof(channelProvider));
    }

    public WnsRegistrationDisplayStatus Status
    {
        get => this.status;
        private set => this.SetProperty(ref this.status, value);
    }

    public string StatusText => this.Status switch
    {
        WnsRegistrationDisplayStatus.Accepted => AcceptedText,
        WnsRegistrationDisplayStatus.PendingOffline => PendingOfflineText,
        WnsRegistrationDisplayStatus.Denied => DeniedText,
        WnsRegistrationDisplayStatus.Retryable => RetryableText,
        WnsRegistrationDisplayStatus.Unavailable => UnavailableText,
        _ => IdleText,
    };

    public bool IsBusy
    {
        get => this.isBusy;
        private set => this.SetProperty(ref this.isBusy, value);
    }

    public bool CanRegister => !this.IsBusy;

    public async Task RegisterAsync(string channelUri, CancellationToken cancellationToken = default)
    {
        await this.ExecuteRegistrationAsync(
            () => Task.FromResult<WnsChannelIntent?>(
                new WnsChannelIntent(channelUri, DateTimeOffset.UtcNow.AddDays(30))),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RegisterAsync(CancellationToken cancellationToken = default)
    {
        await this.ExecuteRegistrationAsync(
            () => this.channelProvider!.CreateChannelAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteRegistrationAsync(
        Func<Task<WnsChannelIntent?>> acquireIntent,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref this.registrationInProgress, 1) != 0)
        {
            return;
        }

        this.IsBusy = true;
        this.OnPropertyChanged(nameof(this.CanRegister));
        try
        {
            var intent = await acquireIntent().ConfigureAwait(false);
            if (intent is null)
            {
                this.SetUnavailable();
                return;
            }

            await this.RegisterIntentAsync(intent, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            this.SetUnavailable();
        }
        finally
        {
            this.SetBusy(false);
            Volatile.Write(ref this.registrationInProgress, 0);
        }
    }

    private async Task RegisterIntentAsync(WnsChannelIntent intent, CancellationToken cancellationToken)
    {
        var result = await this.port.RegisterChannelAsync(
            intent.Uri,
            intent.ExpiresAt,
            cancellationToken).ConfigureAwait(false);

        this.Status = result is null
            ? WnsRegistrationDisplayStatus.Unavailable
            : result.Status switch
            {
                WnsRegistrationStatus.Accepted => WnsRegistrationDisplayStatus.Accepted,
                WnsRegistrationStatus.PendingOffline => WnsRegistrationDisplayStatus.PendingOffline,
                WnsRegistrationStatus.Denied => WnsRegistrationDisplayStatus.Denied,
                WnsRegistrationStatus.Retryable => WnsRegistrationDisplayStatus.Retryable,
                _ => WnsRegistrationDisplayStatus.Unavailable,
            };
        this.OnPropertyChanged(nameof(this.StatusText));
    }

    private void SetUnavailable()
    {
        this.Status = WnsRegistrationDisplayStatus.Unavailable;
        this.OnPropertyChanged(nameof(this.StatusText));
    }

    private void SetBusy(bool value)
    {
        this.IsBusy = value;
        this.OnPropertyChanged(nameof(this.CanRegister));
    }
}
