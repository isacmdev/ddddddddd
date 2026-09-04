# Backend Integration Acceptance Runbook

This runbook is for the future `backend-integration-acceptance` change. Running the local harness proves client conformance only and always emits `ExternalVerified=false`.

## 1. Preconditions

Use an isolated staging tenant with two disposable definitive device identities, short-lived JWTs, revocation/reset control, approved RLS policies/indexes, TLS host plus current/next SPKI pins, and WNS fan-out credentials. Never use `service_role` in a client process or receipt.

The acceptance owner MUST provide a signing identity whose public key and certificate chain are reviewable. Secrets MUST enter only the child process environment or an approved ephemeral secret provider; never command arguments, files in the workspace, logs, screenshots, or receipts.

## 2. Verify the immutable local boundary

From the repository root:

```powershell
dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --filter "FullyQualifiedName~BackendIdentityAcceptancePackageTests|FullyQualifiedName~BackendIdentityContractV1Tests" --no-restore --verbosity normal
Get-FileHash tests/ControlParental.Service.Tests/BackendIdentityContractV1Harness.cs -Algorithm SHA256
Get-FileHash openspec/changes/backend-identity-contract/evidence/local-receipt.schema.v1.json -Algorithm SHA256
```

Both hashes MUST equal the manifest. Validate the emitted local receipt against `local-receipt.schema.v1.json`; validation MUST fail if `ExternalVerified` is changed to `true`.

## 3. Inject external configuration

Create a clean process environment containing only the approved staging base URL, issuer, audience, TLS host/pins, two disposable device fixture references, revocation control reference, and WNS test target reference. Resolve secret values inside the future runner. Print only variable names and presence/absence; abort if any value is echoed or if a `service_role` credential is present.

## 4. Execute bounded probes

Run once, with a 180-second suite timeout and per-request timeouts at most 10 seconds:

1. Pair both devices and verify exact `device_id` JWT binding.
2. Read each own row, attempt both cross-device rows, and require hostile reads/writes to fail.
3. Revoke device A and require its refresh and authenticated REST/WNS registration to fail while device B remains valid.
4. Verify TLS host, chain, online revocation, current pin, next-pin overlap, and mismatch denial without fallback.
5. Register a disposable WNS channel through authenticated Service IPC and verify one fan-out delivery.

Retries MUST remain owned by `BackendClient`, use the same idempotency key, and stay within three attempts. Do not poll or stack runner retries.

## 5. Produce external receipts

The future change MUST define a separate external-receipt schema. Each receipt MUST contain probe ID, UTC start/end, contract version, environment identifier hash, client artifact hashes, redacted typed result, signer key ID, signature, and reproducibility command hash. It MUST contain no URL query, channel URI, JWT, key, pairing code, body, pin, parent/device raw identifier, or exception text.

Only the future verifier may set `ExternalVerified=true`, and only after validating all signatures, signer trust, artifact hashes, environment binding, and complete probe coverage. A local receipt is structurally ineligible for promotion.

## 6. Cleanup

In `finally`: revoke both fixtures, delete WNS registration, reset staging rows, stop child processes, clear process-scoped environment variables, remove temporary receipts containing unsigned data, and verify no secrets or channel URIs remain in logs or the workspace. Preserve only signed, schema-valid, redacted receipts.
