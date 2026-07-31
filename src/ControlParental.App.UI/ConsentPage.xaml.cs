// <copyright file="ConsentPage.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using ControlParental.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T25/T26 — WinUI3 page for data disclosure and consent.
/// Shows disclosure text and requires affirmative action to proceed.
/// Fail-closed: if the IPC grant does not return acknowledged, the page
/// surfaces the error and DOES NOT advance the onboarding step
/// (audit finding #2 / DoD-G prohibition of empty catches).
/// </summary>
public sealed partial class ConsentPage : Page, INotifyPropertyChanged
{
    private readonly IConsentService? consentService;
    private readonly Action? onConsentGranted;
    private readonly Action? onTransparencyRequested;

    /// <summary>
    /// Raised when a bindable property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets a value indicating whether consent has been granted.
    /// </summary>
    public bool ConsentGranted { get; private set; }

    /// <summary>
    /// Event raised when consent is granted or changes.
    /// </summary>
    public event EventHandler<bool>? ConsentGrantedChanged;

    private bool isBusy;

    /// <summary>
    /// Gets a value indicating whether the page is currently persisting consent over IPC.
    /// Bind to the "Acepto" button's IsEnabled to prevent double-clicks.
    /// </summary>
    public bool IsBusy
    {
        get => this.isBusy;
        private set => this.SetField(ref this.isBusy, value);
    }

    private bool hasError;

    /// <summary>
    /// Gets a value indicating whether a persist error should be displayed.
    /// </summary>
    public bool HasError
    {
        get => this.hasError;
        private set => this.SetField(ref this.hasError, value);
    }

    private string errorMessage = string.Empty;

    /// <summary>
    /// Gets the user-facing error copy when <see cref="HasError"/> is true.
    /// </summary>
    public string ErrorMessage
    {
        get => this.errorMessage;
        private set => this.SetField(ref this.errorMessage, value);
    }

    /// <summary>
    /// Gets the typed resource accessor exposed to XAML. Used by
    /// <c>{x:Bind Strings.&lt;Key&gt;, Mode=OneTime}</c> to resolve
    /// localized copy without embedding literals in the markup
    /// (T26 Fase 8 / design.md §10, ADR-007).
    /// </summary>
    public StringsAdapter Strings => StringsAdapter.Instance;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentPage"/> class.
    /// </summary>
    public ConsentPage()
        : this(TryResolveConsentServiceFromDi(), null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentPage"/> class with live-shell callbacks.
    /// </summary>
    /// <param name="onConsentGranted">Callback invoked after acknowledged consent.</param>
    /// <param name="onTransparencyRequested">Callback that opens transparency without mutating onboarding state.</param>
    public ConsentPage(Action onConsentGranted, Action onTransparencyRequested)
        : this(
            TryResolveConsentServiceFromDi(),
            onConsentGranted ?? throw new ArgumentNullException(nameof(onConsentGranted)),
            onTransparencyRequested ?? throw new ArgumentNullException(nameof(onTransparencyRequested)))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentPage"/> class.
    /// </summary>
    /// <param name="consentService">Optional consent service for recording consent.</param>
    /// <param name="onConsentGranted">Optional callback invoked when consent is granted.</param>
    public ConsentPage(IConsentService? consentService, Action? onConsentGranted)
        : this(consentService, onConsentGranted, null)
    {
    }

    private ConsentPage(
        IConsentService? consentService,
        Action? onConsentGranted,
        Action? onTransparencyRequested)
    {
        this.consentService = consentService;
        this.onConsentGranted = onConsentGranted;
        this.onTransparencyRequested = onTransparencyRequested;
        this.InitializeComponent();
    }

    /// <summary>
    /// Handles the Accept button click.
    /// </summary>
    private async void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        await this.GrantAndCloseAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Handles the View Transparency button click.
    /// </summary>
    private void OnViewTransparencyClick(object sender, RoutedEventArgs e)
    {
        this.onTransparencyRequested?.Invoke();
    }

    private async Task GrantAndCloseAsync()
    {
        this.HasError = false;
        this.ErrorMessage = string.Empty;
        this.IsBusy = true;
        var dispatcherQueue = this.DispatcherQueue;

        var result = await this.PersistConsentAsync().ConfigureAwait(false);
        await this.ApplyGrantResultOnUiThreadAsync(dispatcherQueue, result).ConfigureAwait(false);
    }

    private async Task<ConsentGrantResult> PersistConsentAsync()
    {
        if (this.consentService is null)
        {
            return new ConsentGrantResult(false, null);
        }

        try
        {
            await this.consentService.GrantConsentAsync(null).ConfigureAwait(false);
            return new ConsentGrantResult(true, null);
        }
        catch (Exception ex)
        {
            return new ConsentGrantResult(false, ex.GetType().Name);
        }
    }

    private Task ApplyGrantResultOnUiThreadAsync(DispatcherQueue dispatcherQueue, ConsentGrantResult result)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return this.ApplyGrantResultAsync(result);
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await this.ApplyGrantResultAsync(result).ConfigureAwait(true);
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }))
        {
            completion.SetException(new InvalidOperationException("Unable to apply consent state on the UI thread."));
        }

        return completion.Task;
    }

    private Task ApplyGrantResultAsync(ConsentGrantResult result)
    {
        this.IsBusy = false;

        if (!result.Succeeded)
        {
            this.HasError = true;
            this.ErrorMessage = result.ErrorTypeName is null
                ? this.Strings.ConsentPersistFailedShort
                : $"{this.Strings.ConsentRecordFailure} ({result.ErrorTypeName})";
            return Task.CompletedTask;
        }

        this.ConsentGranted = true;
        this.onConsentGranted?.Invoke();
        this.ConsentGrantedChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }

    private static IConsentService? TryResolveConsentServiceFromDi()
    {
        try
        {
            return App.Services.GetService<IConsentService>();
        }
        catch
        {
            return null;
        }
    }

    private readonly record struct ConsentGrantResult(bool Succeeded, string? ErrorTypeName);
}
