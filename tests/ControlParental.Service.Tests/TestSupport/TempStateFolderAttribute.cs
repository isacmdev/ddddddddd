// <copyright file="TempStateFolderAttribute.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests.TestSupport;

using ControlParental.Domain;
using Moq;
using Xunit;

/// <summary>
/// Marks tests that use an isolated onboarding-state folder.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TempStateFolderAttribute : FactAttribute
{
}

/// <summary>
/// Provides an isolated folder and real onboarding-state service to tests.
/// </summary>
public abstract class TempStateFolderTestBase : IDisposable
{
    protected TempStateFolderTestBase()
    {
        this.TempFolderPath = Path.Combine(Path.GetTempPath(), $"cp-state-{Guid.NewGuid():N}");
        this.StateService = new OnboardingStateService(
            this.TempFolderPath,
            new Mock<IChildAccountStore>().Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<OnboardingStateService>>().Object);
    }

    protected string TempFolderPath { get; }

    protected OnboardingStateService StateService { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(this.TempFolderPath))
            {
                Directory.Delete(this.TempFolderPath, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup must not hide the test result.
        }

        GC.SuppressFinalize(this);
    }
}
