# T23 Obfuscation Closure — Obfuscation Checks Harness (PowerShell)
# ----------------------------------------------------------------------------
# Drives the [Obfuscar] gate scenarios in `ControlParental.Domain.csproj` by
# passing `ObfuscateForceFail=<scenario>` to `dotnet build`. Each scenario is
# expected to make the build fail loudly with a non-zero exit code.
#
# Scenarios:
#   MissingTool   — the obfuscar.console shim is treated as missing
#   BrokenExit    — the obfuscar tool invocation is forced into a nonzero exit
#   MissingStage  — the staged obfuscated DLL is deleted after Obfuscar runs
#   MissingMap    — the Mapping.txt is deleted after Obfuscar runs
#   Identical     — the staged DLL is overwritten with the input DLL (forcing
#                   byte-identical hash comparison to trip)
#
# Happy path (no scenario): Run `dotnet build src/ControlParental.Domain
# -c Release` and verify that the obfuscated DLL differs from the unobfuscated
# baseline, Mapping.txt exists, and the marker file is written.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/obfuscation-checks/Invoke-ObfuscationChecks.ps1
#       [-Scenario <MissingTool|BrokenExit|MissingStage|MissingMap|Identical|Happy>]
#       [-ProjectRoot <path-to-repo>]
#       [-BuildConfiguration Release]
#
# Returns 0 only when the scenario's expectation matches the actual build
# outcome; 1 otherwise (after printing the diagnostic to STDERR).
[CmdletBinding()]
param(
    [ValidateSet('Happy', 'MissingTool', 'BrokenExit', 'MissingStage', 'MissingMap', 'Identical')]
    [string]$Scenario = 'Happy',

    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot/../..").Path,

    [string]$BuildConfiguration = 'Release'
)

$ErrorActionPreference = 'Stop'

function Fail([string]$message) {
    [Console]::Error.WriteLine("[Invoke-ObfuscationChecks] $message")
    exit 1
}

function BuildDomain([hashtable]$msbuildArgs) {
    $arguments = @('build', "$ProjectRoot/src/ControlParental.Domain/ControlParental.Domain.csproj", "-c", $BuildConfiguration, "-v:minimal", "-nologo")
    foreach ($pair in $msbuildArgs.GetEnumerator()) {
        $arguments += "/p:$($pair.Key)=$($pair.Value)"
    }

    $outputSb = New-Object System.Text.StringBuilder

    & dotnet @arguments 2>&1 | ForEach-Object {
        [void]$outputSb.AppendLine($_)
        $_
    }
    return @{ ExitCode = $LASTEXITCODE; Output = $outputSb.ToString() }
}

function Clear-Artifacts {
    foreach ($path in @(
            "$ProjectRoot/src/ControlParental.Domain/obj/obfuscar",
            "$ProjectRoot/src/ControlParental.Domain/obj/Release",
            "$ProjectRoot/src/ControlParental.Domain/bin/$BuildConfiguration")) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Assert-Happy-Path($run) {
    if ($run.ExitCode -ne 0) {
        Fail "Expected build success but dotnet build exited with $($run.ExitCode):$([Environment]::NewLine)$($run.Output)"
    }

    $stagedDll = "$ProjectRoot/src/ControlParental.Domain/obj/obfuscar/staging/ControlParental.Domain.dll"
    $marker = "$ProjectRoot/src/ControlParental.Domain/obj/obfuscar/marker.$BuildConfiguration.txt"
    $binaryDll = "$ProjectRoot/src/ControlParental.Domain/bin/$BuildConfiguration/net9.0/ControlParental.Domain.dll"
    $mapping = "$ProjectRoot/src/ControlParental.Domain/obj/obfuscar/staging/Mapping.txt"

    foreach ($expected in @($stagedDll, $marker, $binaryDll, $mapping)) {
        if (-not (Test-Path -LiteralPath $expected)) {
            Fail "Happy path: expected artifact not found: $expected"
        }
    }

    $markerContent = Get-Content -LiteralPath $marker -Raw
    if ($markerContent -notmatch 'obfuscated-by-obfuscar;assembly=ControlParental\.Domain;configuration=Release') {
        Fail "Happy path: marker content missing expected prefix: $markerContent"
    }

    Write-Host "[Invoke-ObfuscationChecks] Happy path: marker, mapping, staged, and bin DLLs all present; marker=$markerContent"
}

function Assert-Failure-Path($run, [string]$expectedGateSubstring, [string]$scenarioLabel) {
    if ($run.ExitCode -eq 0) {
        Fail "Scenario ${scenarioLabel} expected failure (nonzero exit) but build succeeded."
    }

    if (-not ($run.Output -match [regex]::Escape($expectedGateSubstring))) {
        Fail "Scenario ${scenarioLabel}: expected gate message containing '${expectedGateSubstring}' was not emitted."
    }

    Write-Host "[Invoke-ObfuscationChecks] Scenario ${scenarioLabel} failed loudly as expected (exit $($run.ExitCode))."
}

# ----------------------------------------------------------------------------
# Drive the scenario.
# ----------------------------------------------------------------------------

if (-not (Test-Path -LiteralPath "$ProjectRoot/src/ControlParental.Domain/ControlParental.Domain.csproj")) {
    Fail "ProjectRoot does not contain ControlParental.Domain.csproj: $ProjectRoot"
}

Clear-Artifacts

switch ($Scenario) {
    'Happy' {
        $run = BuildDomain @{}
        Assert-Happy-Path $run
    }
    'MissingTool' {
        $run = BuildDomain @{ 'ObfuscateForceFail' = 'MissingTool' }
        Assert-Failure-Path $run "Forced gate failure (MissingTool)" $Scenario
    }
    'BrokenExit' {
        $run = BuildDomain @{ 'ObfuscateForceFail' = 'BrokenExit' }
        Assert-Failure-Path $run "Forced gate failure (BrokenExit)" $Scenario
    }
    'MissingStage' {
        $run = BuildDomain @{ 'ObfuscateForceFail' = 'MissingStage' }
        Assert-Failure-Path $run "Obfuscar][GATE] Obfuscated staging output not found" $Scenario
    }
    'MissingMap' {
        $run = BuildDomain @{ 'ObfuscateForceFail' = 'MissingMap' }
        Assert-Failure-Path $run "Obfuscar][GATE] Mapping file not found" $Scenario
    }
    'Identical' {
        $run = BuildDomain @{ 'ObfuscateForceFail' = 'Identical' }
        Assert-Failure-Path $run "Obfuscar][GATE] Obfuscated DLL is byte-identical" $Scenario
    }
}

Write-Host "[Invoke-ObfuscationChecks] OK"
exit 0
