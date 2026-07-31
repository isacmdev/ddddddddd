// <copyright file="OnboardingStateAtomicTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using ControlParental.Service.Tests.TestSupport;
using Xunit;

public sealed class OnboardingStateAtomicTests : TempStateFolderTestBase
{
    [TempStateFolder]
    public async Task ConcurrentRecordStep_NoCorruption()
    {
        await Task.WhenAll(
            this.StateService.RecordStepCompletedAsync("pairing"),
            this.StateService.RecordStepCompletedAsync("consent"));

        var state = await this.StateService.GetStateAsync();

        Assert.Equal(OnboardingStepStatus.Completed, state.Steps.Single(s => s.Id == "pairing").Status);
        Assert.Equal(OnboardingStepStatus.Completed, state.Steps.Single(s => s.Id == "consent").Status);
    }

    [TempStateFolder]
    public async Task CrashMidWrite_DoesNotCorruptFile()
    {
        await this.StateService.AdvanceAsync(1);
        var statePath = Path.Combine(this.TempFolderPath, "onboarding_state.json");
        var original = await File.ReadAllTextAsync(statePath);
        var tmpPath = statePath + ".tmp";

        await using (var lockedTmp = new FileStream(tmpPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => this.StateService.AdvanceAsync(2));
        }

        Assert.Equal(original, await File.ReadAllTextAsync(statePath));
        Assert.Equal(1, (await this.StateService.GetStateAsync()).CurrentStepIndex);
    }
}
