// <copyright file="ManagedStepViewModelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System.Threading.Tasks;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Moq;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 PR #6 (Fase 4: managed opt-in) — Regression tests for
/// <see cref="ManagedStepViewModel"/>.
///
/// Covers the audit-anchored properties of the managed step:
/// <list type="bullet">
///   <item>edition honesty — Home does NOT show the upgrade card; Pro+ does;</item>
///   <item>finish callback routing — the click/RelayCommand wires through
///   <c>onCompleted.Invoke()</c> so the MainWindow advances the canonical
///   state via IPC, never locally;</item>
///   <item>load-state honesty — <c>LoadStateAsync</c> reads
///   <see cref="IEnforcementLevelMonitor.CurrentLevel"/> and surfaces a
///   deterministic display string.</item>
/// </list>
///
/// T31 (real WDAC/AppLocker provisioning) is explicitly out of scope.
/// </summary>
public sealed class ManagedStepViewModelTests
{
    /// <summary>
    /// On Home edition (or any edition that does not allow MANAGED), the
    /// upgrade card MUST be hidden — the user only sees the
    /// "Modo reforzado no disponible en esta edición" path with the
    /// "Finalizar onboarding" button.
    /// </summary>
    [Fact]
    public void ManagedStepViewModelWhenOnHomeEditionDoesNotShowUpgradeCard()
    {
        // Arrange
        var monitor = CreateMonitor(EnforcementLevel.Standard);
        var callbackInvoked = false;
        var viewModel = new ManagedStepViewModel(
            monitor,
            () => callbackInvoked = true,
            currentEditionSupportsManaged: false);

        // Act
        viewModel.LoadStateAsync();

        // Assert — VM exposes the gate so the XAML can hide the card.
        Assert.False(viewModel.CurrentEditionSupportsManaged);
        Assert.False(viewModel.IsManagedAlready);

        // Sanity — even pressing "Finalizar onboarding" still routes
        // through onCompleted so we don't regress the advance path.
        viewModel.FinishCommand.Execute(null);
        Assert.True(callbackInvoked);
    }

    /// <summary>
    /// On Pro+/Enterprise/Education the upgrade card MUST be shown with
    /// the "Activar modo reforzado" action.
    /// </summary>
    [Fact]
    public void ManagedStepViewModelWhenOnProEditionShowsUpgradeCard()
    {
        // Arrange
        var monitor = CreateMonitor(EnforcementLevel.Standard);
        var callbackInvoked = false;
        var viewModel = new ManagedStepViewModel(
            monitor,
            () => callbackInvoked = true,
            currentEditionSupportsManaged: true);

        // Act
        viewModel.LoadStateAsync();

        // Assert
        Assert.True(viewModel.CurrentEditionSupportsManaged);
        Assert.False(viewModel.IsManagedAlready);

        // Pressing the upgrade button routes through onCompleted AND flips
        // the local opt-in flag so the UI can render the success state
        // before the next IPC poll resolves. The authoritative state still
        // lives in the Service (ADR-002).
        viewModel.FinishCommand.Execute(null);
        Assert.True(callbackInvoked);
        Assert.True(viewModel.IsOptedInToManaged);
    }

    /// <summary>
    /// The finish command MUST always invoke the supplied <c>onCompleted</c>
    /// callback regardless of edition — that's the only path that advances
    /// the canonical onboarding state.
    /// </summary>
    [Fact]
    public void ManagedStepViewModelFinishCommandInvokesOnCompleted()
    {
        // Arrange
        var monitor = CreateMonitor(EnforcementLevel.Standard);
        var callCount = 0;
        var viewModel = new ManagedStepViewModel(
            monitor,
            () => callCount++,
            currentEditionSupportsManaged: true);

        // Act
        viewModel.FinishCommand.Execute(null);
        viewModel.FinishCommand.Execute(null);

        // Assert — invoked exactly twice; no local state write attempted.
        Assert.Equal(2, callCount);
    }

    /// <summary>
    /// <c>LoadStateAsync</c> MUST read the current
    /// <see cref="EnforcementLevel"/> from the injected monitor and surface
    /// the matching user-facing copy. The display string is the only thing
    /// the page binds to for the "current level" badge.
    /// </summary>
    [Fact]
    public void ManagedStepViewModelLoadStateAsyncPopulatesCurrentLevel()
    {
        // Arrange — different monitor snapshots to cover the three branches.
        var cases = new[]
        {
            (EnforcementLevel.Managed, "MANAGED — capa preventiva activa"),
            (EnforcementLevel.Standard, "STANDARD — capa preventiva opcional"),
            (EnforcementLevel.Degraded, "DEGRADED — reparar primero"),
            (EnforcementLevel.Unknown, ManagedStepViewModel.UnknownStateCopy),
        };

        foreach (var (level, expectedDisplay) in cases)
        {
            var monitor = CreateMonitor(level);
            var viewModel = new ManagedStepViewModel(
                monitor,
                () => { },
                currentEditionSupportsManaged: true);

            // Act
            viewModel.LoadStateAsync();

            // Assert
            Assert.Equal(level, viewModel.CurrentEnforcementLevel);
            Assert.Equal(expectedDisplay, viewModel.CurrentEnforcementLevelDisplay);
        }
    }

    /// <summary>
    /// When the monitor already reports <see cref="EnforcementLevel.Managed"/>,
    /// the VM MUST surface <c>IsManagedAlready = true</c> so the XAML can
    /// collapse the upgrade card and show the success state. Onboarding can
    /// close out cleanly in that case without pressing "Activar".
    /// </summary>
    [Fact]
    public void ManagedStepViewModelWhenLevelAlreadyManagedDoesNotOptInOnFinish()
    {
        // Arrange
        var monitor = CreateMonitor(EnforcementLevel.Managed);
        var callbackInvoked = false;
        var viewModel = new ManagedStepViewModel(
            monitor,
            () => callbackInvoked = true,
            currentEditionSupportsManaged: true);
        viewModel.LoadStateAsync();

        // Assert — the gate flips true because the monitor says Managed.
        Assert.True(viewModel.IsManagedAlready);
        Assert.False(viewModel.IsOptedInToManaged);

        // Act — pressing "Finalizar onboarding" still routes through
        // onCompleted; we MUST NOT re-mark IsOptedInToManaged when the
        // monitor already reports Managed (the local flag would imply a
        // state change the Service did not actually accept).
        viewModel.FinishCommand.Execute(null);

        // Assert — callback fires, opt-in flag stays false (the monitor is
        // already authoritative).
        Assert.True(callbackInvoked);
        Assert.False(viewModel.IsOptedInToManaged);
    }

    /// <summary>
    /// Defence-in-depth: passing a null monitor throws an
    /// <see cref="System.ArgumentNullException"/> at construction. Tests
    /// that forget to wire a monitor should fail loudly, not silently.
    /// </summary>
    [Fact]
    public void ManagedStepViewModelConstructorNullMonitorThrows()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new ManagedStepViewModel(
                monitor: null!,
                onCompleted: () => { },
                currentEditionSupportsManaged: true));
    }

    /// <summary>
    /// Defence-in-depth: passing a null completion callback also throws —
    /// the VM never advances the canonical state itself, so a missing
    /// callback would be a silent regression.
    /// </summary>
    [Fact]
    public void ManagedStepViewModelConstructorNullCallbackThrows()
    {
        var monitor = CreateMonitor(EnforcementLevel.Standard);
        Assert.Throws<System.ArgumentNullException>(() =>
            new ManagedStepViewModel(
                monitor,
                onCompleted: null!,
                currentEditionSupportsManaged: true));
    }

    private static IEnforcementLevelMonitor CreateMonitor(EnforcementLevel level)
    {
        var mock = new Mock<IEnforcementLevelMonitor>();
        mock.SetupGet(m => m.CurrentLevel).Returns(level);
        mock.SetupGet(m => m.CurrentIssues).Returns(System.Array.Empty<EnforcementIssue>());
        return mock.Object;
    }
}
