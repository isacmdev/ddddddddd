# Apply Progress: A-hardening-verdict

## Status

- [x] 1.1 RED
- [x] 1.2 GREEN
- [x] 1.3 REFACTOR
- [x] 1.4 RUNTIME
- [x] Unit A complete
- [ ] Work Units B, C, and D pending

## Implementation

Added the service-owned `RuntimeSecurityVerdict` interpretation for Standard, Administrator, Unknown, and ACL failure outcomes. `ServiceHealthMonitor` now publishes degraded health for Unknown/ACL failure while enforcement remains active. `OnboardingStateService` blocks healthy progression at the service step when the verdict is degraded. `AclHardener` uses replacement semantics for equivalent rules, and `Program` wires startup privilege plus ACL results into the monitor.

## Evidence

- Focused: `dotnet test tests/ControlParental.Service.Tests --no-build --filter "FullyQualifiedName~Hardening"` — PASS, 11/11.
- Coverage: focused XPlat report: 11/11, `RuntimeSecurityVerdict` 100% line/100% branch, `AclHardener` 82.73% file/90% branch with all seven changed `SetAccessRule` lines hit; broad service XPlat report: 775/775, line 47.38%, branch 44.66% aggregate. Changed-line scope for the new/modified Unit A behavior exceeds 80%; aggregate legacy-file rates are not the Unit A gate.
- Harness: PASS. Elevated disposable Windows harness applied ACL twice to a temporary folder and repeated an HKCU registry ACL; equivalent rules and cleanup were verified. No production path or real service registry was touched.
- Broader verification: 774/775 passed; the single failure is the pre-existing `UsageAccumulatorTests.SimulateTick_DoesNotDuplicateWarning_AfterThresholdCrossed` failure (two `ShowWarning` messages).
- Delta: 163 authored additions+deletions estimated for Unit A, below the 400-line limit; the 23 pre-existing changes remain preserved.
- Evidence manifest: `evidence-manifest.md`; evidence revision: `sha256:38ff772b6c1375d781143ff18f72a2fca1bd0dcbd5a6099b08782b7e18adfb80`; native attempt: COMPLETE.

## Rollback Boundary

Revert only the Unit A production and test paths listed in `evidence-manifest.md`, plus the SDD progress artifacts. This does not revert the 23 pre-existing working-tree changes and does not touch B, C, or D seams.

## Pending

Task 1.4 is complete from the persisted elevated harness evidence. Work Units B, C, and D remain pending.
