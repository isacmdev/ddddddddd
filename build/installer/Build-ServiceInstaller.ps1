# Build-ServiceInstaller.ps1
# ----------------------------------------------------------------------------
# Publishes ControlParental.Service and ControlParental.SessionAgent as
# self-contained win-x64 Release bundles, stages them under
# `artifacts/service-installer/payload/`, and copies
# Install-ControlParentalService.ps1 alongside the staged payload.
#
# The output is the T00 service installer bundle: a deterministic directory
# that Install-ControlParentalService.ps1 can register with the SCM on a
# clean Windows machine. This script deliberately does NOT touch the App.UI
# MSIX flow — Build-MSIX.ps1 remains the canonical UI/MSIX build path.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass `
#       -File build/installer/Build-ServiceInstaller.ps1 `
#       [-Configuration Release] [-Runtime win-x64] `
#       [-OutputRoot artifacts/service-installer] `
#       [-SkipPublish]
#
# Returns 0 on success, nonzero on the first publish or stage validation
# failure. Every error prints a single explicit message to STDERR.

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    [string]$ServiceProject = 'src/ControlParental.Service/ControlParental.Service.csproj',
    [string]$AgentProject = 'src/ControlParental.SessionAgent/ControlParental.SessionAgent.csproj',

    [string]$OutputRoot = 'artifacts/service-installer',

    [string]$InstallerScript = 'build/installer/Install-ControlParentalService.ps1',

    # Skip the dotnet publish step (use the existing bin/Release output).
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'

function Fail([string]$message) {
    [Console]::Error.WriteLine("[Build-ServiceInstaller] $message")
    exit 1
}

$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
$absoluteOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
} else {
    Join-Path $repoRoot $OutputRoot
}

$serviceStage = Join-Path $absoluteOutputRoot 'payload/service'
$agentStage = Join-Path $absoluteOutputRoot 'payload/agent'
$installerDest = Join-Path $absoluteOutputRoot 'Install-ControlParentalService.ps1'
$manifestPath = Join-Path $absoluteOutputRoot 'build-manifest.json'

Write-Host "[Build-ServiceInstaller] Output root: $absoluteOutputRoot"

# Resolve project paths relative to the repo root.
$absoluteServiceProject = if ([System.IO.Path]::IsPathRooted($ServiceProject)) {
    $ServiceProject
} else {
    Join-Path $repoRoot $ServiceProject
}
$absoluteAgentProject = if ([System.IO.Path]::IsPathRooted($AgentProject)) {
    $AgentProject
} else {
    Join-Path $repoRoot $AgentProject
}
$absoluteInstallerScript = if ([System.IO.Path]::IsPathRooted($InstallerScript)) {
    $InstallerScript
} else {
    Join-Path $repoRoot $InstallerScript
}

foreach ($p in @($absoluteServiceProject, $absoluteAgentProject, $absoluteInstallerScript)) {
    if (-not (Test-Path -LiteralPath $p)) {
        Fail "Required input not found: $p"
    }
}

function Publish-Project([string]$csproj, [string]$output) {
    Write-Host "[Build-ServiceInstaller] Publishing $csproj -> $output"
    & dotnet publish $csproj `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -o $output `
        -v:minimal `
        -nologo

    if ($LASTEXITCODE -ne 0) {
        Fail "dotnet publish failed for $csproj (exit $LASTEXITCODE)"
    }
}

# 1) Publish each project into a clean staging subdirectory under payload/.
# We stage Service and Agent into separate subdirectories to preserve their
# distinct publish trees. Install-ControlParentalService.ps1 flattens both
# into %ProgramFiles%\ControlParental\Agent at install time.
if (-not $SkipPublish) {
    foreach ($stage in @($serviceStage, $agentStage)) {
        if (Test-Path -LiteralPath $stage) {
            Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    if (-not (Test-Path -LiteralPath $absoluteOutputRoot)) {
        New-Item -ItemType Directory -Path $absoluteOutputRoot -Force | Out-Null
    }
    foreach ($stage in @($serviceStage, $agentStage)) {
        New-Item -ItemType Directory -Path $stage -Force | Out-Null
    }

    Publish-Project $absoluteServiceProject $serviceStage
    Publish-Project $absoluteAgentProject $agentStage
} else {
    Write-Host "[Build-ServiceInstaller] SkipPublish set; reusing existing payload directories."
}

# 2) Validate that the expected binaries landed where Install-ControlParentalService.ps1 expects them.
$expectedBinaries = @(
    (Join-Path $serviceStage 'ControlParental.Service.exe'),
    (Join-Path $agentStage 'ControlParental.SessionAgent.exe')
)
foreach ($binary in $expectedBinaries) {
    if (-not (Test-Path -LiteralPath $binary)) {
        Fail "Expected binary missing from staged payload: $binary"
    }
}

# 3) Copy the installer script into the bundle root.
Copy-Item -LiteralPath $absoluteInstallerScript -Destination $installerDest -Force

# 4) Write a deterministic build manifest the install step can reference.
$manifest = [ordered]@{
    schema          = 't00-build-manifest/v1'
    configuration   = $Configuration
    runtime         = $Runtime
    serviceBinary   = 'payload/service/ControlParental.Service.exe'
    agentBinary     = 'payload/agent/ControlParental.SessionAgent.exe'
    installerScript = 'Install-ControlParentalService.ps1'
    builtAtUtc      = (Get-Date).ToUniversalTime().ToString('o')
}
$manifest | ConvertTo-Json -Depth 5 | Out-File -LiteralPath $manifestPath -Encoding utf8

Write-Host "[Build-ServiceInstaller] OK: bundle at $absoluteOutputRoot"
exit 0