Unit: A-hardening-verdict
Scope: tasks 1.1, 1.2, 1.3 only
Paths:
src/ControlParental.Service/AclHardener.cs
src/ControlParental.Service/OnboardingStateService.cs
src/ControlParental.Service/Program.cs
src/ControlParental.Service/RuntimeSecurityVerdict.cs
src/ControlParental.Service/ServiceHealthMonitor.cs
tests/ControlParental.Service.Tests/AclHardenerTests.cs
tests/ControlParental.Service.Tests/OnboardingStateServiceTests.cs
tests/ControlParental.Service.Tests/PrivilegeInspectorTests.cs
tests/ControlParental.Service.Tests/ServiceHealthMonitorTests.cs
openspec/changes/windows-runtime-foundations/tasks.md
Commands:
dotnet test tests/ControlParental.Service.Tests --filter "FullyQualifiedName~Hardening" --verbosity normal = PASS, 11/11
dotnet test tests/ControlParental.Service.Tests --no-build --filter "FullyQualifiedName~Hardening" --logger "console;verbosity=minimal" = PASS, 11/11
dotnet test tests/ControlParental.Service.Tests --no-build --filter "FullyQualifiedName~Hardening" --collect:"XPlat Code Coverage" = PASS, 11/11; focused report: RuntimeSecurityVerdict 100% line/100% branch; AclHardener 82.73% file/90% branch, all 7 changed SetAccessRule lines hit
dotnet test tests/ControlParental.Service.Tests --no-build --collect:"XPlat Code Coverage" = PASS, 775/775; line 47.38%, branch 44.66% repository/service aggregate
Windows ACL disposable harness = PASS; folder rules equivalent after two applications; registry ACL repeated; cleanup complete
Broader service verification = 774/775 passed; one pre-existing UsageAccumulator warning-duplication failure
Delta:
Unit A authored estimate: 163 additions+deletions, below 400-line limit; 23 pre-existing changes preserved
Coverage:
Changed-line scope is above 80% for the new verdict evaluator, ACL replacement lines, onboarding guard, and health verdict surface. Aggregate file rates remain lower because they include legacy production lines outside this unit. Branch coverage is reported above.
Harness:
Elevated Windows harness executed against disposable temp folder and HKCU registry key; no production path or service registry was touched.
Rollback boundary:
Revert only the Unit A production/test paths above plus this SDD evidence; leave the 23 pre-existing files and all B-D seams untouched.
Pending:
Task 1.4 runtime harness artifact is not marked complete; Work Units B, C, and D remain pending.
