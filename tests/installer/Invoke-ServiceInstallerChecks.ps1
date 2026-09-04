# Invoke-ServiceInstallerChecks.ps1
# ----------------------------------------------------------------------------
# Script-contract harness for the T00 service installer closure.
#
# Each scenario:
#   * Builds a self-contained mock bundle under a temp directory.
#   * Stages either real binaries, deliberately missing binaries, or a
#     mock sc.exe that simulates a specific failure (controlled via the
#     $env:MOCK_SC_FAIL_STEP environment variable).
#   * Invokes Install-ControlParentalService.ps1 with overrides that bypass
#     elevation and (by default) the SCM RUNNING query.
#   * Asserts the installer's exit code and the produced receipt contents
#     match the scenario's expectation.
#
# The harness is intentionally non-elevated and does NOT touch the real SCM,
# the real %ProgramFiles%, or any project-managed directory. It exits 0 when
# the scenario's expectation matches the actual install behavior; 1 otherwise
# (after printing the diagnostic to STDERR).
#
# Scenarios:
#   Happy                — both binaries present, mock sc.exe succeeds, install
#                          succeeds, receipt.Status=Installed, authenticodeStatus
#                          matches the actual Get-AuthenticodeSignature of the
#                          copied stub.
#   MissingServiceExe    — payload/service/ControlParental.Service.exe absent;
#                          installer fails on preflight with explicit error.
#   MissingAgentExe      — payload/agent/ControlParental.SessionAgent.exe absent;
#                          installer fails on preflight with explicit error.
#   ScCreateFailure      — mock sc.exe returns nonzero on create; installer
#                          reports sc-create failure with command output.
#   ScStartFailure       — mock sc.exe returns nonzero on start; installer
#                          reports sc-start failure.
#   ScQueryFailure       — mock sc.exe returns 0 for create/config/failure/start
#                          but the query does not include RUNNING; installer
#                          reports sc-query failure.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass `
#       -File tests/installer/Invoke-ServiceInstallerChecks.ps1 `
#       [-Scenario <name>] [-ProjectRoot <path>] [-KeepTemp]

[CmdletBinding()]
param(
    [ValidateSet('Happy','MissingServiceExe','MissingAgentExe','ScCreateFailure','ScStartFailure','ScQueryFailure')]
    [string]$Scenario = 'Happy',

    [string]$ProjectRoot,

    [switch]$KeepTemp
)

$ErrorActionPreference = 'Stop'

if (-not $ProjectRoot) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Fail([string]$message) {
    [Console]::Error.WriteLine("[Invoke-ServiceInstallerChecks] $message")
    exit 1
}

$installer = Join-Path $ProjectRoot 'build/installer/Install-ControlParentalService.ps1'
if (-not (Test-Path -LiteralPath $installer)) {
    Fail "Installer script not found: $installer"
}

function New-TempBundle {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("cp-installer-" + [System.Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $tempRoot 'payload/service') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $tempRoot 'payload/agent') -Force | Out-Null
    return $tempRoot
}

function Write-Stub-Binary([string]$path) {
    # A 1-byte placeholder. The exact Authenticode status Windows reports
    # for a 0-byte file varies (newly created temp files in user dirs can
    # receive a Windows production signature promotion); the assertion is
    # therefore that the receipt faithfully mirrors Get-AuthenticodeSignature
    # rather than asserting a specific status string.
    [System.IO.File]::WriteAllBytes($path, @(0x00))
}

function Write-TestConfiguration([string]$path) {
    @('SUPABASE_URL=https://example.supabase.co', 'SUPABASE_ANON_KEY=sb_publishable_test-key') |
        Set-Content -LiteralPath $path -Encoding utf8
}

function Write-MockSc([string]$dir) {
    # A .cmd shim that simulates sc.exe behavior. Which step fails is
    # controlled by the MOCK_SC_FAIL_STEP environment variable and only
    # triggers when the actual sc.exe subcommand matches:
    #   <unset> | all    — every step succeeds; query output contains RUNNING.
    #   create           — the 'create' subcommand returns exit 1.
    #   config           — the 'config' subcommand returns exit 1.
    #   failure          — the 'failure' subcommand returns exit 1.
    #   start            — the 'start' subcommand returns exit 1.
    #   query            — start succeeds but query output does not include
    #                      RUNNING (service stays STOPPED).
    #
    # The mock reports the service as absent on query whenever
    # MOCK_SC_FAIL_STEP is set, so the installer's idempotent existence
    # check does not short-circuit the failing step. When MOCK_SC_FAIL_STEP
    # is unset, query reports RUNNING so the happy path can flow through.
    #
    # Each branch ends with `goto :done` instead of `exit /b N` inside a
    # parenthesized block, because cmd.exe's exit /b inside a (...) block
    # only exits the block; control falls through and the trailing exit
    # code is overwritten.
    $cmdPath = Join-Path $dir 'sc.cmd'
    $lines = @(
        '@echo off',
        'setlocal',
        'set CMD=%1',
        'if "%CMD%"=="query" goto :cmd_query',
        'if "%MOCK_SC_FAIL_STEP%"=="%CMD%" goto :cmd_fail',
        'if "%CMD%"=="create" ( echo [mock-sc] create OK & goto :done )',
        'if "%CMD%"=="config" ( echo [mock-sc] config OK & goto :done )',
        'if "%CMD%"=="failure" ( echo [mock-sc] failure OK & goto :done )',
        'if "%CMD%"=="start" ( echo [mock-sc] start OK & goto :done )',
        'if "%CMD%"=="stop" ( echo [mock-sc] stop OK & goto :done )',
        'echo [mock-sc] unhandled args: %* 1>&2',
        'exit /b 1',
        ':cmd_fail',
        'echo [mock-sc] %CMD% failure 1>&2',
        'exit /b 1',
        ':cmd_query',
        'if "%MOCK_SC_FAIL_STEP%"=="query" (',
        '    echo SERVICE_NAME: ControlParental & echo STATE              : 1 STOPPED',
        '    exit /b 0',
        ')',
        'if not "%MOCK_SC_FAIL_STEP%"=="" (',
        '    echo [mock-sc] query: service absent 1>&2',
        '    exit /b 1',
        ')',
        'echo SERVICE_NAME: ControlParental & echo STATE              : 4 RUNNING',
        'exit /b 0',
        ':done',
        'exit /b 0'
    )
    $lines -join "`r`n" | Out-File -LiteralPath $cmdPath -Encoding ascii
    return $cmdPath
}

function Write-MockAcl([string]$dir) {
    $cmdPath = Join-Path $dir 'icacls.cmd'
    "@echo off`r`nexit /b 0" | Out-File -LiteralPath $cmdPath -Encoding ascii
    return $cmdPath
}

function Invoke-Installer {
    param(
        [string]$BundleRoot,
        [string]$InstallRoot,
        [string]$ScCmdPath,
        [string]$AclCmdPath,
        [string]$ReceiptPath,
        [string]$DataRoot,
        [hashtable]$Env,
        [switch]$RunRunningCheck
    )

    $envBlock = @{}
    foreach ($e in $Env.GetEnumerator()) { $envBlock[$e.Key] = $e.Value }

    # Persist the current env so we can restore after the run; we cannot
    # blanket-clear because the dotnet/PowerShell host depends on PATH etc.
    $previous = @{}
    foreach ($k in $envBlock.Keys) { $previous[$k] = [Environment]::GetEnvironmentVariable($k) }

    try {
        foreach ($k in $envBlock.Keys) {
            [Environment]::SetEnvironmentVariable($k, $envBlock[$k])
        }

        $argumentList = @(
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-File', $installer,
            '-BundleRoot', $BundleRoot,
            '-InstallRoot', $InstallRoot,
            '-ScExe', $ScCmdPath,
            '-AclExe', $AclCmdPath,
            '-ReceiptPath', $ReceiptPath,
            '-DataRoot', $DataRoot,
            '-ConfigFile', (Join-Path $DataRoot 'input.env'),
            '-SkipElevationCheck',
            '-Force'
        )
        if (-not $RunRunningCheck) {
            $argumentList += '-SkipRunningCheck'
        }

        # Use System.Diagnostics.Process directly so that the child's nonzero
        # exit code surfaces as a captured exit code rather than a PowerShell
        # native-command terminating error.
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = 'powershell.exe'
        $rendered = @()
        foreach ($a in $argumentList) {
            if ($a -match '\s') {
                $rendered += '"' + $a + '"'
            } else {
                $rendered += $a
            }
        }
        $psi.Arguments = $rendered -join ' '
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true

        $proc = [System.Diagnostics.Process]::Start($psi)
        $stdout = $proc.StandardOutput.ReadToEnd()
        $stderr = $proc.StandardError.ReadToEnd()
        $proc.WaitForExit()

        $combined = ($stdout + "`n" + $stderr).TrimEnd()
        return @{
            ExitCode = $proc.ExitCode
            Output   = $combined
        }
    } finally {
        foreach ($k in $envBlock.Keys) {
            [Environment]::SetEnvironmentVariable($k, $previous[$k])
        }
    }
}

function Assert-Happy-Path([hashtable]$run, [string]$receiptPath, [string]$installedServiceExe) {
    if ($run.ExitCode -ne 0) {
        Fail "Happy: expected installer success but exit was $($run.ExitCode):`n$($run.Output)"
    }
    if (-not (Test-Path -LiteralPath $receiptPath)) {
        Fail "Happy: receipt not produced at $receiptPath"
    }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ($receipt.schema -ne 't00-install-receipt/v1') {
        Fail "Happy: unexpected receipt schema: $($receipt.schema)"
    }
    if ($receipt.status -ne 'Installed') {
        Fail "Happy: expected status=Installed, got $($receipt.status)"
    }
    if ($receipt.scmState -notin @('RUNNING', 'SKIPPED')) {
        Fail "Happy: expected scmState RUNNING or SKIPPED, got $($receipt.scmState)"
    }
    if ($receipt.serviceBinaryPath -notmatch 'ControlParental\.Service\.exe$') {
        Fail "Happy: serviceBinaryPath does not end with ControlParental.Service.exe: $($receipt.serviceBinaryPath)"
    }
    if ($receipt.agentBinaryPath -notmatch 'ControlParental\.SessionAgent\.exe$') {
        Fail "Happy: agentBinaryPath does not end with ControlParental.SessionAgent.exe: $($receipt.agentBinaryPath)"
    }

    # The receipt must report the Authenticode status of the actually
    # installed binary. The stub binary's status varies on Windows
    # (temp files in user dirs get a Windows production PCA promotion), so
    # the contract is that the receipt matches reality — not a specific value.
    if (-not (Test-Path -LiteralPath $installedServiceExe)) {
        Fail "Happy: installed service binary missing for Authenticode verification: $installedServiceExe"
    }
    $actualStatus = [string](Get-AuthenticodeSignature -FilePath $installedServiceExe).Status
    if ($receipt.authenticodeStatus -ne $actualStatus) {
        Fail "Happy: receipt.authenticodeStatus=$($receipt.authenticodeStatus) does not match actual Get-AuthenticodeSignature status=$actualStatus"
    }

    Write-Host "[Invoke-ServiceInstallerChecks] Happy: receipt valid; status=$($receipt.status), scmState=$($receipt.scmState), authenticode=$($receipt.authenticodeStatus)"
}

function Assert-Failure(
    [hashtable]$run,
    [string]$expectedSubstring,
    [string]$scenarioLabel,
    [string]$receiptPath) {

    if ($run.ExitCode -eq 0) {
        Fail "Scenario ${scenarioLabel} expected nonzero exit but installer succeeded."
    }
    if (-not ($run.Output -match [regex]::Escape($expectedSubstring))) {
        Fail "Scenario ${scenarioLabel}: expected output to contain '${expectedSubstring}' but did not. Output:`n$($run.Output)"
    }
    if (-not (Test-Path -LiteralPath $receiptPath)) {
        Fail "Scenario ${scenarioLabel}: best-effort receipt missing at $receiptPath"
    }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ($receipt.status -ne 'Failed') {
        Fail "Scenario ${scenarioLabel}: expected receipt.status=Failed, got $($receipt.status)"
    }
    Write-Host "[Invoke-ServiceInstallerChecks] Scenario ${scenarioLabel} failed loudly as expected (exit $($run.ExitCode))."
}

# --- Build the temp bundle and run the scenario --------------------------------
$tempRoot = $null
try {
    $tempRoot = New-TempBundle
    $installRoot = Join-Path $tempRoot 'install'
    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    $dataRoot = Join-Path $tempRoot 'data'
    New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
    Write-TestConfiguration (Join-Path $dataRoot 'input.env')
    $receiptPath = Join-Path $tempRoot 'install-receipt.json'
    $scCmdPath = Write-MockSc $tempRoot
    $aclCmdPath = Write-MockAcl $tempRoot

    $serviceExe = Join-Path $tempRoot 'payload/service/ControlParental.Service.exe'
    $agentExe = Join-Path $tempRoot 'payload/agent/ControlParental.SessionAgent.exe'

    switch ($Scenario) {
        'Happy' {
            Write-Stub-Binary $serviceExe
            Write-Stub-Binary $agentExe
            $run = Invoke-Installer -BundleRoot $tempRoot -InstallRoot $installRoot -ScCmdPath $scCmdPath -AclCmdPath $aclCmdPath -ReceiptPath $receiptPath -DataRoot $dataRoot -Env @{}
            Assert-Happy-Path $run $receiptPath (Join-Path $installRoot 'ControlParental.Service.exe')
        }
        'MissingServiceExe' {
            # Service exe intentionally NOT staged.
            Write-Stub-Binary $agentExe
            $run = Invoke-Installer -BundleRoot $tempRoot -InstallRoot $installRoot -ScCmdPath $scCmdPath -AclCmdPath $aclCmdPath -ReceiptPath $receiptPath -DataRoot $dataRoot -Env @{}
            Assert-Failure $run 'preflight: Staged payload missing' $Scenario $receiptPath
        }
        'MissingAgentExe' {
            Write-Stub-Binary $serviceExe
            # Agent exe intentionally NOT staged.
            $run = Invoke-Installer -BundleRoot $tempRoot -InstallRoot $installRoot -ScCmdPath $scCmdPath -AclCmdPath $aclCmdPath -ReceiptPath $receiptPath -DataRoot $dataRoot -Env @{}
            Assert-Failure $run 'preflight: Staged payload missing' $Scenario $receiptPath
        }
        'ScCreateFailure' {
            Write-Stub-Binary $serviceExe
            Write-Stub-Binary $agentExe
            $run = Invoke-Installer -BundleRoot $tempRoot -InstallRoot $installRoot -ScCmdPath $scCmdPath -AclCmdPath $aclCmdPath -ReceiptPath $receiptPath -DataRoot $dataRoot -Env @{ 'MOCK_SC_FAIL_STEP' = 'create' }
            Assert-Failure $run 'sc-create: sc.exe create failed' $Scenario $receiptPath
        }
        'ScStartFailure' {
            Write-Stub-Binary $serviceExe
            Write-Stub-Binary $agentExe
            $run = Invoke-Installer -BundleRoot $tempRoot -InstallRoot $installRoot -ScCmdPath $scCmdPath -AclCmdPath $aclCmdPath -ReceiptPath $receiptPath -DataRoot $dataRoot -Env @{ 'MOCK_SC_FAIL_STEP' = 'start' }
            Assert-Failure $run 'sc-start: sc.exe start failed' $Scenario $receiptPath
        }
        'ScQueryFailure' {
            Write-Stub-Binary $serviceExe
            Write-Stub-Binary $agentExe
            # ScQueryFailure deliberately exercises the RUNNING query gate:
            # do NOT pass -SkipRunningCheck; let the installer probe state.
            $run = Invoke-Installer -BundleRoot $tempRoot -InstallRoot $installRoot -ScCmdPath $scCmdPath -AclCmdPath $aclCmdPath -ReceiptPath $receiptPath -DataRoot $dataRoot -Env @{ 'MOCK_SC_FAIL_STEP' = 'query' } -RunRunningCheck
            Assert-Failure $run 'sc-query: Service did not reach RUNNING state' $Scenario $receiptPath
        }
    }
} finally {
    if ($tempRoot -and -not $KeepTemp -and (Test-Path -LiteralPath $tempRoot)) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    } elseif ($tempRoot -and $KeepTemp) {
        Write-Host "[Invoke-ServiceInstallerChecks] Keeping temp bundle at $tempRoot"
    }
}

Write-Host "[Invoke-ServiceInstallerChecks] OK"
exit 0