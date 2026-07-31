// <copyright file="PairingViewModelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System.Threading.Tasks;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 PR #5 (Fase 3: real pairing) — Regression tests for
/// <see cref="PairingViewModel"/>.
///
/// Covers the audit finding that <c>PairingViewModel.PairWithCodeAsync</c>
/// previously fell through a <c>Task.Delay(500)</c> simulation. After Fase 3
/// the VM routes exclusively through <see cref="IUIChannel"/> IPC and maps
/// every <see cref="PairingStatus"/> to a deterministic UI copy.
/// </summary>
public sealed class PairingViewModelTests
{
    /// <summary>
    /// On the happy path the mock channel returns a success response; the VM
    /// MUST invoke the completion callback exactly once and never surface an
    /// error message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task PairWithCodeAsyncOnSuccessEmitsCompletedCallback()
    {
        // Arrange
        var channel = new MockNamedPipeUIChannel();
        channel.OnPairDevice(_ => new ControlParental.App.UI.PairDeviceResponse(
            Success: true,
            DeviceId: "device-1",
            ParentId: "parent-1",
            PolicyVersion: 1,
            Status: PairingStatus.Success,
            ErrorMessage: null));

        var callCount = 0;
        var viewModel = new PairingViewModel(() => callCount++, channel)
        {
            SelectedAgeBandIndex = 0,
            CodeDigit1 = "A",
            CodeDigit2 = "B",
            CodeDigit3 = "C",
            CodeDigit4 = "1",
            CodeDigit5 = "2",
            CodeDigit6 = "3",
            CodeDigit7 = "D",
            CodeDigit8 = "E",
        };

        // Act
        await viewModel.PairWithCodeCommand.ExecuteAsync(null).ConfigureAwait(false);

        // Assert — completion callback invoked exactly once, no error.
        Assert.Equal(1, callCount);
        Assert.False(viewModel.HasError);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
        Assert.NotNull(channel.LastPairDeviceRequest);
    }

    /// <summary>
    /// HTTP 404 (InvalidCode) MUST surface the dedicated "Ese código no es válido"
    /// copy — NOT a generic message lifted from the backend.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task PairWithCodeAsyncOnInvalidCodeShowsInvalidCodeMessage()
    {
        var channel = new MockNamedPipeUIChannel();
        channel.OnPairDevice(_ => new ControlParental.App.UI.PairDeviceResponse(
            Success: false,
            DeviceId: null,
            ParentId: null,
            PolicyVersion: 0,
            Status: PairingStatus.InvalidCode,
            ErrorMessage: "backend-specific copy that MUST be ignored"));

        var viewModel = new PairingViewModel(null, channel)
        {
            SelectedAgeBandIndex = 0,
            CodeDigit1 = "A",
            CodeDigit2 = "B",
            CodeDigit3 = "C",
            CodeDigit4 = "1",
            CodeDigit5 = "2",
            CodeDigit6 = "3",
            CodeDigit7 = "D",
            CodeDigit8 = "E",
        };

        await viewModel.PairWithCodeCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.True(viewModel.HasError);
        Assert.Equal(PairingViewModel.PairingErrorInvalidCode, viewModel.ErrorMessage);
    }

    /// <summary>
    /// HTTP 410 (ExpiredCode) MUST surface the dedicated "Ese código ya expiró"
    /// copy.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task PairWithCodeAsyncOnExpiredCodeShowsExpiredCodeMessage()
    {
        var channel = new MockNamedPipeUIChannel();
        channel.OnPairDevice(_ => new ControlParental.App.UI.PairDeviceResponse(
            Success: false,
            DeviceId: null,
            ParentId: null,
            PolicyVersion: 0,
            Status: PairingStatus.ExpiredCode,
            ErrorMessage: null));

        var viewModel = new PairingViewModel(null, channel)
        {
            SelectedAgeBandIndex = 0,
            CodeDigit1 = "A",
            CodeDigit2 = "B",
            CodeDigit3 = "C",
            CodeDigit4 = "1",
            CodeDigit5 = "2",
            CodeDigit6 = "3",
            CodeDigit7 = "D",
            CodeDigit8 = "E",
        };

        await viewModel.PairWithCodeCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.True(viewModel.HasError);
        Assert.Equal(PairingViewModel.PairingErrorExpiredCode, viewModel.ErrorMessage);
    }

    /// <summary>
    /// HTTP 429 (TooManyRequests) MUST surface the soft retry copy
    /// "Probá de nuevo en un ratito" — NOT the generic "Pedile ayuda a tu tutor"
    /// copy reserved for unrecoverable errors.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task PairWithCodeAsyncOnTooManyRequestsShowsRateLimitMessage()
    {
        var channel = new MockNamedPipeUIChannel();
        channel.OnPairDevice(_ => new ControlParental.App.UI.PairDeviceResponse(
            Success: false,
            DeviceId: null,
            ParentId: null,
            PolicyVersion: 0,
            Status: PairingStatus.TooManyRequests,
            ErrorMessage: "Rate limit message text from backend"));

        var viewModel = new PairingViewModel(null, channel)
        {
            SelectedAgeBandIndex = 1,
            CodeDigit1 = "X",
            CodeDigit2 = "Y",
            CodeDigit3 = "Z",
            CodeDigit4 = "9",
            CodeDigit5 = "8",
            CodeDigit6 = "7",
            CodeDigit7 = "6",
            CodeDigit8 = "5",
        };

        await viewModel.PairWithCodeCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.True(viewModel.HasError);
        Assert.Equal(PairingViewModel.PairingErrorTooManyRequests, viewModel.ErrorMessage);
    }

    /// <summary>
    /// When the mock channel returns a generic <see cref="PairingStatus.Error"/>
    /// with no useful <c>ErrorMessage</c> the VM MUST fall back to the
    /// "Pedile ayuda a tu tutor" copy rather than leave the user looking at
    /// an empty error state.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task PairWithCodeAsyncOnIpcErrorShowsGenericError()
    {
        var channel = new MockNamedPipeUIChannel();
        channel.OnPairDevice(_ => new ControlParental.App.UI.PairDeviceResponse(
            Success: false,
            DeviceId: null,
            ParentId: null,
            PolicyVersion: 0,
            Status: PairingStatus.Error,
            ErrorMessage: null));

        var viewModel = new PairingViewModel(null, channel)
        {
            SelectedAgeBandIndex = 2,
            CodeDigit1 = "A",
            CodeDigit2 = "B",
            CodeDigit3 = "C",
            CodeDigit4 = "D",
            CodeDigit5 = "E",
            CodeDigit6 = "F",
            CodeDigit7 = "G",
            CodeDigit8 = "H",
        };

        await viewModel.PairWithCodeCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.True(viewModel.HasError);
        Assert.Equal(PairingViewModel.PairingErrorFallback, viewModel.ErrorMessage);
    }

    /// <summary>
    /// On construction the VM MUST default to no age-band selection
    /// (<c>SelectedAgeBandIndex == -1</c>) so the user is forced to pick one
    /// before the Emparejar button can enable — and the derived
    /// <see cref="PairingViewModel.CanPair"/> MUST be false even after the
    /// user has typed a complete 8-character code.
    /// </summary>
    [Fact]
    public void SelectedAgeBandNullAtStartEmparejarButtonDisabled()
    {
        // Arrange — only digits are populated; no age band has been picked.
        var channel = new MockNamedPipeUIChannel();
        var viewModel = new PairingViewModel(null, channel)
        {
            SelectedAgeBandIndex = -1,
            CodeDigit1 = "A",
            CodeDigit2 = "B",
            CodeDigit3 = "C",
            CodeDigit4 = "1",
            CodeDigit5 = "2",
            CodeDigit6 = "3",
            CodeDigit7 = "D",
            CodeDigit8 = "E",
        };

        // Assert — the CanPair gate refuses until the user picks a band.
        Assert.Equal(-1, viewModel.SelectedAgeBandIndex);
        Assert.False(viewModel.CanPair);
        Assert.Null(viewModel.SelectedAgeBandWire);

        // And after selecting a band, CanPair flips true.
        viewModel.SelectedAgeBandIndex = 0;
        Assert.True(viewModel.CanPair);
        Assert.NotNull(viewModel.SelectedAgeBandWire);
    }

    /// <summary>
    /// ADR-004 — the wire payload's <c>AgeBand</c> field MUST be one of
    /// "7-12", "13-16" or "17-18" (no "años" suffix, no localized copy).
    /// Regression for the audit finding that the VM previously read
    /// <c>ComboBoxItem.Content</c> directly, which would have yielded
    /// "7-12 años" on the wire.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task WireAgeBandSentMatchesDomainFormatNoAnosSuffix()
    {
        foreach (var (index, expected) in new[]
        {
            (0, "7-12"),
            (1, "13-16"),
            (2, "17-18"),
        })
        {
            var channel = new MockNamedPipeUIChannel();
            channel.OnPairDevice(_ => new ControlParental.App.UI.PairDeviceResponse(
                Success: true,
                DeviceId: "d",
                ParentId: "p",
                PolicyVersion: 1,
                Status: PairingStatus.Success,
                ErrorMessage: null));

            var viewModel = new PairingViewModel(null, channel)
            {
                SelectedAgeBandIndex = index,
                CodeDigit1 = "A",
                CodeDigit2 = "B",
                CodeDigit3 = "C",
                CodeDigit4 = "1",
                CodeDigit5 = "2",
                CodeDigit6 = "3",
                CodeDigit7 = "D",
                CodeDigit8 = "E",
            };

            await viewModel.PairWithCodeCommand.ExecuteAsync(null).ConfigureAwait(false);

            Assert.NotNull(channel.LastPairDeviceRequest);
            Assert.Equal(expected, channel.LastPairDeviceRequest!.AgeBand);
            Assert.DoesNotContain("a", channel.LastPairDeviceRequest.AgeBand);
            Assert.DoesNotContain("años", channel.LastPairDeviceRequest.AgeBand);
        }
    }
}
