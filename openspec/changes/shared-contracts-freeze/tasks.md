# Tasks: Shared Contracts Freeze

- [x] Record authority, scope, compatibility, privacy, and `child_first_name` decision.
- [x] Specify versioned envelope and source-generated JSON requirements.
- [x] Specify SetProtectedAccount request/ACK, durable idempotency, and RuntimeActivationState.
- [x] Specify CreateTimeRequest, atomic local outbox, all six states, retry, deduplication, and grant application.
- [x] Specify JWT validation, exact device binding, refresh/rotation/revocation, and RLS two-device isolation.
- [x] Specify complete monotonic policy snapshots, equal-version conflict quarantine, and out-of-order handling.
- [x] Specify IntegrityEvidence/Verdict and backend-only authority.
- [x] Specify WNS/Realtime signal-only behavior and lifecycle convergence.
- [ ] Downstream implementation lanes add round-trip and negative serialization tests against this spec.
- [ ] Backend acceptance adds live two-device JWT/RLS, atomic approval, deduplication, and WNS signal-only tests; these remain external and cannot be claimed by this change.

## Acceptance test matrix

| ID | Test | Expected |
|---|---|---|
| CT-01 | Serialize/deserialize every v1 contract and envelope | Stable snake_case round-trip with source-generated metadata |
| CT-02 | Unknown major, enum, missing required field, oversized payload | Rejected without side effect; safe unknown/degraded outcome |
| CT-03 | Account ACK then Service restart; duplicate and conflicting operation IDs | One durable selection; same retry result; conflict rejected |
| CT-04 | Activation failure after account commit | `degraded`/`failed`; onboarding does not complete; enforcement remains cached |
| CT-05 | Offline request, timeout, duplicate retry, restart | One idempotency key; state remains truthful; no inferred approval |
| CT-06 | Approved request with delayed grant and denial | Only grant evidence reaches `applied`; denial creates no grant |
| CT-07 | JWT issuer/audience/expiry/signature/device mismatch and refresh race | Invalid/stale generations denied; one current refresh; no secret disclosure |
| CT-08 | Two device sessions access each other's rows | RLS/server authorization denies all cross-device reads/writes |
| CT-09 | Policy v9 then delayed v8; equal-version altered hash | v9 retained; conflict quarantined; no downgrade |
| CT-10 | Integrity trust/revoked/unknown, local-only evidence, timeout | Only current backend verdict changes issue; unknown is non-degrading |
| CT-11 | Duplicate/out-of-order WNS and Realtime hints offline/online | One bounded sync intent; authenticated pull only; payload never applied |
| CT-12 | Log/receipt/IPC redaction scan | No token, key, pairing code, URI, child name, reason, or raw identifier |

No test in this change is evidence of live backend, WNS fan-out, staging, or Windows native matrix acceptance.
