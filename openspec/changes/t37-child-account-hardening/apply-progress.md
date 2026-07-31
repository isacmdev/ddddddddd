# Apply Progress: T37 — Child Account Standard + Hardening

## Status: ✅ DONE

**Change**: t37-child-account-hardening
**Mode**: Standard (strict_tdd: false)
**Delivery strategy**: ask-on-risk

---

## Completed Tasks

### Item 1: IPrivilegeInspector — Privilege Detection
- [x] `PrivilegeInspector.cs`: uses `WindowsIdentity.GetCurrent()` + `WindowsPrincipal.IsInRole(Administrator)` to detect privilege level
- [x] Returns `PrivilegeLevel.Standard`, `PrivilegeLevel.Administrator`, or `PrivilegeLevel.Unknown`
- [x] `IsChildStandardAsync()` convenience method: returns `true` only when level is `Standard`
- [x] Exposes result to T12 (`EnforcementLevelMonitor` consumes `IsChildStandardAsync`)
- [x] **9 unit tests**: integration against live Windows API — returns valid enum values, completes within reasonable time, cancellation support, consistent results

### Item 2: AccountManager — Standard Account Creation/Conversion
- [x] `AccountManager.cs`: `CreateStandardAccountAsync` + `ConvertToStandardAsync` via `net.exe`
- [x] `GetAccountsAsync` via WMI `Win32_UserAccount` enumeration
- [x] `ChildAccountStore` persists child account name
- [x] `AccountCreationResult` factory: `Succeeded`, `NeedsAdminElevation`, `Failed`
- [x] Validation: empty username guard, password minimum length (4 chars)
- [x] **21 unit tests**: constructor guards, account store delegation, username validation, password validation, result factory methods

### Item 3: AclHardener — ACL Hardening
- [x] `AclHardener.cs`: `Deny Write/Delete` ACLs on 4 targets:
  - Agent folder (Program Files) — directory-level `Delete` + `ChangePermissions` deny
  - Data folder (ProgramData) — same
  - Registry key — `WriteKey` + `Delete` deny
  - Service binary — `Delete` + `ChangePermissions` deny
- [x] `HardenAllAsync` applies all measures atomically
- [x] **3 existing unit tests** passing

### Item 4: PPL (Best-Effort, Optional)
- [x] `IProtectedProcessReporter` + `ProtectedProcessReporter` implemented
- [x] Checks if binary is signed with PPL-eligible certificate (WinVerifyTrust)
- [x] Documents how to enable PPL: sign binary, set `SERVICE_PPL_FLAG`
- [x] Registered in DI via `Program.cs`
- [x] Reports PPL status to T12 monitoring

---

## Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `tests/ControlParental.Service.Tests/PrivilegeInspectorTests.cs` | **Created** | 9 integration tests for `PrivilegeInspector` against live Windows API |
| `tests/ControlParental.Service.Tests/AccountManagerTests.cs` | **Created** | 21 unit tests for `AccountManager` with mocked dependencies |

---

## Known Design Issue (not a blocker)

**`AccountManager.IsAccountStandardAsync` uses `WindowsIdentity` directly** — it does NOT call `IPrivilegeInspector`. The mock of `IPrivilegeInspector` in tests has no effect on this method because the real implementation bypasses the interface. This is an architectural inconsistency: `PrivilegeInspector` already knows how to check privilege via `WindowsIdentity`, yet `AccountManager` duplicates that logic.

**Impact**: Low. The duplication means the `IPrivilegeInspector` abstraction is not fully utilized by `AccountManager`. Fixing it would require refactoring `AccountManager.IsAccountStandardAsync` to use `IPrivilegeInspector.GetPrivilegeLevelAsync` instead of direct `WindowsIdentity` construction — but this is a design improvement, not a functional bug.

---

## Test Coverage Summary

| Component | Coverage | Tests |
|-----------|----------|-------|
| `PrivilegeInspector` | Integration (live Windows API) | 9 |
| `AccountManager` | Unit with mocks | 21 |
| `AclHardener` | Unit (pre-existing) | 3 |
| `PrivilegeLevel` enum | Unit | 6 (pre-existing in `Domain.Tests`) |
| `AccountCreationResult` | Unit | 3 (pre-existing in `Domain.Tests`) |
| **T37 Total** | | **42 tests** |

**Full test suite**: 516 Service ✅ · 80 SessionAgent ✅ · 8 App.UI ✅ (9 pre-existing `DispatcherTimer` failures unrelated to T37)

---

## Done Criteria Verification

| Backlog criterion | Status |
|---------------------|--------|
| ☑ Detección de privilegio (`IPrivilegeInspector`) | ✅ 9 integration tests pass |
| ☑ Guía/automatización a estándar (`AccountManager`) | ✅ 21 unit tests pass |
| ☑ ACLs de carpeta/DB/registro (`AclHardener`) | ✅ 3 existing tests pass |
| ☑ DoD-G | ✅ `dotnet build` 0 errors · `dotnet test` 516 Service ✅ |
