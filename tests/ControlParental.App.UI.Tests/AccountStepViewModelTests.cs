// <copyright file="AccountStepViewModelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI.Interop;
using Xunit;
using AppUI = ControlParental.App.UI;

public sealed class AccountStepViewModelTests
{
    [Fact]
    public async Task LoadAccountsAsyncOnSuccessPopulatesAccountsList()
    {
        var channel = new RecordingUIChannel(query => new AppUI.AccountList(
            new[]
            {
                new AppUI.AccountInfo("Child", "Standard", true),
                new AppUI.AccountInfo("Parent", "Administrator", false),
            }));
        var viewModel = new AppUI.AccountStepViewModel(uiChannel: channel);

        await viewModel.LoadAccountsAsync().ConfigureAwait(false);

        Assert.Equal(2, viewModel.Accounts.Count);
        Assert.Equal(AppUI.AccountType.Standard, viewModel.Accounts[0].Type);
        Assert.Equal(AppUI.AccountType.Administrator, viewModel.Accounts[1].Type);
    }

    [Fact]
    public async Task LoadAccountsAsyncUsesListAccountsMessage()
    {
        var channel = new RecordingUIChannel(
            _ => new AppUI.AccountList(Array.Empty<AppUI.AccountInfo>()));
        var viewModel = new AppUI.AccountStepViewModel(uiChannel: channel);

        await viewModel.LoadAccountsAsync().ConfigureAwait(false);

        Assert.Contains(channel.QueriedMessages, message => message is AppUI.ListAccounts);
    }

    [Fact]
    public async Task LoadAccountsAsyncOnEmptyDoesNotShowFakeAccount()
    {
        var channel = new RecordingUIChannel(
            _ => new AppUI.AccountList(Array.Empty<AppUI.AccountInfo>()));
        var viewModel = new AppUI.AccountStepViewModel(uiChannel: channel);

        await viewModel.LoadAccountsAsync().ConfigureAwait(false);

        Assert.Empty(viewModel.Accounts);
        Assert.DoesNotContain(viewModel.Accounts, account => account.Username == "UsuarioTest");
    }

    [Fact]
    public async Task AccountStepViewModelRequiresElevationMessageUsesStringsAdapter()
    {
        var channel = new RecordingUIChannel(query => query switch
        {
            AppUI.ListAccounts => new AppUI.AccountList(Array.Empty<AppUI.AccountInfo>()),
            AppUI.CreateAccount => new AppUI.CreateAccountResponse(false, "Elevation needed", true),
            _ => null,
        });
        var viewModel = new AppUI.AccountStepViewModel(uiChannel: channel)
        {
            NewAccountUsername = "Child",
            NewAccountPassword = "password",
        };

        await viewModel.CreateAccountCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.True(viewModel.RequiresElevation);
        Assert.Equal(AppUI.StringsAdapter.Instance.AccountElevationRequired, viewModel.ErrorMessage);
    }
}

internal sealed class RecordingUIChannel : IUIChannel
{
    private readonly Func<object, object?> queryHandler;

    public RecordingUIChannel(Func<object, object?>? queryHandler = null)
    {
        this.queryHandler = queryHandler ?? (_ => null);
    }

    public Exception? SendException { get; init; }

    public List<object> QueriedMessages { get; } = new();

    public List<object> SentMessages { get; } = new();

    public Task<TResponse?> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken ct = default)
        where TQuery : ControlParental.Domain.IUIMessage
        where TResponse : class, ControlParental.Domain.IUIMessage
    {
        this.QueriedMessages.Add(query!);
        return Task.FromResult(this.queryHandler(query!) as TResponse);
    }

    public Task SendAsync<T>(T message, CancellationToken ct = default)
        where T : ControlParental.Domain.IUIMessage
    {
        if (this.SendException is not null)
        {
            throw this.SendException;
        }

        this.SentMessages.Add(message!);
        return Task.CompletedTask;
    }
}
