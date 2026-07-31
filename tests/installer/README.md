# T00 Service Installer — Script Contract Harness

This harness exercises the **Install-ControlParentalService.ps1** script
contract without requiring elevation, a real SCM, or `sc.exe` on a clean
Windows machine. It exists so that the installer's fail-loud branches are
verifiable in CI and locally on a dev box.

## What the harness exercises

Each scenario builds a self-contained mock bundle under a temp directory,
stubs `ControlParental.Service.exe` / `ControlParental.SessionAgent.exe`,
drops a mock `sc.cmd` shim that can be steered to fail at a specific step,
and invokes `Install-ControlParentalService.ps1` with overrides that
bypass the elevation check and the post-install `RUNNING` query. It then
asserts the installer exit code, the diagnostic substring, and the receipt
contents (`status`, `scmState`, `authenticodeStatus`, paths).

| Scenario            | Simulation                                              | Expected installer behavior                                          |
|---------------------|---------------------------------------------------------|----------------------------------------------------------------------|
| `Happy`             | both binaries present, mock sc.exe succeeds             | exit 0; receipt.status=`Installed`, authenticode=`NotSigned`          |
| `MissingServiceExe` | payload/service/ControlParental.Service.exe absent     | exit nonzero; output contains `preflight: Staged payload missing`    |
| `MissingAgentExe`   | payload/agent/ControlParental.SessionAgent.exe absent   | exit nonzero; output contains `preflight: Staged payload missing`    |
| `ScCreateFailure`   | mock sc.exe returns nonzero on create                  | exit nonzero; output contains `sc-create: sc.exe create failed`      |
| `ScStartFailure`    | mock sc.exe returns nonzero on start                    | exit nonzero; output contains `sc-start: sc.exe start failed`        |
| `ScQueryFailure`    | mock sc.exe succeeds on start but reports STOPPED query | exit nonzero; output contains `sc-query: Service did not reach RUNNING` |

The mock `sc.cmd` lives only inside the harness's temp directory; it never
replaces the real `sc.exe`.

## Running it

From the repository root, using Windows PowerShell 5.1 (the script is
PowerShell 5.1 compatible because the project pins .NET 9 / Windows):

```powershell
# Happy path
powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/installer/Invoke-ServiceInstallerChecks.ps1 `
     -Scenario Happy

# Each failure scenario
powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/installer/Invoke-ServiceInstallerChecks.ps1 `
     -Scenario MissingServiceExe

powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/installer/Invoke-ServiceInstallerChecks.ps1 `
     -Scenario MissingAgentExe

powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/installer/Invoke-ServiceInstallerChecks.ps1 `
     -Scenario ScCreateFailure

powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/installer/Invoke-ServiceInstallerChecks.ps1 `
     -Scenario ScStartFailure

powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/installer/Invoke-ServiceInstallerChecks.ps1 `
     -Scenario ScQueryFailure
```

The harness exits 0 when the scenario's expectation matches the actual
install behavior; 1 otherwise (after printing the diagnostic to STDERR).

## What the harness does **NOT** cover

Clean-machine integration testing (real elevation, real `sc.exe`, real
`%ProgramFiles%\ControlParental\Agent`, real `RUNNING` query, real ACLs
applied by the service's `ApplyHardeningAsync` startup) remains a manual
gate that the release operator runs on a fresh Windows machine against
the bundle produced by `Build-ServiceInstaller.ps1`. The App.UI MSIX flow
remains validated by the unchanged `Build-MSIX.ps1` invocation.