// <copyright file="AccountStepViewModel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;

/// <summary>
/// T26 — ViewModel for the account step.
/// Reads and mutates Windows accounts through the Service IPC channel.
/// </summary>
public sealed partial class AccountStepViewModel : ObservableObject
{
    private readonly Action? onAccountCompleted;
    private readonly SynchronizationContext? synchronizationContext;
    private readonly IUIChannel? uiChannel;

    [ObservableProperty]
    private ObservableCollection<AccountItem> accounts = new();

    [ObservableProperty]
    private bool isLoading = true;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool requiresElevation;

    [ObservableProperty]
    private bool showCreateForm;

    [ObservableProperty]
    private bool showConvertForm;

    [ObservableProperty]
    private string newAccountUsername = string.Empty;

    [ObservableProperty]
    private string newAccountPassword = string.Empty;

    [ObservableProperty]
    private string selectedAccountName = string.Empty;

    [ObservableProperty]
    private bool isProcessing;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountStepViewModel"/> class.
    /// </summary>
    /// <param name="onAccountCompleted">Callback when account step completes.</param>
    /// <param name="uiChannel">IPC channel to the Service.</param>
    public AccountStepViewModel(
        Action? onAccountCompleted = null,
        IUIChannel? uiChannel = null)
    {
        this.onAccountCompleted = onAccountCompleted;
        this.synchronizationContext = SynchronizationContext.Current;
        this.uiChannel = uiChannel;
        _ = this.LoadAccountsAsync();
    }

    /// <summary>
    /// Loads the list of existing OS accounts from the Service.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task LoadAccountsAsync(CancellationToken ct = default)
    {
        var uiContext = this.synchronizationContext ?? SynchronizationContext.Current;
        await this.RunOnUiThreadAsync(uiContext, this.BeginAccountLoad).ConfigureAwait(false);

        try
        {
            var result = await this.QueryAccountsAsync(ct).ConfigureAwait(false);
            await this.ApplyAccountLoadResultOnUiThreadAsync(uiContext, result).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await this.RunOnUiThreadAsync(
                uiContext,
                () => this.IsLoading = false).ConfigureAwait(false);
            throw;
        }
    }

    private void BeginAccountLoad()
    {
        this.IsLoading = true;
        this.HasError = false;
        this.ErrorMessage = string.Empty;
    }

    private async Task<AccountLoadResult> QueryAccountsAsync(CancellationToken ct)
    {
        if (this.uiChannel is null)
        {
            return new AccountLoadResult(null, null);
        }

        try
        {
            var response = await this.uiChannel.QueryAsync<ListAccounts, AccountList>(
                new ListAccounts(),
                ct).ConfigureAwait(false);

            if (response is null)
            {
                return new AccountLoadResult(null, null);
            }

            var accounts = response.Accounts
                .Select(account => new AccountItem(
                    account.Username,
                    this.MapAccountType(account.Type),
                    account.IsStandard))
                .ToArray();

            return new AccountLoadResult(accounts, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new AccountLoadResult(null, $"Error cargando cuentas: {ex.Message}");
        }
    }

    private Task ApplyAccountLoadResultOnUiThreadAsync(
        SynchronizationContext? uiContext,
        AccountLoadResult result)
    {
        return this.RunOnUiThreadAsync(
            uiContext,
            () => this.ApplyAccountLoadResult(result));
    }

    private void ApplyAccountLoadResult(AccountLoadResult result)
    {
        try
        {
            if (result.Accounts is null)
            {
                if (result.ErrorMessage is null)
                {
                    this.SetServiceUnavailable();
                }
                else
                {
                    this.HasError = true;
                    this.ErrorMessage = result.ErrorMessage;
                }

                return;
            }

            this.Accounts.Clear();
            foreach (var account in result.Accounts)
            {
                this.Accounts.Add(account);
            }
        }
        finally
        {
            this.IsLoading = false;
        }
    }

    private Task RunOnUiThreadAsync(SynchronizationContext? uiContext, Action action)
    {
        if (uiContext is null || ReferenceEquals(uiContext, SynchronizationContext.Current))
        {
            action();
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
                        action();
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

    /// <summary>
    /// Creates a new standard account through the Service.
    /// </summary>
    [RelayCommand]
    private async Task CreateAccountAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(this.NewAccountUsername))
        {
            this.SetValidationError("Ingresá un nombre de usuario.");
            return;
        }

        if (string.IsNullOrWhiteSpace(this.NewAccountPassword))
        {
            this.SetValidationError("Ingresá una contraseña.");
            return;
        }

        this.IsProcessing = true;
        this.HasError = false;
        this.ErrorMessage = string.Empty;
        this.RequiresElevation = false;

        var uiContext = this.synchronizationContext ?? SynchronizationContext.Current;

        try
        {
            var username = this.NewAccountUsername;
            var password = this.NewAccountPassword;

            var result = await this.CreateAccountCoreAsync(username, password, ct).ConfigureAwait(false);
            await this.ApplyCreateAccountResultOnUiThreadAsync(uiContext, result, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await this.RunOnUiThreadAsync(
                uiContext,
                () => this.IsProcessing = false).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await this.RunOnUiThreadAsync(
                uiContext,
                () =>
                {
                    this.HasError = true;
                    this.ErrorMessage = $"Error creando cuenta: {ex.Message}";
                    this.IsProcessing = false;
                }).ConfigureAwait(false);
        }
    }

    private async Task<CreateAccountOutcome> CreateAccountCoreAsync(
        string username,
        string password,
        CancellationToken ct)
    {
        if (this.uiChannel is null)
        {
            return new CreateAccountOutcome(CreateAccountStatus.ServiceUnavailable, null);
        }

        try
        {
            var response = await this.uiChannel.QueryAsync<CreateAccount, CreateAccountResponse>(
                new CreateAccount(username, password),
                ct).ConfigureAwait(false);

            if (response is null)
            {
                return new CreateAccountOutcome(CreateAccountStatus.ServiceUnavailable, null);
            }

            if (response.RequiresElevation)
            {
                return new CreateAccountOutcome(CreateAccountStatus.RequiresElevation, null);
            }

            if (!response.Success)
            {
                return new CreateAccountOutcome(
                    CreateAccountStatus.GeneralFailure,
                    response.ErrorMessage ?? "No pudimos crear la cuenta.");
            }

            return new CreateAccountOutcome(CreateAccountStatus.Succeeded, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new CreateAccountOutcome(CreateAccountStatus.ServiceUnavailable, null);
        }
    }

    private Task ApplyCreateAccountResultOnUiThreadAsync(
        SynchronizationContext? uiContext,
        CreateAccountOutcome outcome,
        CancellationToken ct)
    {
        return this.RunOnUiThreadAsync(
            uiContext,
            () => this.ApplyCreateAccountResult(outcome, ct));
    }

    private void ApplyCreateAccountResult(CreateAccountOutcome outcome, CancellationToken ct)
    {
        try
        {
            switch (outcome.Status)
            {
                case CreateAccountStatus.ServiceUnavailable:
                    this.SetServiceUnavailable();
                    return;

                case CreateAccountStatus.RequiresElevation:
                    this.RequiresElevation = true;
                    this.HasError = true;
                    this.ErrorMessage = StringsAdapter.Instance.AccountElevationRequired;
                    return;

                case CreateAccountStatus.GeneralFailure:
                    this.HasError = true;
                    this.ErrorMessage = outcome.FailureMessage ?? "No pudimos crear la cuenta.";
                    return;

                case CreateAccountStatus.Succeeded:
                    this.NewAccountUsername = string.Empty;
                    this.NewAccountPassword = string.Empty;
                    this.ShowCreateForm = false;
                    this.onAccountCompleted?.Invoke();
                    return;
            }
        }
        finally
        {
            this.IsProcessing = false;
        }
    }

    /// <summary>
    /// Converts an existing administrator account through the Service.
    /// </summary>
    [RelayCommand]
    private async Task ConvertAccountAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(this.SelectedAccountName))
        {
            this.SetValidationError("Seleccioná una cuenta para convertir.");
            return;
        }

        this.IsProcessing = true;
        this.HasError = false;
        this.ErrorMessage = string.Empty;

        var uiContext = this.synchronizationContext ?? SynchronizationContext.Current;

        try
        {
            var accountName = this.SelectedAccountName;

            var result = await this.ConvertAccountCoreAsync(accountName, ct).ConfigureAwait(false);
            await this.ApplyConvertAccountResultOnUiThreadAsync(uiContext, result, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await this.RunOnUiThreadAsync(
                uiContext,
                () => this.IsProcessing = false).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await this.RunOnUiThreadAsync(
                uiContext,
                () =>
                {
                    this.HasError = true;
                    this.ErrorMessage = $"Error convirtiendo cuenta: {ex.Message}";
                    this.IsProcessing = false;
                }).ConfigureAwait(false);
        }
    }

    private async Task<ConvertAccountOutcome> ConvertAccountCoreAsync(
        string accountName,
        CancellationToken ct)
    {
        if (this.uiChannel is null)
        {
            return new ConvertAccountOutcome(ConvertAccountStatus.ServiceUnavailable, null);
        }

        try
        {
            var response = await this.uiChannel.QueryAsync<ConvertAccount, ConvertAccountResponse>(
                new ConvertAccount(accountName),
                ct).ConfigureAwait(false);

            if (response is null)
            {
                return new ConvertAccountOutcome(ConvertAccountStatus.ServiceUnavailable, null);
            }

            if (!response.Success)
            {
                return new ConvertAccountOutcome(
                    ConvertAccountStatus.GeneralFailure,
                    response.ErrorMessage ?? "No pudimos convertir la cuenta.");
            }

            return new ConvertAccountOutcome(ConvertAccountStatus.Succeeded, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new ConvertAccountOutcome(ConvertAccountStatus.ServiceUnavailable, null);
        }
    }

    private Task ApplyConvertAccountResultOnUiThreadAsync(
        SynchronizationContext? uiContext,
        ConvertAccountOutcome outcome,
        CancellationToken ct)
    {
        return this.RunOnUiThreadAsync(
            uiContext,
            () => this.ApplyConvertAccountResult(outcome, ct));
    }

    private void ApplyConvertAccountResult(ConvertAccountOutcome outcome, CancellationToken ct)
    {
        try
        {
            switch (outcome.Status)
            {
                case ConvertAccountStatus.ServiceUnavailable:
                    this.SetServiceUnavailable();
                    return;

                case ConvertAccountStatus.GeneralFailure:
                    this.HasError = true;
                    this.ErrorMessage = outcome.FailureMessage ?? "No pudimos convertir la cuenta.";
                    return;

                case ConvertAccountStatus.Succeeded:
                    this.ShowConvertForm = false;
                    this.onAccountCompleted?.Invoke();
                    return;
            }
        }
        finally
        {
            this.IsProcessing = false;
        }
    }

    /// <summary>
    /// Uses an existing standard account as the child account.
    /// </summary>
    [RelayCommand]
    private void UseAccount(AccountItem? account)
    {
        if (account == null)
        {
            return;
        }

        this.SelectedAccountName = account.Username;
        this.onAccountCompleted?.Invoke();
    }

    /// <summary>
    /// Shows the create account form.
    /// </summary>
    [RelayCommand]
    private void ShowCreate()
    {
        this.ShowCreateForm = true;
        this.ShowConvertForm = false;
    }

    /// <summary>
    /// Shows the convert account form.
    /// </summary>
    [RelayCommand]
    private void ShowConvert()
    {
        this.ShowConvertForm = true;
        this.ShowCreateForm = false;
    }

    /// <summary>
    /// Cancels the current form.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        this.ShowCreateForm = false;
        this.ShowConvertForm = false;
        this.NewAccountUsername = string.Empty;
        this.NewAccountPassword = string.Empty;
        this.RequiresElevation = false;
        this.HasError = false;
        this.ErrorMessage = string.Empty;
    }

    private AccountType MapAccountType(string type)
    {
        return Enum.TryParse<AccountType>(type, ignoreCase: true, out var accountType)
            ? accountType
            : AccountType.Unknown;
    }

    private void SetServiceUnavailable()
    {
        this.HasError = true;
        this.ErrorMessage = "No pudimos conectar con el servicio. Intentá de nuevo.";
    }

    private void SetValidationError(string message)
    {
        this.HasError = true;
        this.ErrorMessage = message;
        this.RequiresElevation = false;
    }

    private sealed record AccountLoadResult(
        IReadOnlyList<AccountItem>? Accounts,
        string? ErrorMessage);

    private enum CreateAccountStatus
    {
        Succeeded,
        ServiceUnavailable,
        RequiresElevation,
        GeneralFailure,
    }

    private sealed record CreateAccountOutcome(
        CreateAccountStatus Status,
        string? FailureMessage);

    private enum ConvertAccountStatus
    {
        Succeeded,
        ServiceUnavailable,
        GeneralFailure,
    }

    private sealed record ConvertAccountOutcome(
        ConvertAccountStatus Status,
        string? FailureMessage);
}

/// <summary>
/// Represents an account item in the list.
/// </summary>
public sealed class AccountItem
{
    /// <inheritdoc/>
    public string Username { get; }

    /// <inheritdoc/>
    public AccountType Type { get; }

    /// <inheritdoc/>
    public bool IsStandard { get; }

    /// <inheritdoc/>
    public string TypeDisplayName => $"Tipo: {this.Type}";

    /// <inheritdoc/>
    public AccountItem(string username, AccountType type, bool isStandard)
    {
        this.Username = username;
        this.Type = type;
        this.IsStandard = isStandard;
    }
}

/// <summary>
/// Type of account.
/// </summary>
public enum AccountType
{
    /// <inheritdoc/>
    Standard,
    /// <inheritdoc/>
    Administrator,
    /// <inheritdoc/>
    Unknown,
}
