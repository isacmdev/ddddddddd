// <copyright file="DependencyInjectionTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain.Tests;

using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Basic dependency injection tests for T00.
/// Verifies that the Domain project can be used with DI.
/// </summary>
public class DependencyInjectionTests
{
    [Fact]
    public void ResolveService_ShouldReturnRegisteredService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();

        // Act
        var provider = services.BuildServiceProvider();

        // Assert
        var service = provider.GetRequiredService<ITestService>();
        Assert.NotNull(service);
        Assert.Equal("TestValue", service.GetValue());
    }

    [Fact]
    public void SingletonResolution_ShouldReturnSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();

        // Act
        var provider = services.BuildServiceProvider();
        var instance1 = provider.GetRequiredService<ITestService>();
        var instance2 = provider.GetRequiredService<ITestService>();

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void TransientResolution_ShouldReturnDifferentInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();

        // Act
        var provider = services.BuildServiceProvider();
        var instance1 = provider.GetRequiredService<ITestService>();
        var instance2 = provider.GetRequiredService<ITestService>();

        // Assert
        Assert.NotSame(instance1, instance2);
    }
}

/// <summary>
/// Test service interface for DI verification.
/// </summary>
public interface ITestService
{
    /// <summary>
    /// Gets a test value.
    /// </summary>
    string GetValue();
}

/// <summary>
/// Test service implementation.
/// </summary>
public sealed class TestService : ITestService
{
    /// <inheritdoc />
    public string GetValue() => "TestValue";
}
