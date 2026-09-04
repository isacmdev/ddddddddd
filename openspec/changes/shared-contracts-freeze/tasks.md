# Tasks: Shared Contracts Freeze

- [x] Record authority, scope, compatibility, privacy, and `child_first_name` decision.
- [x] Specify versioned envelope and source-generated JSON requirements.
- [x] Specify SetProtectedAccount request/ACK, durable idempotency, and RuntimeActivationState.
- [x] Specify CreateTimeRequest, atomic local outbox, all six states, retry, deduplication, and grant application.
- [x] Specify JWT validation, exact device binding, refresh/rotation/revocation, and RLS two-device isolation.
- [x] Specify complete monotonic policy snapshots, equal-version conflict quarantine, and out-of-order handling.
- [x] Specify IntegrityEvidence/Verdict and backend-only authority.
- [x] Specify WNS/Realtime signal-only behavior and lifecycle convergence.
- [x] Fix numeric wire limits, required/optional members, extension semantics, canonical JSON fixtures, and ownership handoff.
- [x] Contract Pre-lane (`device-sync-identity`) implements and freezes `src/ControlParental.Domain/**`, source-generated JSON metadata, fixtures, and CT-01..CT-12. It must integrate into the canonical base before any parallel lane is created. Lane A is runtime, B is App.UI/status, C is SessionAgent/overlay; D preserves root `Build-MSIX.ps1`.
- [ ] Backend acceptance adds live two-device JWT/RLS, atomic approval, deduplication, and WNS signal-only tests; these remain external and cannot be claimed by this change.

## Acceptance test matrix

| ID | Executable command / fixture | Expected |
|---|---|---|
| CT-01 | `dotnet test --filter FullyQualifiedName~CT01` | Every v1 contract round-trips with stable snake_case and source-generated metadata |
| CT-02 | `dotnet test --filter FullyQualifiedName~CT02` + every `fixtures/invalid-*.json` (`invalid-major`, `invalid-enum`, `invalid-missing-required`, `invalid-unknown-required-member`, `invalid-envelope-oversize`, `invalid-reason-too-long`, `invalid-depth`, `invalid-array-too-long`, `invalid-string-too-long`, `invalid-extensions`, `invalid-timestamp-future`, `invalid-minutes-too-high`, `invalid-hint-member`, `invalid-hint-1025`) | Each negative has exactly one declared cause and rejects without side effect |
| CT-03 | `dotnet test --filter FullyQualifiedName~CT03` | Account ACK then restart; one durable selection; same retry result; conflict rejected |
| CT-04 | `dotnet test --filter FullyQualifiedName~CT04` | `degraded`/`failed`; onboarding incomplete; cached enforcement remains |
| CT-05 | `dotnet test --filter FullyQualifiedName~CT05` | One `(device_generation, request_id)` key; truthful state; no inferred approval |
| CT-06 | `dotnet test --filter FullyQualifiedName~CT06` | Only grant evidence reaches `applied`; denial creates no grant |
| CT-07 | `dotnet test --filter FullyQualifiedName~CT07` | Invalid/stale generations denied; one current refresh; no secret disclosure |
| CT-08 | `dotnet test --filter FullyQualifiedName~CT08` | RLS/server authorization denies all cross-device reads/writes |
| CT-09 | `dotnet test --filter FullyQualifiedName~CT09` | v9 retained; equal-version conflict quarantined; no downgrade |
| CT-10 | `dotnet test --filter FullyQualifiedName~CT10` | Only current backend verdict changes issue; unknown is non-degrading |
| CT-11 | `dotnet test --filter FullyQualifiedName~CT11` | One bounded sync intent; authenticated pull only; payload never applied |
| CT-12 | `dotnet test --filter FullyQualifiedName~CT12` | No token, key, pairing code, URI, child name, reason, or raw identifier |

No test in this change is evidence of live backend, WNS fan-out, staging, or Windows native matrix acceptance.

The implementation owner MUST publish test names matching CT-01..CT-12 and run the commands above from the repository root. Boundary cases MUST include exactly-at-limit and limit-plus-one values; fixture validation MUST include every file under `fixtures/`. The manifest's CT-02 entry explicitly enumerates every negative fixture (the `invalid-*.json` glob remains an exhaustiveness guard), and CT-11 explicitly includes the 1,024-byte valid and 1,025-byte invalid hint boundaries.

Fixture mapping and the fixed evaluation clock are recorded in `fixtures/fixture-manifest.json`. The manifest covers valid account/activation/time/outbox/policy/integrity/WNS/Realtime families and one-cause negatives for major, enum, missing, unknown member, envelope oversize, depth, array, string, extension key, timestamps, minutes, reason (257 UTF-8 bytes), and hint validation. The 256-byte reason and 180-minute inclusive boundaries are in `valid-boundary-minutes-reason.json`.
