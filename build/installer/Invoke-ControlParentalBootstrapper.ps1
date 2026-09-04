# ControlParental bundle bootstrapper
# Installs the UI MSIX and the Service/SessionAgent bundle as one transaction.
# This is a lab/release mechanism; it does not claim EV signing or SmartScreen.
[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Install','Repair','Upgrade','Uninstall')]
    [string]$Action = 'Install',
    [string]$BundleRoot = $PSScriptRoot,
    [string]$MsixPath,
    [string]$ServiceInstaller = 'Install-ControlParentalService.ps1',
    [string]$ManifestPath = 'build-manifest.json',
    [string]$ServiceName = 'ControlParental',
    [string]$InstallRoot,
    [string]$ConfigFile,
    [string]$DataRoot,
    [switch]$AllowUnsignedLabBundle,
    [switch]$SkipElevationCheck
)

$ErrorActionPreference = 'Stop'
function Fail([string]$message) {
    [Console]::Error.WriteLine("[ControlParentalBootstrapper] $message")
    exit 1
}
function Resolve-BundlePath([string]$path) {
    if ([IO.Path]::IsPathRooted($path)) { return $path }
    return Join-Path $BundleRoot $path
}
function Invoke-Checked([string]$file, [string[]]$arguments) {
    & $file @arguments
    if ($LASTEXITCODE -ne 0) { Fail "operation failed (exit $LASTEXITCODE)" }
}
function Test-BundleIntegrity {
    $manifest = Resolve-BundlePath $ManifestPath
    if (-not (Test-Path -LiteralPath $manifest)) { Fail 'build manifest is missing' }
    try { $data = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json } catch { Fail 'build manifest is invalid' }
    $entries = @($data.msixPackage, $data.serviceBinary, $data.agentBinary, $data.installerScript, $data.bootstrapperScript)
    if ($entries.Count -ne 5 -or $entries | Where-Object { [string]::IsNullOrWhiteSpace($_) }) { Fail 'build manifest must list UI, service, agent, installer and bootstrapper' }
    if (-not $data.version -or -not $data.checksums) { Fail 'build manifest must include version and checksums' }
    if (-not $data.componentVersions -or
        [string]::IsNullOrWhiteSpace($data.componentVersions.ui) -or
        [string]::IsNullOrWhiteSpace($data.componentVersions.service) -or
        [string]::IsNullOrWhiteSpace($data.componentVersions.agent)) {
        Fail 'build manifest must include UI, service and agent versions'
    }
    foreach ($entry in $entries) {
        $path = Resolve-BundlePath $entry
        if (-not (Test-Path -LiteralPath $path)) { Fail 'manifest references a missing bundle file' }
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $expected = $null
        if ($data.checksums) { $expected = $data.checksums.$entry }
        if ([string]::IsNullOrWhiteSpace($expected) -or $actual -ne $expected.ToLowerInvariant()) { Fail 'bundle checksum validation failed' }
        $signature = Get-AuthenticodeSignature -FilePath $path
        if ($signature.Status -eq 'NotSigned' -and $AllowUnsignedLabBundle) { continue }
        if ($signature.Status -ne 'Valid') { Fail 'bundle Authenticode validation failed' }
    }
    return $data
}
function Install-Ui {
    if (-not $MsixPath) { Fail 'manifest must provide an MSIX package' }
    $resolved = Resolve-BundlePath $MsixPath
    if (-not (Test-Path -LiteralPath $resolved)) { Fail 'MSIX package is missing' }
    if ($PSCmdlet.ShouldProcess($resolved, 'Add-AppxPackage')) {
        Add-AppxPackage -Path $resolved -ForceApplicationShutdown -ErrorAction Stop
    }
}
function Remove-Ui {
    if ($PSCmdlet.ShouldProcess('ControlParental.App', 'Remove-AppxPackage')) {
        Get-AppxPackage -Name 'ControlParental.App' -ErrorAction SilentlyContinue |
            Remove-AppxPackage -ErrorAction Stop
    }
}

if (-not (Test-Path -LiteralPath $BundleRoot)) { Fail 'bundle root is missing' }
if ($Action -ne 'Uninstall') {
    $manifestData = Test-BundleIntegrity
    if (-not $MsixPath) { $MsixPath = $manifestData.msixPackage }
}
$installer = Resolve-BundlePath $ServiceInstaller
if (-not (Test-Path -LiteralPath $installer)) { Fail 'service installer is missing' }
$arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$installer,'-ServiceName',$ServiceName)
if ($InstallRoot) { $arguments += @('-InstallRoot',$InstallRoot) }
if ($ConfigFile) { $arguments += @('-ConfigFile',$ConfigFile) }
if ($DataRoot) { $arguments += @('-DataRoot',$DataRoot) }
if ($SkipElevationCheck) { $arguments += '-SkipElevationCheck' }

try {
    switch ($Action) {
        'Install' { if ($PSCmdlet.ShouldProcess($installer, 'Install service')) { Invoke-Checked 'powershell.exe' ($arguments + '-Force') }; Install-Ui }
        'Repair' { if ($PSCmdlet.ShouldProcess($installer, 'Repair service')) { Invoke-Checked 'powershell.exe' ($arguments + '-Force') }; Install-Ui }
        'Upgrade' { if ($PSCmdlet.ShouldProcess($installer, 'Upgrade service')) { Invoke-Checked 'powershell.exe' ($arguments + '-Force') }; Install-Ui }
        'Uninstall' {
            if ($PSCmdlet.ShouldProcess($ServiceName, 'Stop and delete service')) {
                & sc.exe stop $ServiceName | Out-Null
                & sc.exe delete $ServiceName | Out-Null
                if ($LASTEXITCODE -ne 0) { Fail 'service uninstall failed' }
            }
            Remove-Ui
        }
    }
} catch {
    [Console]::Error.WriteLine('[ControlParentalBootstrapper] transaction failed; existing installation was not deleted')
    exit 1
}
Write-Host "[ControlParentalBootstrapper] $Action completed"
exit 0
