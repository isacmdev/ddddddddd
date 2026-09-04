// <copyright file="WnsNavigationCompositionBehaviorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System.Reflection;
using System.Runtime.CompilerServices;
using ControlParental.App.UI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

/// <summary>
/// Behavior-first RED contract for mounting the WNS page through the shell.
/// </summary>
public sealed class WnsNavigationCompositionBehaviorTests
{
    [Fact]
    public void WnsNavigation_CompositionSeam_CreatesPageForPageHostMount()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IWnsRegistrationPort>(new Mock<IWnsRegistrationPort>().Object)
            .AddSingleton<WnsRegistrationViewModel>()
            .AddSingleton(
                (WnsRegistrationPage)RuntimeHelpers.GetUninitializedObject(typeof(WnsRegistrationPage)))
            .BuildServiceProvider();

        var createPage = typeof(MainWindow).GetMethod(
            "CreateWnsRegistrationPage",
            BindingFlags.Static | BindingFlags.NonPublic);

        var appServices = typeof(App).GetField(
            "serviceProvider",
            BindingFlags.Static | BindingFlags.NonPublic);
        var previousServices = appServices!.GetValue(null);
        appServices.SetValue(null, services);

        try
        {
            var page = createPage!.Invoke(null, new object[] { services });

            Assert.IsType<WnsRegistrationPage>(page);
        }
        finally
        {
            appServices.SetValue(null, previousServices);
        }
    }
}
