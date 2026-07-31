// <copyright file="ObfuscationStaticGateTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Obfuscation.CheckTests;

using System.Text.Json;
using System.Xml.Linq;
using Xunit;

/// <summary>
/// T23 Obfuscation Closure — Unit 1 + Unit 2 RED / guard tests.
///
/// These tests assert that each fail-loud gate defined by the
/// <c>design.md</c> is statically wired up in
/// <c>ControlParental.Domain.csproj</c> / <c>ControlParental.Service.csproj</c>
/// and that the Obfuscar tooling, smoke project, and checked-in
/// configuration are consistent. They are deliberately static: they
/// read the project files from the repository root and check for the
/// literal gate strings rather than executing MSBuild, so they stay
/// fast and deterministic across the developer's normal test runs.
///
/// The MSBuild runtime check for each gate scenario lives in
/// <c>tests/obfuscation-checks/Invoke-ObfuscationChecks.ps1</c> and is
/// driven by the <c>ObfuscateForceFail</c> property documented in the
/// Domain csproj.
/// </summary>
public sealed class ObfuscationStaticGateTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", ".."));

    private static string RepoPath(params string[] segments)
        => Path.Combine(RepositoryRoot, Path.Combine(segments));

    /// <inheritdoc/>
    [Fact]
    public void DotnetToolsManifestPinsObfuscar2250()
    {
        var manifestPath = RepoPath(".config", "dotnet-tools.json");
        Assert.True(File.Exists(manifestPath), $"the local tool manifest must exist at {manifestPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));

        Assert.True(document.RootElement.TryGetProperty("tools", out var tools),
            "the manifest must declare tools");

        Assert.True(tools.TryGetProperty("obfuscar.globaltool", out var obfuscar),
            "obfuscar.globaltool must be the pinned obfuscator");

        Assert.Equal("2.2.50", obfuscar.GetProperty("version").GetString());

        var commands = obfuscar.GetProperty("commands")
            .EnumerateArray()
            .Select(c => c.GetString())
            .Where(c => c is not null)
            .Select(c => c!)
            .ToArray();
        Assert.Contains("obfuscar.console", commands);
    }

    /// <inheritdoc/>
    [Fact]
    public void ObfuscarConfigPreservesPublicApi()
    {
        var configPath = RepoPath("build", "obfuscation", "ControlParental.Domain.xml");
        Assert.True(File.Exists(configPath), $"obfuscator config must exist at {configPath}");

        var xml = XDocument.Load(configPath);
        var root = xml.Root ?? throw new InvalidOperationException("Empty config XML");

        var keepPublicApi = root.Elements("Var")
            .Where(v => string.Equals((string?)v.Attribute("name"), "KeepPublicApi", StringComparison.Ordinal))
            .Select(v => (string?)v.Attribute("value"))
            .FirstOrDefault();
        Assert.Equal("true", keepPublicApi);

        var skipTypes = root.Descendants("SkipType")
            .Select(e => (string?)e.Attribute("name"))
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToList();
        Assert.Contains("ControlParental.Domain.PolicyJsonContext", skipTypes);
        Assert.Contains("ControlParental.Domain.UIMessagesJsonContext", skipTypes);
        Assert.Contains("ControlParental.Domain.Strings", skipTypes);
    }

    /// <inheritdoc/>
    [Fact]
    public void ObfuscarConfigInputsOnlyDomainAssembly()
    {
        var xml = XDocument.Load(RepoPath("build", "obfuscation", "ControlParental.Domain.xml"));
        var root = xml.Root ?? throw new InvalidOperationException("Empty config XML");

        // Read only the actual element payload (excluding comments); the
        // documentation comments are allowed to mention excluded names.
        var payloadNodes = root.Nodes()
            .Where(n => n.NodeType != System.Xml.XmlNodeType.Comment)
            .ToList();
        var payloadText = string.Concat(payloadNodes.Select(n => n.ToString()));

        var forbidden = new[]
        {
            "ControlParental.Service",
            "ControlParental.App.UI",
            "SessionAgent",
            "Backend",
            "TpmSigner",
            "DhaSigner",
        };

        foreach (var token in forbidden)
        {
            Assert.False(
                payloadText.Contains(token, StringComparison.OrdinalIgnoreCase),
                $"the obfuscator config element payload must NOT reference {token}");
        }

        var modules = root.Descendants("Module").ToList();
        Assert.Single(modules);

        var moduleFile = (string?)modules[0].Attribute("file");
        Assert.False(string.IsNullOrEmpty(moduleFile));
        Assert.EndsWith("{ModuleFile}", moduleFile!, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    [Fact]
    public void DomainCsprojDeclaresObfuscateDomainTargetWithAllGates()
    {
        var csprojPath = RepoPath("src", "ControlParental.Domain", "ControlParental.Domain.csproj");
        Assert.True(File.Exists(csprojPath), $"Domain csproj must exist at {csprojPath}");

        var text = File.ReadAllText(csprojPath);

        Assert.Contains("<Target", text);
        Assert.Contains("Name=\"ObfuscateDomain\"", text);
        Assert.Contains("AfterTargets=\"Build\"", text);
        Assert.Contains("'$(Configuration)' == 'Release'", text);
        Assert.Contains("'$(ObfuscarSkip)' != 'true'", text);

        // Gates must be statically present as <Error> conditions.
        Assert.Contains("Input assembly not found at", text);
        Assert.Contains("Template config not found", text);
        Assert.Contains("Prepare script not found", text);
        Assert.Contains("Forced gate failure (MissingTool)", text);
        Assert.Contains("Forced gate failure (BrokenExit)", text);
        Assert.Contains("Obfuscated staging output not found", text);
        Assert.Contains("Mapping file not found", text);
        Assert.Contains("byte-identical to the input", text);

        Assert.Contains("GetFileHash", text);
        Assert.Contains("FileHash", text);

        Assert.Contains("Inputs=\"$(ObfuscationModuleFile)\"", text);
        Assert.Contains("Outputs=\"$(ObfuscationMarkerFile)\"", text);

        Assert.Contains("ObfuscationMarkerFile", text);
        Assert.Contains("obfuscated-by-obfuscar;assembly=", text);
    }

    /// <inheritdoc/>
    [Fact]
    public void DomainCsprojOnlyObfuscatesDomainDll()
    {
        var csprojPath = RepoPath("src", "ControlParental.Domain", "ControlParental.Domain.csproj");
        var text = File.ReadAllText(csprojPath);

        var forbidden = new[]
        {
            "ControlParental.Service.dll",
            "ControlParental.App.UI.dll",
            "ControlParental.SessionAgent.dll",
            "TpmSigner",
            "DhaSigner",
            "BackendClient",
        };

        foreach (var token in forbidden)
        {
            Assert.False(
                text.Contains(token, StringComparison.OrdinalIgnoreCase),
                $"Domain obfuscation must NOT reference {token}");
        }
    }

    /// <inheritdoc/>
    [Fact]
    public void ObfuscationPrepareScriptExists()
    {
        var scriptPath = RepoPath("build", "obfuscation", "Prepare-ObfuscarConfig.ps1");
        Assert.True(File.Exists(scriptPath));

        var text = File.ReadAllText(scriptPath);
        Assert.Contains("param(", text);
        Assert.Contains("[string]$Template", text);
        Assert.Contains("[string]$ModuleFile", text);
        Assert.Contains("{InPath}", text);
        Assert.Contains("{ModuleFile}", text);
    }

    // ---------------------------------------------------------------------
    // Unit 2 — publish smoke static gates
    // ---------------------------------------------------------------------

    /// <inheritdoc/>
    [Fact]
    public void SmokeProjectExistsWithCorrectTfm()
    {
        var smokeCsprojPath = RepoPath(
            "tests", "ControlParental.Obfuscation.Smoke",
            "ControlParental.Obfuscation.Smoke.csproj");
        Assert.True(File.Exists(smokeCsprojPath), $"smoke csproj must exist at {smokeCsprojPath}");

        var text = File.ReadAllText(smokeCsprojPath);

        // Plain net9.0 (not Windows-specific) so the smoke can be invoked
        // from any build/publish pipeline without dragging in the
        // Service's Windows App SDK / Hosting dependencies.
        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", text);
        Assert.Contains("<OutputType>Exe</OutputType>", text);
        Assert.Contains("<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>", text);

        // Smoke MUST NOT reference the Domain or Service projects: the
        // obfuscated artifact is supplied at runtime via --domain-dll.
        Assert.DoesNotContain("ProjectReference Include=\"..\\..\\src\\ControlParental.Domain", text);
        Assert.DoesNotContain("ProjectReference Include=\"..\\..\\src\\ControlParental.Service", text);

        // EF Core / SQLite are explicit dependencies.
        Assert.Contains("Microsoft.EntityFrameworkCore", text);
        Assert.Contains("Microsoft.EntityFrameworkCore.Sqlite", text);
        Assert.Contains("Microsoft.Data.Sqlite", text);
    }

    /// <inheritdoc/>
    [Fact]
    public void SmokeProgramImplementsAllThreeGates()
    {
        var programPath = RepoPath(
            "tests", "ControlParental.Obfuscation.Smoke", "Program.cs");
        Assert.True(File.Exists(programPath), $"smoke Program.cs must exist at {programPath}");

        var text = File.ReadAllText(programPath);

        // Load: --domain-dll argument handling + AssemblyLoadContext.
        Assert.Contains("--domain-dll", text);
        Assert.Contains("AssemblyLoadContext", text);
        Assert.Contains("LoadFromAssemblyPath", text);

        // JSON: PolicyJsonContext + JsonSerializer source-gen invocation.
        Assert.Contains("PolicyJsonContext", text);
        Assert.Contains("JsonSerializer.Serialize", text);

        // EF: SQLite in-memory + DbContext + PolicyDbEntity + EnsureCreated.
        Assert.Contains("SqliteConnection", text);
        Assert.Contains("EnsureCreated", text);
        Assert.Contains("PolicyDbEntity", text);
        Assert.Contains("DbContextOptionsBuilder", text);

        // RED-path exit codes documented in the file.
        Assert.Contains("[smoke] FAIL", text);
    }

    /// <inheritdoc/>
    [Fact]
    public void SmokeDbContextMirrorsProductionOnModelCreatingForPolicyDbEntity()
    {
        var smokeDbContextPath = RepoPath(
            "tests", "ControlParental.Obfuscation.Smoke", "SmokeDbContext.cs");
        Assert.True(File.Exists(smokeDbContextPath), $"smoke DbContext must exist at {smokeDbContextPath}");

        var text = File.ReadAllText(smokeDbContextPath);

        Assert.Contains("DbContext", text);
        Assert.Contains("OnModelCreating", text);
        Assert.Contains("policies", text);
        Assert.Contains("device_id", text);
        Assert.Contains("policy_json", text);
        Assert.Contains("category_assignments_json", text);

        // Hardcoded property names reflect the production entity
        // (KeepPublicApi=true preserves them through obfuscation).
        Assert.Contains("DeviceIdProperty", text);
        Assert.Contains("VersionProperty", text);
        Assert.Contains("PolicyJsonProperty", text);
        Assert.Contains("LastUpdatedProperty", text);
        Assert.Contains("CategoryAssignmentsJsonProperty", text);
    }

    /// <inheritdoc/>
    [Fact]
    public void ServiceCsprojDeclaresRunObfuscationSmokeTarget()
    {
        var serviceCsprojPath = RepoPath(
            "src", "ControlParental.Service", "ControlParental.Service.csproj");
        Assert.True(File.Exists(serviceCsprojPath), $"Service csproj must exist at {serviceCsprojPath}");

        var text = File.ReadAllText(serviceCsprojPath);

        Assert.Contains("Name=\"RunObfuscationSmoke\"", text);
        Assert.Contains("AfterTargets=\"Build\"", text);
        Assert.Contains("'$(Configuration)' == 'Release'", text);
        Assert.Contains("'$(ObfuscarSkip)' != 'true'", text);
        Assert.Contains("'$(ObfuscationSmokeSkip)' != 'true'", text);

        // Marker gate must be present (fail-loud before smoke runs).
        Assert.Contains("[ObfuscationSmoke][GATE] Domain completion marker missing", text);

        // Smoke must be invoked against the obfuscated Domain.dll in the
        // Domain project's Release bin folder (not a fresh build output).
        Assert.Contains("ObfuscationSmokeTargetDll", text);
        Assert.Contains("ControlParental.Domain.dll", text);
        Assert.Contains("ControlParental.Obfuscation.Smoke.exe", text);
    }

    /// <inheritdoc/>
    [Fact]
    public void SolutionIncludesSmokeProject()
    {
        var slnPath = RepoPath("ControlParental.sln");
        Assert.True(File.Exists(slnPath));

        var text = File.ReadAllText(slnPath);
        Assert.Contains("ControlParental.Obfuscation.Smoke.csproj", text);
        Assert.Contains("{A1B2C3D4-0010-0010-0010-000000000010}", text);
    }
}
