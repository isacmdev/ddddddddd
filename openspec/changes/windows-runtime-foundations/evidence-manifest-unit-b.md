Unit: B-multi-session-lifecycle
Tasks: 2.1, 2.2, 2.3
Paths: src/ControlParental.Service/Program.cs; src/ControlParental.Service/SessionWatcher.cs; src/ControlParental.Service/AgentLauncher.cs; tests/ControlParental.Service.Tests/SessionManagerLifecycleTests.cs
Focused: `dotnet test tests/ControlParental.Service.Tests --no-restore --filter "FullyQualifiedName~SessionManagerLifecycleTests|FullyQualifiedName~AgentLauncherLaunchSeamTests"` = PASS 11/11; Session filter = PASS 33/33.
Broad: `dotnet test tests/ControlParental.Service.Tests --no-build --verbosity minimal` = 781/782 passed; one pre-existing UsageAccumulator warning-duplication failure; duplicate xUnit ID skipped.
Coverage: exact production diff/intersection against `ca5b4c1df43ef826df25bf859ac6d04bd1430677`: AgentLauncher 12/23 = 52.17%; SessionWatcher 38/43 = 88.37%; Program/SessionManager 113/132 = 85.61%; combined 163/198 = 82.32%. Approximate changed-line branch coverage is 36/60 = 60.00%. Cobertura emits duplicate generated async state-machine classes, preventing an exact branch union; this is the closest available metric and is not invented as exact.
Harness: UNAVAILABLE — no two real child WTS sessions or elevation; no runtime PASS claimed.
Historical native count: 427 lines; maintainer-approved ceiling: 500 lines.
Rollback: revert the four Unit B paths above and this manifest; preserve Unit A evidence.
Status: 2.1, 2.2, and 2.3 complete; Unit B functionally complete. Runtime WTS evidence is UNAVAILABLE/deferred and never PASS.
Test-only attempt: 174 authored test lines, under the 250-line allowance; native historical count 427 and approved ceiling 500.
Exact uncovered instrumentable lines: AgentLauncher 129,144,153-159,351-354,376; SessionWatcher 42-44,62-63,74-75,120,129-130,143; Program/SessionManager 557-562,564,636,638,641-642,648,663,669-679,689,691-698,727-734,736,738-739,767-768,776,786-789,852,858-863,865,899-910,945-948.
Pending: C and D.
