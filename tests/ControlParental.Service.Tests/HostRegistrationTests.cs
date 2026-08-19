// <copyright file="HostRegistrationTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ControlParental.Domain;
using ControlParental.Service.Interop;
using Xunit;

/// <summary>
/// P0 — Regression tests that guard the production service registrations
/// in <see cref="Program"/>. These are static, source-level assertions
/// that fail FIRST if someone deletes, renames, or duplicates a P0
/// registration; they pair with the behavioral descriptor tests below
/// that document what those registrations produce in the DI container.
/// </summary>
public class HostRegistrationTests
{
    private const string ProgramCsRelativePath = "src/ControlParental.Service/Program.cs";

    [Fact]
    public void ProgramCs_RegistersNamedPipeUIServerSingleton_ExactlyOnce()
    {
        string source = ReadProgramSource();

        int occurrences = CountOccurrences(source, "AddSingleton<NamedPipeUIServer>(");

        occurrences.Should().Be(1,
            "NamedPipeUIServer must be registered exactly once so the UI pipe has a single ACL-authoritative instance.");
    }

    [Fact]
    public void ProgramCs_RegistersUIMessageHandlerSingleton_ExactlyOnce()
    {
        string source = ReadProgramSource();

        int occurrences = CountOccurrences(source, "AddSingleton<UIMessageHandler>(");

        occurrences.Should().Be(1,
            "UIMessageHandler must be registered exactly once so the UI pipe dispatches IPC envelopes through a single DI-resolved handler.");
    }

    [Fact]
    public void ProgramCs_RegistersOnboardingStateServiceInterface_ExactlyOnce()
    {
        string source = ReadProgramSource();

        int occurrences = CountOccurrences(source, "AddSingleton<IOnboardingStateService>(");

        occurrences.Should().Be(1,
            "IOnboardingStateService must be registered exactly once so the canonical onboarding state has a single DI-resolved owner.");
    }

    [Fact]
    public void ProgramCs_RegistersOnboardingStateServiceConcrete_ExactlyOnce()
    {
        string source = ReadProgramSource();

        int occurrences = CountOccurrences(source, "AddSingleton<OnboardingStateService>(");

        occurrences.Should().Be(1,
            "OnboardingStateService (concrete) must be registered exactly once — UIMessageHandler depends on the concrete type, not the interface.");
    }

    [Fact]
    public void ProgramCs_RegistersNamedPipeUIServerHostedAdapter_ExactlyOnce()
    {
        string source = ReadProgramSource();

        int occurrences = CountOccurrences(source, "AddHostedService<NamedPipeUIServerHostedAdapter>();");

        occurrences.Should().Be(1,
            "NamedPipeUIServerHostedAdapter must be registered exactly once as the hosted lifecycle owner for the UI pipe.");
    }

    [Fact]
    public void HostBuilder_DuplicateAddSingleton_ProducesTwoDescriptors()
    {
        // Behavior contract: if someone re-introduces the duplicate, this test
        // documents the failure mode (two distinct singletons of the same type).
        // It pairs with the static checks above: the static checks fail FIRST on
        // a regression, and this test explains WHY the duplicate is harmful.
        var services = new ServiceCollection();
        services.AddSingleton<NamedPipeUIServer>();
        services.AddSingleton<NamedPipeUIServer>();

        var descriptors = services.Where(d => d.ServiceType == typeof(NamedPipeUIServer)).ToList();

        descriptors.Should().HaveCount(2,
            "AddSingleton<T>() is not idempotent — two descriptors means two instances will be created.");
    }

    [Fact]
    public void HostBuilder_DuplicateAddIOnboardingStateServiceInterface_ProducesTwoDescriptors()
    {
        // Behavior contract: same shape as above but for the
        // IOnboardingStateService → OnboardingStateService wiring. Two
        // interface descriptors means two state owners.
        var services = new ServiceCollection();
        services.AddSingleton<IOnboardingStateService>(_ => null!);
        services.AddSingleton<IOnboardingStateService>(_ => null!);

        var descriptors = services.Where(d => d.ServiceType == typeof(IOnboardingStateService)).ToList();

        descriptors.Should().HaveCount(2,
            "AddSingleton<TInterface>() is not idempotent — two interface descriptors means the resolver will return the LAST registered implementation.");
    }

    [Fact]
    public void HostBuilder_SingleAddHostedService_ProducesOneHostedServiceDescriptor()
    {
        // Behavior contract: a single AddHostedService<T>() call must yield
        // exactly one IHostedService descriptor whose implementation type
        // matches the adapter.
        var services = new ServiceCollection();
        services.AddHostedService<NamedPipeUIServerHostedAdapter>();

        var hostedDescriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        hostedDescriptors.Should().ContainSingle(
            "a single AddHostedService<T>() call must register exactly one IHostedService descriptor.");
        hostedDescriptors[0].ImplementationType.Should().Be<NamedPipeUIServerHostedAdapter>();
    }

    private static string ReadProgramSource()
    {
        // The tests run with the repo root as working directory (the .sln is one level up
        // from src/ and tests/), so we walk upward until we find the Program.cs we need.
        string? path = LocateRepoFile(ProgramCsRelativePath);
        path.Should().NotBeNullOrEmpty(
            "HostRegistrationTests must run from a working directory that contains the repo, " +
            "or be invoked with the repo root on the search path.");
        return File.ReadAllText(path!);
    }

    private static string? LocateRepoFile(string relativePath)
    {
        DirectoryInfo? dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle))
        {
            return 0;
        }

        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
