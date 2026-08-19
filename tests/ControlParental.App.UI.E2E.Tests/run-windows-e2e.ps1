param(
    [Parameter(Mandatory = $true)][string]$AppPath,
    [Parameter(Mandatory = $true)][string]$AppiumUrl,
    [string]$EvidenceDirectory = "$env:TEMP\ControlParental\e2e-evidence",
    [string]$AppiumHome = $env:APPIUM_HOME
)

$ErrorActionPreference = 'Stop'
$appiumVersion = '3.6.0'
$windowsDriverVersion = '6.1.1'
$deadline = [DateTime]::UtcNow.AddSeconds(180)

function Require-Step([string]$Name, [scriptblock]$Action) {
    if ([DateTime]::UtcNow -gt $deadline) { throw "E2E runbook timeout before $Name." }
    & $Action
}

try {
    Require-Step 'Windows interactive desktop' { if (-not [Environment]::UserInteractive) { throw 'Interactive desktop required; Session 0 is forbidden.' } }
    Require-Step 'App path' { if (-not (Test-Path -LiteralPath $AppPath -PathType Leaf)) { throw "App not found: $AppPath" } }
    Require-Step 'Appium URL' {
        $parsedAppiumUrl = $null
        if (-not [Uri]::TryCreate($AppiumUrl, [UriKind]::Absolute, [ref]$parsedAppiumUrl) -or $parsedAppiumUrl.Scheme -notin @('http', 'https')) { throw "Invalid Appium URL: $AppiumUrl" }
    }
    Require-Step 'Appium home' {
        if ([string]::IsNullOrWhiteSpace($AppiumHome)) { throw 'APPIUM_HOME must be set for an explicit external Appium invocation.' }
        if (-not (Test-Path -LiteralPath $AppiumHome -PathType Container)) { throw "APPIUM_HOME is not a directory: $AppiumHome" }
        $env:APPIUM_HOME = $AppiumHome
    }
    Require-Step 'external Appium and pinned driver' {
        $null = Get-Command npx -ErrorAction Stop
        try { $status = Invoke-RestMethod -Uri "$($AppiumUrl.TrimEnd('/'))/status" -TimeoutSec 3 } catch { throw "External Appium $appiumVersion / Windows driver $windowsDriverVersion is unavailable at $AppiumUrl." }
        if ($null -eq $status) { throw "External Appium returned no status at $AppiumUrl." }
    }
    Require-Step 'ControlParental.Service IPC' {
        $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'ControlParental.UI', [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::Asynchronous)
        try { $pipe.Connect(500) } catch { throw 'E2E BLOCKED: ControlParental.Service IPC is unavailable: named pipe ControlParental.UI is not ready.' } finally { $pipe.Dispose() }
    }

    New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
    $env:CONTROL_PARENTAL_APP_PATH = $AppPath
    $env:CONTROL_PARENTAL_APPIUM_URL = $AppiumUrl
    $env:CONTROL_PARENTAL_E2E_EVIDENCE = $EvidenceDirectory
    $env:E2E_REQUIRED = '1'
    $env:CONTROL_PARENTAL_E2E_AUTOMATION_NAME = 'NovaWindows2'

    Write-Host "Compatible spike: Appium $appiumVersion + Windows driver $windowsDriverVersion."
    Write-Host 'Explicit invocation only: this project is intentionally excluded from ControlParental.sln.'
    Require-Step 'App.UI E2E contract' {
        $test = Start-Process dotnet -ArgumentList @('test', 'tests/ControlParental.App.UI.E2E.Tests/ControlParental.App.UI.E2E.Tests.csproj', '--no-restore', '--no-build', '-p:Platform=x64', '--logger', 'console;verbosity=minimal') -PassThru -NoNewWindow
        $completed = $test.WaitForExit(150000)
        if (-not $completed) {
            & taskkill.exe /PID $test.Id /T /F *> $null
            throw 'E2E command exceeded the 150 second step budget.'
        }

        $test.WaitForExit()
        $test.Refresh()
        $exitCode = [int]$test.ExitCode
        if ($exitCode -ne 0) { throw "E2E contract failed with exit code $exitCode." }
    }
}
finally {
    Write-Host "Cleanup: W3C session DELETE terminates only the app launched by that session; external Appium and ControlParental.Service are never stopped. Evidence = $EvidenceDirectory"
    Write-Host 'ExternalVerified=false: this is only the compatible-environment spike, not the 6.4 OS/architecture matrix.'
}

exit 0
