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

    [string]$AclExe = 'icacls.exe',

    [string]$ReceiptPath,

    # File containing SUPABASE_URL and SUPABASE_ANON_KEY. Never pass secrets
    # as command-line values. If omitted, an existing DataRoot\\.env is reused.
    [string]$ConfigFile,

    [string]$DataRoot,

    # Reinstall even if the service already exists (config + failure actions reapplied).
    [switch]$Force,

    # Skip the elevation check (used by the test harness only).
    [switch]$SkipElevationCheck,

    # Skip the post-install RUNNING query (used by the test harness only).
    [switch]$SkipRunningCheck
)

$ErrorActionPreference = 'Stop'
$createdService = $false
$installBackup = $null
$configBackup = $null
$configExisted = $false
$serviceWasRunning = $false

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
    if ($createdService -and (Get-Command Invoke-Sc -ErrorAction SilentlyContinue)) {
        try {
            $stopResult = Invoke-Sc "stop $quotedServiceName"
            $deleteResult = Invoke-Sc "delete $quotedServiceName"
            if ($stopResult.ExitCode -ne 0 -or $deleteResult.ExitCode -ne 0) {
                throw 'SCM cleanup returned a nonzero exit code'
            }
        } catch {
            [Console]::Error.WriteLine('[Install-ControlParentalService] rollback: service cleanup failed')
        }
    }
    if ($serviceWasRunning -and (Get-Command Invoke-Sc -ErrorAction SilentlyContinue)) {
        try { $null = Invoke-Sc "start $quotedServiceName" } catch { }
    }
    try {
        if ($installBackup -and (Test-Path -LiteralPath $installBackup)) {
            if (Test-Path -LiteralPath $InstallRoot) { Remove-Item -LiteralPath $InstallRoot -Recurse -Force }
            Copy-Item -LiteralPath $installBackup -Destination $InstallRoot -Recurse -Force
        }
        $configDestination = Join-Path $script:DataRoot '.env'
        if ($script:configExisted -and $script:configBackup -and (Test-Path -LiteralPath $script:configBackup)) {
            Copy-Item -LiteralPath $script:configBackup -Destination $configDestination -Force
        } elseif (-not $script:configExisted -and (Test-Path -LiteralPath $configDestination)) {
            Remove-Item -LiteralPath $configDestination -Force
        }
        if ($script:configBackup -and (Test-Path -LiteralPath $script:configBackup)) { Remove-Item -LiteralPath $script:configBackup -Force }
    } catch {
        [Console]::Error.WriteLine('[Install-ControlParentalService] rollback: previous payload/config restore failed')
    }
    try {
        Write-Receipt -Status 'Failed' -State $step -AuthenticodeStatus 'UnknownError' -Notes $message -BestEffort
    } catch {
        # Best-effort: do not let a receipt write mask the original failure.
    }
    exit 1
}

function Provision-Configuration {
    if (-not $DataRoot) {
        $script:DataRoot = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'ControlParental'
    }
    if (-not $script:DataRoot) { $script:DataRoot = $DataRoot }
    if (-not (Test-Path -LiteralPath $script:DataRoot)) {
        New-Item -ItemType Directory -Path $script:DataRoot -Force | Out-Null
    }
    $destination = Join-Path $script:DataRoot '.env'
    if (Test-Path -LiteralPath $destination) {
        $script:configExisted = $true
        $script:configBackup = Join-Path $script:DataRoot '.env.rollback'
        Copy-Item -LiteralPath $destination -Destination $script:configBackup -Force
    }
    $source = if ($ConfigFile) { $ConfigFile } else { $destination }
    if (-not (Test-Path -LiteralPath $source)) {
        Fail 'Configuration file was not supplied and no existing service configuration was found.' 'configuration'
    }
    try {
        $contents = Get-Content -LiteralPath $source -Raw -ErrorAction Stop
    } catch {
        Fail 'Configuration file could not be read.' 'configuration'
    }
    $urlMatch = [regex]::Match($contents, '(?m)^\s*SUPABASE_URL\s*=\s*(\S+)\s*$')
    $keyMatch = [regex]::Match($contents, '(?m)^\s*SUPABASE_ANON_KEY\s*=\s*(\S+)\s*$')
    if (-not $urlMatch.Success -or -not $keyMatch.Success) {
        Fail 'Configuration must contain SUPABASE_URL and SUPABASE_ANON_KEY.' 'configuration'
    }
    $url = $urlMatch.Groups[1].Value.Trim('"')
    $key = $keyMatch.Groups[1].Value.Trim('"')
    $parsedUrl = $null
    $pinMatch = [regex]::Match($contents, '(?m)^\s*(SUPABASE_CERT_PINS|SUPABASE_CERT_PIN)\s*=\s*(\S+)\s*$')
    $pins = if ($pinMatch.Success) { $pinMatch.Groups[2].Value.Trim('"') } else { $null }
    $pinsValid = $true
    if ($pins) {
        foreach ($pin in $pins.Split(';')) {
            try {
                $bytes = [Convert]::FromBase64String($pin.Trim().Substring(7))
                if (-not $pin.Trim().StartsWith('sha256/') -or $bytes.Length -ne 32) { $pinsValid = $false }
            } catch { $pinsValid = $false }
        }
    }
    $keyLooksPublishable = $key -match '^sb_(publishable|anon)_[A-Za-z0-9._~-]{5,}$'
    $jwtParts = $key.Split('.')
    if (-not $keyLooksPublishable -and $jwtParts.Count -eq 3) {
        try {
            $payload = $jwtParts[1].Replace('-', '+').Replace('_', '/')
            $payload = $payload.PadRight($payload.Length + ((4 - $payload.Length % 4) % 4), '=')
            $claims = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
            $keyLooksPublishable = $claims.role -eq 'anon'
        } catch { $keyLooksPublishable = $false }
    }
    if (-not [Uri]::TryCreate($url, [UriKind]::Absolute, [ref]$parsedUrl) -or
        $parsedUrl.Scheme -ne 'https' -or [string]::IsNullOrWhiteSpace($parsedUrl.Host) -or
        -not $keyLooksPublishable -or $key -match '(?i)^sb_secret_|YOUR-' -or -not $pinsValid) {
        Fail 'Configuration is invalid; expected an HTTPS URL and publishable key.' 'configuration'
    }
    try {
        $outputLines = @("SUPABASE_URL=$url", "SUPABASE_ANON_KEY=$key")
        if ($pins) { $outputLines += "SUPABASE_CERT_PINS=$pins" }
        $outputLines |
            Set-Content -LiteralPath $destination -Encoding utf8 -Force -ErrorAction Stop
        & $AclExe $script:DataRoot /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)(F)' '*S-1-5-32-544:(OI)(CI)(F)' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'directory ACL failed' }
        & $AclExe $destination /inheritance:r /grant:r '*S-1-5-18:(F)' '*S-1-5-32-544:(F)' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'file ACL failed' }
    } catch {
        Fail 'Configuration could not be written with a restricted ACL.' 'configuration'
    }
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

Provision-Configuration

# --- Copy payload ------------------------------------------------------------
Write-Host "[Install-ControlParentalService] Copying payload to $InstallRoot"
try {
    if (Test-Path -LiteralPath $InstallRoot) {
        $installBackup = Join-Path ([IO.Path]::GetTempPath()) ('ControlParental-install-' + [Guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath $InstallRoot -Destination $installBackup -Recurse -Force
    }
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

function Get-ScState {
    $result = Invoke-Sc "query $quotedServiceName"
    if ($result.ExitCode -ne 0) { return 'ABSENT' }
    if ($result.Output -match 'RUNNING') { return 'RUNNING' }
    if ($result.Output -match 'STOP_PENDING') { return 'STOP_PENDING' }
    if ($result.Output -match 'START_PENDING') { return 'START_PENDING' }
    if ($result.Output -match 'STOPPED') { return 'STOPPED' }
    return 'UNKNOWN'
}

function Wait-ForRunning([int]$TimeoutSeconds = 30) {
    for ($attempt = 0; $attempt -lt $TimeoutSeconds; $attempt++) {
        if ((Get-ScState) -eq 'RUNNING') { return $true }
        Start-Sleep -Seconds 1
    }
    return $false
}

# --- Idempotent create -------------------------------------------------------
$quotedServiceName = '"' + $ServiceName + '"'
$quotedBinaryPath = '"' + $installedServiceExe + '"'

$initialState = Get-ScState
$exists = $initialState -ne 'ABSENT'
$serviceWasRunning = $initialState -eq 'RUNNING'

if (-not $exists) {
    Write-Host "[Install-ControlParentalService] Creating service $ServiceName -> $installedServiceExe"
    $create = Invoke-Sc "create $quotedServiceName binPath= $quotedBinaryPath"
    if ($create.ExitCode -ne 0) {
        Fail "sc.exe create failed (exit $($create.ExitCode)): $($create.Output)" 'sc-create'
    }
    $createdService = $true
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
if ($serviceWasRunning) {
    Write-Host "[Install-ControlParentalService] Restarting the existing service after payload replacement"
    $stop = Invoke-Sc "stop $quotedServiceName"
    if ($stop.ExitCode -ne 0) {
        Fail "sc.exe stop failed (exit $($stop.ExitCode)): $($stop.Output)" 'sc-stop'
    }
}
Write-Host "[Install-ControlParentalService] Starting service"
$start = Invoke-Sc "start $quotedServiceName"
if ($start.ExitCode -ne 0) {
    Fail "sc.exe start failed (exit $($start.ExitCode)): $($start.Output)" 'sc-start'
}

# --- query RUNNING -----------------------------------------------------------
$queriedState = 'UNKNOWN'
if (-not $SkipRunningCheck) {
    if (-not (Wait-ForRunning)) {
        $queriedState = Get-ScState
        Fail "Service did not reach RUNNING state. SCM reports: $queriedState." 'sc-query'
    }
    $queriedState = 'RUNNING'
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
if ($installBackup -and (Test-Path -LiteralPath $installBackup)) { Remove-Item -LiteralPath $installBackup -Recurse -Force -ErrorAction SilentlyContinue }
if ($script:configBackup -and (Test-Path -LiteralPath $script:configBackup)) { Remove-Item -LiteralPath $script:configBackup -Force -ErrorAction SilentlyContinue }

Write-Host "[Install-ControlParentalService] OK: $ServiceName is $queriedState (Authenticode = $authenticodeStatus)"
exit 0