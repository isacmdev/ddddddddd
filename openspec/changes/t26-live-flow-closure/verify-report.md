# HISTORICAL / SUPERSEDED VERIFICATION REPORT

> This entire report records an older failing snapshot and is preserved without changing its original YAML or results. It is not current closure evidence. For the authoritative passing verification and final status, see `archive-report.md`.

```yaml
schema: verify-result/v1
evidence_revision: sha256:e01c4e8d17f2796f00376c21e34bb57a795c3bd3413d0d9b1dc523a482144b71
verdict: fail
blockers: 3
critical_findings: 3
requirements: 0/5
scenarios: 0/9
test_command: dotnet test tests/ControlParental.App.UI.Tests/ControlParental.App.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~Onboarding|FullyQualifiedName~ConsentFlow|FullyQualifiedName~DeadCodeRemoval|FullyQualifiedName~OnboardingLauncherThreat" --verbosity minimal; dotnet test tests/ControlParental.App.UI.Tests/ControlParental.App.UI.Tests.csproj --no-restore --verbosity minimal; dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --filter "FullyQualifiedName~Onboarding|FullyQualifiedName~UIMessageHandler" --verbosity minimal; dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --verbosity minimal
test_exit_code: 1
test_output_hash: sha256:dd37bff52b345d2dffe450bc442c132d3bb0b049a33629313e1784e470c7bb66
build_command: dotnet build ControlParental.sln --no-restore --verbosity minimal
build_exit_code: 1
build_output_hash: sha256:8e6cc9d62c028cf4ec4396679328d1e512a5751e144df6090138c4716369448a
```

## Verification Report

**Change**: `t26-live-flow-closure`  
**Version**: N/A (delta spec)  
**Mode**: Standard (`strict_tdd: false`)  
**Evidence basis**: current source snapshot and commands executed on 2026-07-28 after focused remediation

### Completeness

| Metric | Value |
|---|---:|
| Original tasks total | 15 |
| Focused-remediation tasks total | 2 |
| Tasks complete | 17 |
| Tasks incomplete | 0 |
| Requirements compliant with current runtime evidence | 0/5 |
| Scenarios with current passing runtime evidence | 0/9 |

All persisted task checkboxes are complete, so full verification was attempted. The completion claims do not match the current production source: `App.xaml.cs`, `MainWindow.xaml`, and `MainWindow.xaml.cs` still contain the legacy composition/generic shell and do not wire the new IPC route coordinator.

### Build & Tests Execution

**Build**: ❌ Failed

```text
dotnet build ControlParental.sln --no-restore --verbosity minimal
exit code: 1
output hash: sha256:8e6cc9d62c028cf4ec4396679328d1e512a5751e144df6090138c4716369448a

Current errors:
- CS0101: untracked src/ControlParental.App.UI/Program.cs duplicates WinAppSDK-generated Program.
- CS0246/CS0426/CS0122: untracked AgentLauncherLaunchSeamTests.cs targets absent/inaccessible AgentLauncher seams.
- MSB3073: App.UI XamlCompiler exits 1 after the duplicate Program error.

Projects that did build before the solution failed:
- ControlParental.Domain
- ControlParental.Service
- ControlParental.SessionAgent
- ControlParental.Domain.Tests
- ControlParental.SessionAgent.Tests
```

The malformed Domain project XML and broad `NoWarn` suppression reported previously are resolved. The refreshed solution build now reaches the two known untracked-file blockers.

**Tests**: ❌ No relevant suite reached runtime

| Command | Exit | Output hash | Result |
|---|---:|---|---|
| Focused App.UI T26 filter | 1 | `sha256:2d7911cb78762dff0137300ad08c7fcb5770442c4e9e90bac2234974c5e5732f` | App.UI compile blocked by duplicate `Program` |
| Full App.UI suite | 1 | `sha256:3401b5df023edb9cf4c11f2aba3bd7fdc489a812ddf21a1ebc0dbfc626a96fca` | App.UI compile blocked by duplicate `Program` |
| Focused Service onboarding/UIMessageHandler filter | 1 | `sha256:0855cd215b842cccb33a6a2402da14e9d0edef6e1f9a3643484f19b5e636e110` | Service.Tests compile blocked by stale AgentLauncher seam test |
| Full Service suite | 1 | `sha256:0855cd215b842cccb33a6a2402da14e9d0edef6e1f9a3643484f19b5e636e110` | Service.Tests compile blocked by stale AgentLauncher seam test |

The declared `test_output_hash` is SHA-256 over the exact four redirected outputs concatenated in command order. Prior 141/776 passing counts and the prior `--no-build` App.UI smoke are stale and are not credited.

**Coverage**: ➖ Not available. No relevant test assembly executed in this source snapshot.

**Runtime harness**: ➖ Not run. A current-source App.UI build does not exist, and a `--no-build` launch would only repeat stale-binary evidence.

### Format Verification

Required command:

```text
dotnet format ControlParental.sln --no-restore --verify-no-changes --verbosity minimal
exit code: 2
output hash: sha256:863d466dc3b0538ba0c159edbdc82392c4b071aee63bb56e6dfc931fdc6b9ea9
```

Decomposition:

| Category | Command | Exit | Output hash | Classification |
|---|---|---:|---|---|
| Whitespace | `dotnet format whitespace ControlParental.sln --no-restore --verify-no-changes --verbosity normal` | 0 | `sha256:366f182e8d5e6830fd820625ea562d78643e94a8db16aa8f16b45490b7fbb0e2` | Clean |
| Style | `dotnet format style ControlParental.sln --no-restore --verify-no-changes --verbosity normal` | 0 | `sha256:83de61ab0894268a0258abcebec69f549b46346f55038c6dc867cc8ddb6a5688` | Clean |
| Analyzers | `dotnet format analyzers ControlParental.sln --no-restore --verify-no-changes --verbosity normal` | 2 | `sha256:4abd1b490773d62e2f08911a73fa4aa05875b62a07c815cb8af9758396c2fa83` | Failing |

Analyzer output contains 6,391 warning lines across 263 source files. It is broad workspace debt, but it also includes current T26 implementation and test surfaces such as `App.xaml.cs`, `MainWindow.xaml.cs`, `OnboardingViewModel.cs`, `ConsentPage.xaml.cs`, `ServiceSetupPage.xaml.cs`, `TransparencyPage.xaml.cs`, `IIpcOnboardingStateService.cs`, `IpcOnboardingStateService.cs`, `OnboardingStateService.cs`, `UIMessageHandler.cs`, and T26 onboarding tests. Therefore the exact required command is not green, and the analyzer failure cannot be classified wholly as unrelated/pre-existing debt.

### Known Untracked File Classification

| File | Classification | Current evidence |
|---|---|---|
| `src/ControlParental.App.UI/Program.cs` | Pre-existing relative to `t26-live-flow-closure`; out of this change's live-flow scope, but an active build blocker | Engram history dates its custom WinUI packaging entry point to 2026-07-16, before this closure change began on 2026-07-28. Current content only performs WinUI process/bootstrap initialization and contains no live-flow routing. It duplicates generated `Program` and blocks App.UI. |
| `tests/ControlParental.Service.Tests/AgentLauncherLaunchSeamTests.cs` | Pre-existing and out of T26 live-flow scope, but an active Service.Tests blocker | Engram history records this AgentLauncher seam test on 2026-07-24, before this closure change. Current content concerns process launch/session recovery only and has no onboarding/live-flow dependency. It references stale seams and prevents Service.Tests compilation. |

These classifications do not rescue the verdict: independent current-source evidence also shows substantive T26 composition and routing defects.

### Spec Compliance Matrix

| Requirement | Scenario | Covering test/evidence | Current result |
|---|---|---|---|
| Live onboarding uses in-app consent/transparency only | In-app path is the only live consent path | `ConsentFlowTests.LiveShell_RoutesConsentAndTransparencyInApp`; production composition inspection | ❌ FAILING — tests cannot run; `App.xaml.cs` still constructs `ConsentDialog`, and `MainWindow` does not host consent/transparency pages |
| Live onboarding uses in-app consent/transparency only | Resume stays in-app | IPC ownership/resume tests; production composition inspection | ❌ FAILING — tests cannot run; launch still resolves local `IOnboardingStateStore` and legacy dialog composition |
| Live shell progress is honest/service-confirmed | Progress matches confirmed state | `OnboardingViewModelProgressTests`; monitor tests | ❌ FAILING — tests cannot run; updated VM exists but live `App` does not construct it with IPC/monitor dependencies |
| Live shell progress is honest/service-confirmed | Pending steps are not counted as done | progress/unknown-monitor tests | ❌ FAILING — tests cannot run and live composition is stale |
| T26 live routing covers canonical flow | Canonical sequence is reachable | route catalog and E2E state-machine tests | ❌ FAILING — tests cannot run; `MainWindow.xaml` remains a generic title/description/Execute/Next shell with no page host or route catalog use |
| T26 live routing covers canonical flow | Deep-link return requires re-verification | `OnboardingLauncherThreatTests`; ServiceSetup page | ❌ FAILING — tests cannot run; `ServiceSetupPage` is not hosted by the live window |
| Resume/state/funnel follow real flow | Restart resumes without duplicate completion | Service resumability/idempotency tests | ❌ FAILING — tests cannot run; Service-side implementation is present but not proven through the live route |
| Resume/state/funnel follow real flow | Funnel events describe live milestones only | VM funnel tests and Service persistence/dedupe tests | ❌ FAILING — tests cannot run; IPC-backed VM is not composed by the live app |
| Stale legacy consent path is inactive | Legacy path is unreachable in production flow | dead-code/live-source regression tests; source inspection | ❌ FAILING — `ConsentDialog.cs` still exists and `App.xaml.cs:83` constructs it |

**Compliance summary**: 0/9 scenarios compliant with current runtime evidence.

### Correctness (Static Evidence)

| Requirement | Status | Notes |
|---|---|---|
| In-app consent/transparency only | ❌ Not implemented in live composition | In-app pages exist, but the production app still constructs `ConsentDialog`; transparency is not exposed by `MainWindow`. |
| Honest service-confirmed progress | ⚠️ Partial | Fail-closed progress logic exists in `OnboardingViewModel`, but the live app constructs the obsolete local-store/dialog shape instead of IPC + monitor dependencies. |
| Canonical six-step routing | ❌ Not implemented in live shell | `OnboardingRouteCatalog` exists, but `MainWindow` never selects or hosts its destinations. |
| Service-owned resume/state | ⚠️ Partial | IPC and Service state implementations exist; production launch still resolves `IOnboardingStateStore`. |
| Live causal funnel behavior | ⚠️ Partial | VM and Service funnel code exists, but live composition does not wire that VM and no runtime test executed. |

### Coherence (Design)

| Decision | Followed? | Notes |
|---|---|---|
| MainWindow is navigation host with guarded callbacks | ❌ No | Current window is the old generic shell and has no routed page host or guarded completion callback. |
| Service snapshot is sole progress/resume authority | ❌ No in production composition | `App.xaml.cs` resolves `IOnboardingStateStore` and tries to pass it to the VM. |
| Funnel IPC acknowledgement fails closed | ⚠️ Static implementation only | The updated VM catches acknowledgement failures, but the live app does not compose it and tests did not run. |
| Remove production use of `ConsentDialog` | ❌ No | `ConsentDialog.cs` remains and `App.xaml.cs` constructs it. |
| Add acknowledged Service setup route | ⚠️ Partial | Page and IPC methods exist, but MainWindow never hosts the page. |
| DoD-G format/analyzer gate is green | ❌ No | Exact command exits 2; analyzer decomposition exits 2. |

### Issues Found

**CRITICAL**

1. The current production composition contradicts the completed tasks and design: `App.xaml.cs` constructs the legacy `ConsentDialog` and obsolete local-store VM shape, while `MainWindow` remains the generic shell and does not route the six in-app pages. This independently violates the consent-only, canonical-routing, resume, and live-flow requirements.
2. The solution build and all relevant App.UI/Service test commands exit 1. The two direct compiler blockers are pre-existing/out-of-scope untracked files, but no current T26 scenario has passing runtime evidence; final verification fails closed.
3. The required format command exits 2. Whitespace and style pass, but analyzers fail and report violations on current T26 files as well as broad workspace debt.

**WARNING**

1. The artifact backends disagree with current source: tasks/apply-progress claim legacy removal, routed MainWindow composition, and final passing suites, but the latest files do not contain that live wiring.
2. No current-source App.UI runtime smoke is possible. Prior `--no-build` evidence is intentionally excluded.
3. NuGet resolves newer versions than requested for Microsoft.WindowsAppSDK and supabase-csharp (NU1601).

**SUGGESTION**

1. After remediation, rerun this same build/test/format set from a clean current-source build; do not reuse old TRX files or binaries.

### Verdict

**FAIL**

The focused project-file remediation succeeded, but T26 cannot receive PASS or PASS WITH WARNINGS. Current production composition still uses the legacy consent/local-store shell instead of the designed live IPC route, all relevant runtime suites are blocked before execution, and the exact required format command remains nonzero.
