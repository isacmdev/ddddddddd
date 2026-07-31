# Install-ControlParentalService.ps1
# ----------------------------------------------------------------------------
# Registers the ControlParental Windows service with the SCM using the
# payload staged by Build-ServiceInstaller.ps1.
#
# What this script does:
#   1. Verifies the staged payload contains ControlParental.Service.exe and
#      ControlParental.SessionAgent.exe.
#   2. Copies the staged payload under %ProgramFiles%\ControlParental\Agent
#      (override with -InstallRoot).
#   3. Creates the named service (default: ControlParental) idempotently.
#   4. Sets start= auto, three 60-second restart failure actions with a 24h
#      reset period.
#   5. Starts the service and queries `sc query`; fails loudly unless the
#      service reports RUNNING.
#   6. Writes an install-receipt.json with bundle/installed paths, queried
#      state, and the Authenticode status of the installed service binary
#      (Valid / NotSigned / UnknownError). It never implies production EV
#      signing when the binary is unsigned.
#
# The script reuses the existing Program.AgentFolderPath convention and
# leaves the runtime hardening path (AclHardener + ScmController recovery
# wiring invoked from Program.ApplyHardeningAsync) untouched: the service
# process continues to be authoritative for ACL/registry/failure-config
# state. This installer only performs SCM-level registration and verifies
# that the install landed in a running state.
#
# Usage (elevated):
#   powershell -NoProfile -ExecutionPolicy Bypass `
#       -File build/installer/Install-ControlParentalService.ps1 `
#       [-BundleRoot artifacts/service-installer] `
#       [-ServiceName ControlParental] `
#       [-InstallRoot "$env:ProgramFiles\ControlParental\Agent"] `
#       [-ScExe sc.exe] `
#       [-ReceiptPath artifacts/service-installer/install-receipt.json] `
#       [-Force] `
#       [-SkipElevationCheck] [-SkipRunningCheck]
#
# Returns 0 only when every step above succeeded. Any failure exits nonzero
# and writes the receipt (best-effort) with the failing step recorded.

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$BundleRoot,

    [string]$ServiceName = 'ControlParental',

    [string]$InstallRoot,

    # Override sc.exe path (used by the script-contract test harness).
    [string]$ScExe = 'sc.exe',

    [string]$ReceiptPath,

    # Reinstall even if the service already exists (config + failure actions reapplied).
    [switch]$Force,

    # Skip the elevation check (used by the test harness only).
    [switch]$SkipElevationCheck,

    # Skip the post-install RUNNING query (used by the test harness only).
    [switch]$SkipRunningCheck
)

$ErrorActionPreference = 'Stop'

function Write-Receipt(
    [string]$Status,
    [string]$State,
    [string]$AuthenticodeStatus,
    [string]$Notes,
    [switch]$BestEffort) {
    $payload = [ordered]@{
        schema              = 't00-install-receipt/v1'
        serviceName         = $ServiceName
        bundleRoot          = $absoluteBundleRoot
        installRoot         = $InstallRoot
        serviceBinaryPath   = Join-Path $InstallRoot 'ControlParental.Service.exe'
        agentBinaryPath     = Join-Path $InstallRoot 'ControlParental.SessionAgent.exe'
        scmState            = $State
        authenticodeStatus  = $AuthenticodeStatus
        status              = $Status
        notes               = $Notes
        producedAtUtc       = (Get-Date).ToUniversalTime().ToString('o')
    }
    $json = $payload | ConvertTo-Json -Depth 5
    if ($BestEffort) {
        try { $json | Out-File -LiteralPath $absoluteReceiptPath -Encoding utf8 } catch { }
    } else {
        $json | Out-File -LiteralPath $absoluteReceiptPath -Encoding utf8
    }
}

function Fail([string]$message, [string]$step) {
    [Console]::Error.WriteLine("[Install-ControlParentalService] $step`: $message")
    try {
        Write-Receipt -Status 'Failed' -State $step -AuthenticodeStatus 'UnknownError' -Notes $message -BestEffort
    } catch {
        # Best-effort: do not let a receipt write mask the original failure.
    }
    exit 1
}

# --- Determine bundle root ---------------------------------------------------
if (-not $BundleRoot) {
    $BundleRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
}
$absoluteBundleRoot = if ([System.IO.Path]::IsPathRooted($BundleRoot)) {
    $BundleRoot
} else {
    Join-Path (Get-Location).Path $BundleRoot
}
if (-not (Test-Path -LiteralPath $absoluteBundleRoot)) {
    Fail "BundleRoot not found: $absoluteBundleRoot" 'preflight'
}

# Resolve the receipt path BEFORE any preflight Fail() can run, so the
# best-effort receipt write has a valid destination.
if (-not $ReceiptPath) {
    $ReceiptPath = Join-Path $absoluteBundleRoot 'install-receipt.json'
}
$absoluteReceiptPath = if ([System.IO.Path]::IsPathRooted($ReceiptPath)) {
    $ReceiptPath
} else {
    Join-Path (Get-Location).Path $ReceiptPath
}

if (-not $InstallRoot) {
    $programFiles = [Environment]::GetFolderPath('ProgramFiles')
    $InstallRoot = Join-Path $programFiles 'ControlParental\Agent'
}
if (-not (Test-Path -LiteralPath $InstallRoot)) {
    New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
}

$servicePayload = Join-Path $absoluteBundleRoot 'payload/service'
$agentPayload = Join-Path $absoluteBundleRoot 'payload/agent'
$expectedServiceExe = Join-Path $servicePayload 'ControlParental.Service.exe'
$expectedAgentExe = Join-Path $agentPayload 'ControlParental.SessionAgent.exe'

foreach ($candidate in @($expectedServiceExe, $expectedAgentExe)) {
    if (-not (Test-Path -LiteralPath $candidate)) {
        Fail "Staged payload missing: $candidate" 'preflight'
    }
}

# --- Pre-flight: elevation + sc.exe availability -----------------------------
if (-not $SkipElevationCheck) {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Fail 'Script must run elevated (Administrator). Re-launch from an elevated PowerShell.' 'elevation'
    }
}

if (-not (Get-Command $ScExe -ErrorAction SilentlyContinue) -and -not (Test-Path -LiteralPath $ScExe)) {
    Fail "sc.exe not found at path: $ScExe" 'preflight'
}

# --- Copy payload ------------------------------------------------------------
Write-Host "[Install-ControlParentalService] Copying payload to $InstallRoot"
try {
    Copy-Item -Path "$servicePayload/*" -Destination $InstallRoot -Recurse -Force -ErrorAction Stop
} catch {
    Fail "Failed to copy service payload: $($_.Exception.Message)" 'copy'
}
try {
    Copy-Item -Path "$agentPayload/*" -Destination $InstallRoot -Recurse -Force -ErrorAction Stop
} catch {
    Fail "Failed to copy agent payload: $($_.Exception.Message)" 'copy'
}

$installedServiceExe = Join-Path $InstallRoot 'ControlParental.Service.exe'
$installedAgentExe = Join-Path $InstallRoot 'ControlParental.SessionAgent.exe'
if (-not (Test-Path -LiteralPath $installedServiceExe)) {
    Fail "Service binary missing after copy: $installedServiceExe" 'copy'
}
if (-not (Test-Path -LiteralPath $installedAgentExe)) {
    Fail "Agent binary missing after copy: $installedAgentExe" 'copy'
}

# --- Helper: invoke sc.exe and capture stdout/stderr/exit -------------------
function Invoke-Sc {
    param([string]$Arguments)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $ScExe
    $psi.Arguments = $Arguments
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()
    return @{
        ExitCode = $proc.ExitCode
        Output   = ($stdout + "`n" + $stderr).Trim()
    }
}

# --- Idempotent create -------------------------------------------------------
$quotedServiceName = '"' + $ServiceName + '"'
$quotedBinaryPath = '"' + $installedServiceExe + '"'

$query = Invoke-Sc "query $quotedServiceName"
$exists = $query.ExitCode -eq 0

if (-not $exists) {
    Write-Host "[Install-ControlParentalService] Creating service $ServiceName -> $installedServiceExe"
    $create = Invoke-Sc "create $quotedServiceName binPath= $quotedBinaryPath"
    if ($create.ExitCode -ne 0) {
        Fail "sc.exe create failed (exit $($create.ExitCode)): $($create.Output)" 'sc-create'
    }
} elseif ($Force) {
    Write-Host "[Install-ControlParentalService] Service already exists; -Force set, leaving create idempotent."
} else {
    Write-Host "[Install-ControlParentalService] Service already exists; skipping create (use -Force to reapply)."
}

# --- start= auto -------------------------------------------------------------
Write-Host "[Install-ControlParentalService] Configuring start= auto"
$startup = Invoke-Sc "config $quotedServiceName start= auto"
if ($startup.ExitCode -ne 0) {
    Fail "sc.exe config start=auto failed (exit $($startup.ExitCode)): $($startup.Output)" 'sc-config'
}

# --- failure actions: 3x 60s restart, 86400s reset --------------------------
Write-Host "[Install-ControlParentalService] Configuring failure actions (3x 60s restart, 86400s reset)"
$failure = Invoke-Sc "failure $quotedServiceName actions= restart/60000/restart/60000/restart/60000 reset= 86400"
if ($failure.ExitCode -ne 0) {
    Fail "sc.exe failure failed (exit $($failure.ExitCode)): $($failure.Output)" 'sc-failure'
}

# --- start -------------------------------------------------------------------
Write-Host "[Install-ControlParentalService] Starting service"
$start = Invoke-Sc "start $quotedServiceName"
# sc.exe start returns 0 on a START_PENDING outcome; treat as success.
if ($start.ExitCode -ne 0) {
    Fail "sc.exe start failed (exit $($start.ExitCode)): $($start.Output)" 'sc-start'
}

# --- query RUNNING -----------------------------------------------------------
$queriedState = 'UNKNOWN'
if (-not $SkipRunningCheck) {
    Start-Sleep -Seconds 2
    $runningQuery = Invoke-Sc "query $quotedServiceName"
    $queriedState = if ($runningQuery.Output -match 'RUNNING') {
        'RUNNING'
    } elseif ($runningQuery.Output -match 'STOP_PENDING') {
        'STOP_PENDING'
    } elseif ($runningQuery.Output -match 'START_PENDING') {
        'START_PENDING'
    } elseif ($runningQuery.Output -match 'STOPPED') {
        'STOPPED'
    } else {
        'UNKNOWN'
    }
    if ($queriedState -ne 'RUNNING') {
        Fail "Service did not reach RUNNING state. SCM reports: $queriedState. Output: $($runningQuery.Output)" 'sc-query'
    }
} else {
    $queriedState = 'SKIPPED'
}

# --- Authenticode probe ------------------------------------------------------
$authenticodeStatus = 'UnknownError'
try {
    $signature = Get-AuthenticodeSignature -FilePath $installedServiceExe
    if ($signature) {
        $authenticodeStatus = [string]$signature.Status
    }
} catch {
    $authenticodeStatus = 'UnknownError'
}

Write-Receipt -Status 'Installed' -State $queriedState -AuthenticodeStatus $authenticodeStatus -Notes 'Install completed successfully.'

Write-Host "[Install-ControlParentalService] OK: $ServiceName is $queriedState (Authenticode = $authenticodeStatus)"
exit 0