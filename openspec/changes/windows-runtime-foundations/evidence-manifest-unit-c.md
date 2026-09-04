# Evidence Manifest — Work Unit C1

## Scope

- `src/ControlParental.Domain/IpcFrameCodec.cs`
- `tests/ControlParental.Domain.Tests/IpcFrameCodecTests.cs`
- C2 native transport paths are restored to Unit B commit `736d56907d62135e325b22c8f2aa43fc9169408b` and remain pending.

## Native token

- `sha256:507d746b56c92c34c4ed75a63c146decc493cb86934f461531af330ced30557c`

## Evidence

- RED: codec tests cover fragmented input, coalesced frames, malformed JSON, and oversized length prefixes; the tests target the pure Domain project.
- GREEN: `dotnet test tests/ControlParental.Domain.Tests/ControlParental.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~IpcFrameCodecTests --collect:"XPlat Code Coverage"` — PASS, 2/2.
- GREEN/build: `dotnet build ControlParental.sln --no-restore --verbosity minimal` — PASS, 0 errors (warnings pre-existing).
- Coverage: exact C1 Cobertura intersection is 33/39 changed instrumentable lines (84.62%), with 8/12 branches (66.67%). The `IpcFrameCodec` class is 81.81% line/50% branch and `Decoder` is 85.71% line/70% branch. The >80% changed-line gate passes; branch coverage is reported rather than gated.
- Runtime harness: `reused` managed Domain test harness; native C2 ACL/authenticated harness is N/A/deferred and is not part of this unit.
- Authenticode, session handshake, PID/SID validation, reconnect, per-connection transport, and raw-stream integration are C2 concerns and are not retained in this C1 state.

## Status

- C1 RED: complete.
- C1 GREEN: complete; focused tests and full build pass.
- C1 REFACTOR/evidence: complete; exact changed-line coverage exceeds 80% and branch coverage is reported.
- C2: fully pending after rollback to Unit B.
- C1: complete and verified. Unit C overall remains open because C2 is pending.

## Rollback Boundary

Revert only `IpcFrameCodec.cs`, `IpcFrameCodecTests.cs`, and the C1 evidence/progress entries. Unit A/B implementation and evidence remain preserved. C2 paths are already identical to Unit B commit.
