# T23 Obfuscation Closure — Obfuscation Checks Harness

This directory contains the runtime harness for the **T23 Obfuscation Closure —
Unit 1** fail-loud gate scenarios. Static / structural assertions live in
`tests/ControlParental.Obfuscation.CheckTests/`.

## What the harness exercises

Each scenario is wired into `ControlParental.Domain.csproj` via the
`/p:ObfuscateForceFail=<scenario>` MSBuild property. When set, the
`ObfuscateDomain` target simulates the corresponding post-Obfuscar failure
and the next gate must fail the build loudly. The harness asserts exactly
that:

| Scenario     | Simulation                                                | Expected gate                                                              |
|--------------|-----------------------------------------------------------|----------------------------------------------------------------------------|
| `Happy`      | none                                                      | build exits 0, marker, Mapping.txt and obfuscated DLL all present          |
| `MissingTool`| pre-Obfuscar gate short-circuit                           | `[Obfuscar][GATE] Forced gate failure (MissingTool)`                       |
| `BrokenExit` | post-PowerShell short-circuit                              | `[Obfuscar][GATE] Forced gate failure (BrokenExit)`                        |
| `MissingStage` | post-Obfuscar `<Delete>` of the staged DLL               | `[Obfuscar][GATE] Obfuscated staging output not found`                     |
| `MissingMap`   | post-Obfuscar `<Delete>` of Mapping.txt                  | `[Obfuscar][GATE] Mapping file not found`                                  |
| `Identical`    | post-Obfuscar `Copy-Item` of input DLL over the staged   | `[Obfuscar][GATE] Obfuscated DLL is byte-identical to the input`           |

The harness is intentionally independent of the Domain.Tests xUnit project so
that future PR-2 work (publish smoke) can extend it without disturbing the
Domain.Tests suite.

## Running it

From the repository root, using Windows PowerShell 5.1 (the script is
PowerShell 5.1 compatible because the project pins .NET 9 / Windows):

```powershell
# Happy path
powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/obfuscation-checks/Invoke-ObfuscationChecks.ps1 `
     -Scenario Happy

# Each failure scenario
powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/obfuscation-checks/Invoke-ObfuscationChecks.ps1 `
     -Scenario MissingTool

powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/obfuscation-checks/Invoke-ObfuscationChecks.ps1 `
     -Scenario BrokenExit

powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/obfuscation-checks/Invoke-ObfuscationChecks.ps1 `
     -Scenario MissingStage

powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/obfuscation-checks/Invoke-ObfuscationChecks.ps1 `
     -Scenario MissingMap

powershell -NoProfile -ExecutionPolicy Bypass `
     -File tests/obfuscation-checks/Invoke-ObfuscationChecks.ps1 `
     -Scenario Identical
```

The harness exits 0 when the scenario's expectation matches the actual
build outcome; 1 otherwise (after printing the diagnostic to STDERR).

## What the harness does **NOT** cover (Unit 2)

The publish-side smoke (load + JSON round-trip + SQLite EF save/query
against the obfuscated artifact) is **out of scope** for Unit 1. It lives in
the deferred Unit 2 slice and will be wired into the Service publish path in
a subsequent apply pass.
