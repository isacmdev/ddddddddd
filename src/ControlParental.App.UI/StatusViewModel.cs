// <copyright file="StatusViewModel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlParental.Domain;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T27 — ViewModel for the child's status screen.
/// Shows time remaining, current app, active grants, and enforcement issues.
/// </summary>
public sealed partial class StatusViewModel : ObservableObject
{
    private readonly IUIPipeClient uiPipeClient;
    private readonly IRealtimeSubscriber realtimeSubscriber;

    [ObservableProperty]
    private int? minutesRemaining;

    [ObservableProperty]
    private string? currentAppId;

    [ObservableProperty]
    private bool isPaused;

    [ObservableProperty]
    private string statusSummary = "Cargando...";

    [ObservableProperty]
    private bool hasActiveIssues;

    [ObservableProperty]
    private EnforcementLevel currentLevel = EnforcementLevel.Unknown;

    public ObservableCollection<GrantInfo> ActiveGrants { get; } = new();

    public ObservableCollection<ActiveIssue> ActiveIssues { get; } = new();

    public StatusViewModel(IUIPipeClient uiPipeClient, IRealtimeSubscriber realtimeSubscriber)
    {
        this.uiPipeClient = uiPipeClient ?? throw new ArgumentNullException(nameof(uiPipeClient));
        this.realtimeSubscriber = realtimeSubscriber ?? throw new ArgumentNullException(nameof(realtimeSubscriber));
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        this.realtimeSubscriber.PolicyChanged += this.OnPolicyOrGrantsChanged;
        this.realtimeSubscriber.GrantsChanged += this.OnPolicyOrGrantsChanged;
        await this.RefreshAsync(ct);
    }

    private async void OnPolicyOrGrantsChanged(object? sender, EventArgs e)
    {
        await this.RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct = default)
    {
        var state = await this.uiPipeClient.GetUsageStateAsync(ct);

        this.MinutesRemaining = state.MinutesRemaining;
        this.CurrentAppId = state.CurrentAppId;
        this.IsPaused = state.IsPaused;
        this.CurrentLevel = state.CurrentLevel;
        this.HasActiveIssues = state.ActiveIssues.Count > 0;

        this.ActiveGrants.Clear();
        foreach (var grant in state.ActiveGrants)
        {
            this.ActiveGrants.Add(grant);
        }

        this.ActiveIssues.Clear();
        foreach (var issue in state.ActiveIssues)
        {
            this.ActiveIssues.Add(issue);
        }

        this.StatusSummary = this.ComputeSummary(state);
    }

    private string ComputeSummary(UsageStateResponse state)
    {
        if (state.CurrentLevel == EnforcementLevel.Unknown)
        {
            return "Protección no disponible";
        }

        if (state.IsPaused)
        {
            return "Sesión pausada";
        }

        if (state.MinutesRemaining == null)
        {
            return "Sin límite de tiempo";
        }

        if (state.MinutesRemaining <= 0)
        {
            return "Tiempo agotado";
        }

        return $"Te quedan {state.MinutesRemaining} minutos";
    }

    [RelayCommand]
    private void RequestTime()
    {
        // T28 flow — delegate to the request time extra screen
        // Stub for now: just show the summary
    }

    public void Dispose()
    {
        this.realtimeSubscriber.PolicyChanged -= this.OnPolicyOrGrantsChanged;
        this.realtimeSubscriber.GrantsChanged -= this.OnPolicyOrGrantsChanged;
    }
}
