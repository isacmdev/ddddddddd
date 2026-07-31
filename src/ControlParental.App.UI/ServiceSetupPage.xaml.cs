// <copyright file="ServiceSetupPage.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.App.UI.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

/// <summary>Verifies the Service-owned protection setup before reporting completion.</summary>
public sealed partial class ServiceSetupPage : Page
{
    private readonly IIpcOnboardingStateService onboardingClient;
    private readonly Action onSetupVerified;

    /// <inheritdoc/>
    public ServiceSetupPage(Action onSetupVerified, IIpcOnboardingStateService? onboardingClient = null)
    {
        this.onSetupVerified = onSetupVerified ?? throw new ArgumentNullException(nameof(onSetupVerified));
        this.onboardingClient = onboardingClient
            ?? App.Services.GetRequiredService<IIpcOnboardingStateService>();
        this.InitializeComponent();
    }

    private async void OnVerifyClick(object sender, RoutedEventArgs e)
    {
        this.VerifyButton.IsEnabled = false;
        this.VerificationProgress.IsActive = true;
        try
        {
            var status = await this.onboardingClient.RequestOrVerifyServiceSetupAsync().ConfigureAwait(true);
            this.StatusText.Text = status.StatusDescription ?? string.Empty;
            if (status.IsInstalled && status.IsRunning)
            {
                this.onSetupVerified();
                return;
            }
        }
        catch (ConsentServiceUnavailableException ex)
        {
            this.StatusText.Text = ex.Message;
        }
        finally
        {
            this.VerificationProgress.IsActive = false;
            this.VerifyButton.IsEnabled = true;
        }
    }
}
