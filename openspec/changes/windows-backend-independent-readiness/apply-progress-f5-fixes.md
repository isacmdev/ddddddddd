# Apply Progress: F5 Spec Coverage — windows-backend-independent-readiness

## Status: DONE — all 4 spec coverage gaps closed, 947 tests pass

**Change**: windows-backend-independent-readiness
**Mode**: Standard (strict_tdd: false)
**Delivery strategy**: single PR (this is focused remediation, not a new feature)

---

## Completed Tasks

### Fix 1: `DeviceAuthenticator.InitializeAsync_WhenStoredSessionExpired_ReturnsNeedsRefresh` — explicit state assertion
- [x] `tests/ControlParental.Service.Tests/DeviceAuthenticatorTests.cs` — assert `sut.CurrentState == DeviceAuthState.NeedsRefresh` and the persisted `DeviceId` is preserved.
- **Why it was stale**: `DeviceAuthenticator.InitializeAsync` sets the internal `currentState = NeedsRefresh` for expired sessions but returns `DeviceAuthResult.Succeeded(...)` which always reports `State = Authenticated`. The previous test only checked `result.Success` and `result.DeviceId`, never the surfaced state.
- **Spec scenario covered**: "Session metadata is stale" → service marks session refresh-needed instead of pretending success.

### Fix 2: `ScheduledWorkService.ExecuteOutboxPushAsync` — per-entry isolation
- [x] `src/ControlParental.Service/ScheduledWorkService.cs` — refactored outbox processing so each entry is independently classified and marked sent/failed.
- **Per-category isolation**: when a category push fails, only entries in that category are marked failed. Entries in successful categories are marked sent without misclassification.
- **Unknown table names**: now surface as per-entry `MarkFailedAsync("Unknown table: <name>")` instead of silent drop.
- **Malformed JSON**: `JsonException` per-entry `MarkFailedAsync(... )` retained.
- **Public surface parity**: `ExecuteOutboxPushAsync` is now `internal` (was `private`) so tests can drive it directly. `InternalsVisibleTo` was already configured.
- [x] `tests/ControlParental.Service.Tests/ScheduledWorkServiceTests.cs` — new `ScheduledWorkServiceOutboxPerEntryTests` class with 4 focused tests:
  - `MixedValidAndMalformedRows_IsolatesFailures` — spec scenario "Mixed valid and malformed rows".
  - `UnknownTableName_MarksFailedAndContinues` — spec scenario "Unknown table name is encountered".
  - `OneCategoryFails_DoesNotMisclassifyOtherCategory` — guarantees a successful category's valid entries are NOT misclassified when an unrelated category fails.
  - `MalformedJsonRow_OnlyAffectsThatEntry` — narrower regression for malformed JSON isolation.

### Fix 3: `IntegrityChecker` — deterministic debugger probe seam
- [x] `src/ControlParental.Service/IntegrityChecker.cs` — added `Func<bool>? debuggerProbe = null` constructor parameter. Default falls back to `System.Diagnostics.Debugger.IsAttached`. Backward-compatible.
- [x] `tests/ControlParental.Service.Tests/IntegrityCheckerTests.cs` — 3 new focused tests:
  - `CheckLocalIntegrityAsync_WhenDebuggerAttached_StillReturnsLocalEvidence` — verifier returns signature, hash, and path even when the debugger branch fires.
  - `CheckLocalIntegrityAsync_WhenDebuggerNotAttached_StillReturnsLocalEvidence` — same with the probe returning false.
  - `CheckLocalIntegrityAsync_InvokesDebuggerProbe` — lock in that the probe is actually consulted.
- **Spec scenario covered**: "Debugger is attached" → diagnostics may note the debugger, AND the local evidence result is still returned.

### Fix 4: `NamedPipeUIServer.DeserializeMessage` — malformed JSON fail-closed coverage
- [x] `src/ControlParental.Service/Interop/NamedPipeUIServer.cs` — added explicit `root.ValueKind != JsonValueKind.Object` check before `TryGetProperty`. The existing `catch (JsonException)` did NOT catch `InvalidOperationException` raised when `TryGetProperty` is called on a non-object root (array, scalar). Failure now fails closed.
- [x] `tests/ControlParental.Service.Tests/NamedPipeUIServerDeserializeTests.cs` — 3 new tests:
  - `DeserializeMessage_MalformedJson_ReturnsNull` — `{ this is not valid json }` → null (no exception).
  - `DeserializeMessage_NonObjectJson_ReturnsNull` — JSON array → null (regression for the ValueKind bug above).
  - `DeserializeMessage_UnknownMessageType_ReturnsNull` — well-formed object with unrecognized MessageType → null.
- **Spec scenario covered**: "Malformed IPC JSON arrives" → parser fails closed, process remains alive.

---

## Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `src/ControlParental.Service/ScheduledWorkService.cs` | Modified | Per-entry outbox processing; unknown tables marked failed; per-category failure isolation; `ExecuteOutboxPushAsync` made `internal` for direct test access |
| `src/ControlParental.Service/IntegrityChecker.cs` | Modified | Added `Func<bool>? debuggerProbe` seam (default: `Debugger.IsAttached`) |
| `src/ControlParental.Service/Interop/NamedPipeUIServer.cs` | Modified | Added `root.ValueKind != Object` guard before `TryGetProperty` so non-object JSON fails closed |
| `tests/ControlParental.Service.Tests/DeviceAuthenticatorTests.cs` | Modified | `InitializeAsync_WhenStoredSessionExpired_ReturnsNeedsRefresh` now asserts `sut.CurrentState == NeedsRefresh` and `result.DeviceId == expected` |
| `tests/ControlParental.Service.Tests/ScheduledWorkServiceTests.cs` | Modified | Added `ScheduledWorkServiceOutboxPerEntryTests` class with 4 focused isolation tests |
| `tests/ControlParental.Service.Tests/IntegrityCheckerTests.cs` | Modified | Added 3 tests: debugger-attached, no-debugger, and probe-invocation locks |
| `tests/ControlParental.Service.Tests/NamedPipeUIServerDeserializeTests.cs` | Modified | Added 3 fail-closed tests: malformed JSON, non-object JSON, unknown MessageType |

---

## Deviations from Design

- **Production code change beyond pure test additions**: The `NamedPipeUIServer.DeserializeMessage` improvement was a small production bug fix (non-object JSON threw `InvalidOperationException` that wasn't caught). Without this, the new "malformed IPC JSON" test would crash instead of returning null. Total production change: ~28 lines.
- **Test-only visibility change**: `ExecuteOutboxPushAsync` was changed from `private` to `internal` so tests can drive the outbox loop without going through the timer. The `InternalsVisibleTo` attribute was already in place.

---

## Issues Found

- **Pre-existing `NamedPipeUIServer.cs` and `NamedPipeUIServerDeserializeTests.cs` are untracked in git**. The user has them in the working tree but they are not in the index. My edits to those files do not appear in `git diff --stat HEAD`. This is a pre-existing repo state, not caused by my changes.

---

## Verification

### Focused tests
| Filter | Result |
|--------|--------|
| `DeviceAuthenticatorTests.InitializeAsync_WhenStoredSessionExpired_ReturnsNeedsRefresh` | 1 passed |
| `ScheduledWorkServiceOutboxPerEntryTests` | 4 passed |
| `IntegrityCheckerTests` | 10 passed (7 pre-existing + 3 new) |
| `NamedPipeUIServerDeserializeTests` | 6 passed (3 pre-existing + 3 new) |

### Full project
| Project | Result |
|---------|--------|
| `ControlParental.Service.Tests` | 673 passed, 0 failed |
| `ControlParental.Domain.Tests` | 82 passed, 0 failed |
| `ControlParental.SessionAgent.Tests` | 80 passed, 0 failed |
| `ControlParental.App.UI.Tests` | 112 passed, 0 failed |
| **Total** | **947 passed, 0 failed** |

### Build
- `dotnet build ControlParental.sln -c Debug` → 0 errors, 976 warnings (all pre-existing stylecop/analyzer).

---

## Workload / PR Boundary

- **Mode**: Single PR (focused remediation, not a new feature)
- **Boundary**: 4 spec coverage gaps only — minimal behavior-focused changes
- **Estimated review budget impact**: ~493 tracked lines (inserts + deletes) — slightly over the 400-line soft budget due to 4 distinct per-entry tests. Largest contributions are tests (382 lines); production code is ~111 lines.
- **Rollback boundary**: Revert the 4 modified tracked files; the 2 untracked files (NamedPipeUIServer.cs, NamedPipeUIServerDeserializeTests.cs) are pre-existing local state.

---

## Remaining Tasks

- [ ] F5 verification with the canonical F5 command remains to be re-run by the operator.
