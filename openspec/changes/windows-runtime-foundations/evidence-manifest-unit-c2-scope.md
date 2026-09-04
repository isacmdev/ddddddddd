# C2 Scope Classification Manifest (Task 4.1)

## Decision

Task 4.1 has an exact reconciliation for the immutable C2 candidate snapshot: **481 managed-deterministic lines + 156 native-boundary lines = 637 total changed production additions**. This manifest classifies behavior by path and line range; it does not exclude whole files.

## Candidate and baseline identity

| Item | Identity |
|---|---|
| Verified baseline commit | `90a5a2a44299d99b67aff18da2a9182bc167fc75` |
| Verified baseline tree | `c33bd1e53d46abbb95d33f345e6e135790dfeda3` |
| Candidate full-tree snapshot | `sha256:04dea78271d9dd88db51f13dbb823cb6ce9b1a5ae0897a711dfdd5a5d13ce5c5` |
| Candidate C2 production-scope snapshot | `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c` |

The candidate is the immutable read-only snapshot of the current working tree before classification. `HEAD` remained the verified baseline and no source/test/build/coverage command was run. Each snapshot hash is SHA-256 over sorted repository-relative paths, a NUL separator, file bytes, and a NUL separator; the full snapshot uses `git ls-files --cached --others --exclude-standard`, while the scope snapshot uses the ten C2 production paths.

## Method and invariants

1. Compare the verified baseline to the candidate using the documented C2 scope. New production files absent from the baseline contribute every current source line; modified files contribute only current-side additions from the zero-context diff.
2. Reconcile every changed production addition exactly once by normalized repository path and current source line. Missing Cobertura mapping, async generation, private visibility, or low coverage never creates a native exclusion.
3. `managed-deterministic` covers protocol decisions, state transitions, validation orchestration, ownership, serialization, reconnect/retry/deadline policy, and adapter behavior executable through managed seams.
4. `native-boundary` covers direct P/Invoke/marshalling, OS PID/SID/session/process-path retrieval, WinTrust/certificate-chain system evaluation, and concrete named-pipe kernel creation/identity/ACL behavior.
5. Mixed files are split into managed and native ranges. This is a classification boundary, not a coverage result.

## Exact changed production additions

### Managed-deterministic ranges

| Path | Current line ranges | Count |
|---|---|---:|
| `src/ControlParental.Domain/IpcHandshake.cs` | `1-55` | 55 |
| `src/ControlParental.Domain/IpcPhaseTrace.cs` | `1-41` | 41 |
| `src/ControlParental.Service/AgentLauncher.cs` | `27`, `87`, `104` | 3 |
| `src/ControlParental.Service/Interop/AuthenticodeSigner.cs` | `1-26`, `31-45`, `51-69`, `79-89`, `95` | 72 |
| `src/ControlParental.Service/Interop/NamedPipeServer.cs` | `21`, `27`, `29-31`, `44-57`, `62`, `64-66`, `89`, `92-95`, `107-108`, `189`, `194`, `198-203`, `211`, `214-217`, `222`, `226-228`, `239`, `241`, `243-245`, `247`, `252-255`, `285-288`, `291`, `294`, `298`, `308`, `310-311`, `327`, `331`, `338-341`, `343-344`, `347-349`, `352`, `354`, `359-373`, `375`, `377`, `381-402`, `442-443`, `447-450`, `454-466` | 138 |
| `src/ControlParental.Service/Interop/WinTrust.cs` | `72` | 1 |
| `src/ControlParental.SessionAgent/Interop/AuthenticodeSigner.cs` | `1-28`, `33-47`, `52-64`, `76-78`, `81-82` | 61 |
| `src/ControlParental.SessionAgent/Interop/NamedPipeClient.cs` | `18`, `21-28`, `36-45`, `49-52`, `79-81`, `83`, `85`, `123-124`, `130-131`, `144`, `146-168`, `178`, `184-185`, `190-193`, `195-196`, `199-229`, `267`, `269`, `273-274`, `277-285`, `296` | 110 |
| **Managed subtotal** |  | **481** |

### Native-boundary ranges and reasons

| Path | Current line ranges | Concrete design-rule reason | Count |
|---|---|---|---:|
| `src/ControlParental.Service/IntegrityChecker.cs` | `55` | Constructs the WinTrust-backed certificate-chain evaluator; the system trust result cannot be faithfully simulated. | 1 |
| `src/ControlParental.Service/Interop/AuthenticodeSigner.cs` | `27-30`, `46-50`, `70-78`, `90-94` | Invokes WinTrust/certificate extraction or opens a process and retrieves its OS image path/session through native APIs and marshalling. | 23 |
| `src/ControlParental.Service/Interop/NamedPipeServer.cs` | `22-23`, `100-106`, `336`, `342`, `345-346`, `357-358`, `451-453`, `467-478` | Uses kernel pipe instance/ACL creation, kernel client PID lookup, impersonated SID retrieval, process/WinTrust identity, direct P/Invoke, or the concrete named-pipe adapter. | 30 |
| `src/ControlParental.Service/Interop/WinTrust.cs` | `16`, `49-52`, `71`, `99-102`, `107`, `112` | Defines native WinTrust ABI/GUID/marshalling and invokes system certificate-chain evaluation. | 12 |
| `src/ControlParental.Service/ProtectedProcessReporter.cs` | `45-47` | Selects and constructs the native WinTrust verification path. | 3 |
| `src/ControlParental.SessionAgent/Interop/AuthenticodeSigner.cs` | `29-32`, `48-51`, `65-75`, `79-80`, `83-129` | Performs WinTrust/certificate-chain evaluation, OS process handle/path/session retrieval, direct P/Invoke, and native structure marshalling. | 68 |
| `src/ControlParental.SessionAgent/Interop/NamedPipeClient.cs` | `186-189`, `194`, `197-198`, `275-276`, `286-295` | Retrieves the kernel server PID, opens native process identity, evaluates signer identity, declares the native PID API, or wraps the concrete named-pipe stream. | 19 |
| **Native subtotal** |  |  | **156** |

## Reconciliation

```text
managed-deterministic 481
+ native-boundary     156
= total additions     637
```

The equation reconciles exactly by path and current line range. The historical global `192/383 = 50.13%` figure is **not** the scoped managed gate: it was a prior aggregate changed-line measurement before this explicit native-boundary classification and must not be reused as the managed numerator or denominator.

## Current task 4.1 settlement

- `historical_classification_revision`: `sha256:e344f980b0fddc92cbbbade3fc633d62b01f2a30632bba4dee0118b805a3f624` (superseded by the managed-gate manifest revision).
- `status`: passed for classification only; overall C2 remains blocked.
- `next_recommended`: task 4.2 managed coverage gate only; tasks 4.2–4.4 remain open and Unit D remains blocked.
- `harness_disposition`: invalidated/not run by scope; this classification performed no runtime or harness activity.

## Branch-normalization methodology

For the later managed gate, Cobertura records will be normalized to repository-relative source path plus source line. Duplicate async state-machine mappings will be unioned before line-hit counting: a line is hit when any normalized mapping reports a hit. Branches will be reported separately; branch totals will not substitute for line coverage, and no branch is removed merely because mappings are duplicated. Missing mappings remain eligible changed lines unless this manifest's concrete native rule applies.

## Rollback and provenance

- Rollback boundary: remove this classification manifest and the corresponding task/progress/evidence documentation additions only; do not revert A/B/C1, C2 production paths, tests, or unrelated working-tree changes.
- Provenance: read-only Git baseline/tree/diff inspection and current-source inspection; no production/test edits, build, test, coverage, certificate/store mutation, signing, harness, named-pipe loopback, native attempt ledger action, commit, push, PR, review, merge, or Unit D activity.
- This artifact records classification only. It does not claim managed coverage, runtime acceptance, or C2 PASS.

## Historical C2 executable-managed gate scope revalidation (superseded)

- Before measurement, the ten frozen C2 production paths produced `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c`; no stale classification was used.
- The `481 managed-deterministic` source scope and `156 native-boundary` classification remain unchanged. The executable-managed continuation added no production lines; candidate, classification, and denominator were not refreshed.
- The current ten-file C2 production snapshot remains `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c`; the current four-file focused-test snapshot is `sha256:555c2c68d9eb90519d6ff20f83b910ca0fa3e5798a69a4ea2ff5b62b5b293f77`. The prior `240/288` result is historical and unreproducible, did not retain a comparable test snapshot, and the fresh measurement therefore cannot reproduce that result identity.
- The sole authoritative fresh executable-managed gate is `213/288 = 73.958333%`; `213/481 = 44.282744%` is source/global context only and is never authoritative. The remaining mapped executable unhit count is `75`; Service focused result is `54/54`. Tasks 4.2/4.2.1–4.2.3 remain open; 4.3 remains next/open but blocked by native exclusions, and 4.4 and Unit D remain blocked. No production attribution or native activity occurred.
- Path+line proof for all 193 non-executable managed exclusions is retained in the current table in `evidence-manifest-unit-c2.md`; the native-boundary path+line ranges remain above in this manifest.
- Current managed-gate evidence revision: `sha256:3733a52fc3f673f8ebd24d4011cd66a763cf617a199da0d41e304a47abb2f331`.

## Historical C2 managed correction scope revalidation

- The immutable ten-file C2 production candidate remains `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c`; no production file changed in the correction. The four-file focused-test identity is `sha256:6402caea1b7fcee2e134e005311165bef34c7a3325e7e5a7334e28e60fe3f74c`.
- The classification remains exactly `481 managed-deterministic + 156 native-boundary = 637` production additions. The executable managed denominator remains exactly `288`; the correction added no source lines and changed no exclusions.
- Fresh behavior-value tests raised the authoritative managed result from `213/288` to `232/288 = 80.555556%`; `232/481 = 48.232848%` remains source/global context only. The prior `240/288` result remains historical and unreproducible.
- The 193 managed non-executable path+line exclusions and all native-boundary ranges remain unchanged. Branch evidence remains bounded aggregate only; exact changed-branch union is unavailable due to duplicate async state-machine mappings.
- Current task settlement: 4.2 and 4.2.1–4.2.3 complete; 4.3 next/open but blocked by native proof; 4.4 and Unit D remain blocked. No production attribution, native activity, or scope expansion occurred.
- Current managed-gate evidence revision: `sha256:da1a800c0ff3891b0aa763e0d98b4fdd6b04d51b2afbb72b13caa56a96706f31`.

## C2 signed-native scope settlement

- The native scope remains unchanged at `156` classified native-boundary production additions; this work unit made no production or test edits.
- The disposable harness used only the classified Service/SessionAgent transport boundary. Preflight passed before certificate creation; one CurrentUser-only certificate was created, signed artifacts were verified, and exactly one loopback was attempted.
- The single run failed before authenticated dispatch. Raw WinTrust values and production trace phases were not emitted by the harness and remain unavailable; no diagnostic rerun is permitted.
 - Cleanup proof and the current managed-gate result remain authoritative in `evidence-manifest-unit-c2.md`: certificate absent from all three CurrentUser stores, zero harness processes, and managed `232/288 = 80.555556%` preserved.

## C2 listener lifecycle correction scope update

- The lifecycle correction adds managed-deterministic ownership/fault/cancellation behavior in `src/ControlParental.Service/AgentLauncher.cs`; native-boundary classifications remain unchanged.
- The prior executable denominator is invalidated by these production additions. A new exact path+line classification/coverage reconciliation is required before task 4.2 can be complete; no `232/288` result is current.
- Current task state: 4.2 and 4.2.2–4.2.3 open; 4.3 open/blocked; 4.4 and Unit D blocked. No native runtime activity occurred.
- The current canonical revision is recorded in the sole CURRENT Result Contract in `evidence-manifest-unit-c2.md`.
- Scope status: task 4.3 remains open; task 4.4 and Unit D remain blocked. This manifest remains classification evidence and does not claim native acceptance.
- Current canonical evidence revision: `sha256:83369bd59957272e8f26b5a6ac45fd64582a7a758f7759e7527dffe18bc1d4c7`.

## C2 post-return listener cleanup scope update

- The post-return correction changes only managed listener ownership/fault cleanup; the original three-argument NamedPipeServer/session-binding contract and all PID/SID/session, auth, naming, WinTrust, and handshake classification ranges are preserved exactly.
- `ipcSync` provides atomic ownership detachment; fault cleanup cancels, stops, and disposes only the expected channel and never awaits the same faulted listener task. Explicit disposal and launch-failure cleanup use the same idempotent ownership boundary.
- The deterministic focused test proves successful process creation and launch return precede the listener fault, then awaits stop/disposal completion and asserts no connection plus completed listener task. Native-boundary ranges remain unchanged; no coverage measurement or native runtime activity was performed.
- Task 4.2 and 4.2.2–4.2.3 remain open; 4.3 remains open/blocked; 4.4 and Unit D remain blocked. Historical signed-run facts remain historical.
- Current canonical evidence revision: `sha256:e0b8a7e55c4cdec31dc355e067c5d9f29a417d7e0231d6a263a14c4b0968cb7b`.

## C2 atomic listener ownership scope update

- Managed ownership now consists of one immutable `(CTS, channel, start task)` record; atomic publication, expected-owner detachment, and disposed transitions remain under `ipcSync`.
- Native-boundary ranges remain unchanged. The original three-argument NamedPipeServer construction and auth/naming/WinTrust/PID/SID/session/handshake behavior are preserved.
- Current canonical evidence revision: `sha256:a565373ff7def34336fe6ab107a5a4b7c96ce70ab23fb39626b3e7f9095de8dd`.

## C2 provisional ownership cleanup scope update

- Managed-only correction: pre-publication CTS/channel/event/start resources are tracked independently until the immutable ownership record is publishable; native-boundary ranges and the original ownership protocol remain unchanged.
- The gated StartAsync interleaving rejects publication after Dispose and proves no process creation, cancellation, null AgentChannel, and one stop/dispose pair. Tasks 4.2/4.2.2–4.2.3 remain open; 4.3 open/blocked; 4.4 and Unit D blocked.
- Current canonical evidence revision: `sha256:46504dbdc50a45b6bd6ef43ced3a9242fe0b097449de31b0b82d01f53f610a2b`.

## C2 post-ownership exact classification (CURRENT)

- Frozen baseline remains commit `90a5a2a44299d99b67aff18da2a9182bc167fc75`; current production candidate is `sha256:af7340bd6af3babef79c5df644c787b82f9ccf28e124b0b5f37f2734fc24bce7`, and focused tests are `sha256:edbe0e4b7b4fb2c766ffd522b71b607e2ad95eafeca4a352f906e0067e3d8db8`. The current additions reconcile as `801 managed-deterministic + 156 native-boundary = 957`.
- AgentLauncher atomic ownership changes are classified managed-deterministic. Native-boundary ranges are unchanged and remain exactly the 156 lines listed above. The managed table and strict gate are authoritative in `evidence-manifest-unit-c2.md`.
- The 67 new AgentLauncher exclusions are independently source-reviewed syntax-only lines: `28,33,38-40,45,47,151-152,158,165-166,174,180-181,199,225-226,238,256,272,277,432,439-440,443,445,451,453-454,459,461-462,467-468,473-474,476,481,487-488,493,509-510,518,521-527,531,537,548-549,575-576,586,595-596,627,634,638,644-645,647` (declarations, signatures, braces, continuations, or structural compiler projections). Existing 193 exclusions retain their exact prior path+line proof; no exclusion is based on missing Cobertura mapping alone.
- Current status: exact classification produced, managed strict gate failed, 4.2/4.2.2–4.2.3 remain open, 4.3 remains open/blocked, and 4.4/Unit D remain blocked. Canonical revision is the one propagated by the CURRENT Result Contract in the evidence manifest.
- Current arithmetic: strict `>80%` of the `541` executable denominator requires `433` hits (`433/541 = 80.036969%`); the measured `421` hits require `12` additional hits. The settled native diagnosis's `13`-hit wording, if cited historically, is an arithmetic error and is not treated as current authority.
- Canonical evidence revision: `sha256:457be3deebac2e1469727280f060fed68d85da13aea077d7ad0ad2f7774b446b`.

## C2-final-focused-tests coverage update (CURRENT)

- The frozen production classification remains `801 managed-deterministic + 156 native-boundary = 957`; the native ranges are unchanged.
- Fresh exact executable reconciliation remains `541` denominator with `433` hits and `108` mapped unhit. The strict gate passes at `433/541 = 80.036969%`; no denominator or exclusion change was made.
- Only the existing AgentLauncher seam test file was edited for six behavior-value tests and 12 additional executable hits. Task 4.2 and 4.2.2–4.2.3 are complete; 4.3 remains open/blocked, and 4.4/Unit D remain blocked.
- Current canonical evidence revision: `sha256:cfa3db6bf8760b2bd9baf7a8d05f6572a0eccba8d34837fcd7d124dab4b1aa55`.
