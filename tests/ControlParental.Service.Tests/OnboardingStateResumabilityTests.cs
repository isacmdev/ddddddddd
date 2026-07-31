// <copyright file="OnboardingStateResumabilityTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using ControlParental.Service.Tests.TestSupport;
using Moq;
using Xunit;

public sealed class OnboardingStateResumabilityTests : TempStateFolderTestBase
{
    [TempStateFolder]
    public async Task Restart_AfterPairingCompleted_ResumesFromConsentStep()
    {
        await this.StateService.RecordStepCompletedAsync("pairing");
        await this.StateService.AdvanceAsync(1);

        var restarted = this.CreateRestartedService();
        var state = await restarted.GetStateAsync();

        Assert.Equal(1, state.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.Completed, state.Steps[0].Status);
        Assert.Equal(OnboardingStepStatus.InProgress, state.Steps[1].Status);
    }

    [TempStateFolder]
    public async Task Restart_AfterAllStepsComplete_ReportsIsCompletedTrue()
    {
        foreach (var stepId in new[] { "pairing", "consent", "account", "service", "demo", "managed" })
        {
            await this.StateService.RecordStepCompletedAsync(stepId);
        }

        await this.StateService.AdvanceAsync(6);

        var state = await this.CreateRestartedService().GetStateAsync();

        Assert.True(state.IsCompleted);
        Assert.All(state.Steps, step => Assert.Equal(OnboardingStepStatus.Completed, step.Status));
    }

    private OnboardingStateService CreateRestartedService()
    {
        return new OnboardingStateService(
            this.TempFolderPath,
            new Mock<IChildAccountStore>().Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<OnboardingStateService>>().Object);
    }
}
