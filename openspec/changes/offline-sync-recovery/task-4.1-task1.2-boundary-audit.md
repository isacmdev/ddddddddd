# Task 1.2 Boundary Reconstruction Audit

**Result: WARNING UNDER EXPLICIT HISTORICAL EXCEPTION — exact predecessor link remains unrecoverable.**

## Authorized exception

The user explicitly authorized closing task 4.1 with this one historical gap as a warning because task-1.2 functionality and tests are independently green. The exception applies **only** to the missing link from the approved foundation terminal to the first preserved task-1.2 pre-state. It does not waive task-1.2's internal remediation chain, final blob equality, runtime gates, coverage, the 800-line boundary, or any other child's immutable evidence. This is a provenance exception, not fabricated patch/numstat evidence.

## Required link

Task 1.2 must start at the immediately prior approved foundation terminal state and end at the exact task-2.1 base. The approved foundation anchor is `7b74a0a4b6430610b344cea0afa9da7493e1a084`; task 2.1 starts at `8fdfb895ccf1ad6411902724d3a636ed4831ed90`. Those commits have identical task-1.2 source/test blobs:

| Path | Git blob at 7b and 8f | Bytes | SHA-256 |
|---|---|---:|---|
| `src/ControlParental.Service/SqliteSchemaBootstrapper.cs` | `e87b2499d863aac882200d1548436b70f0f3a1bb` | 15471 | `f231e717b4abf933b35e4fab5d58a214641d61192b614a7e598d4ea64467b80d` |
| `tests/ControlParental.Service.Tests/SchemaAdoptionTests.cs` | `0d124f895563a981947baf4eb565a3280ddfa1d0` | 36046 | `98b2ef4b05cd0bde2bd40678b08a328a47cf54cd54474f9d20fa768a1f11bbc3` |

The repository history contains only the baseline introduction of these paths at `7b74a0a`; `git log --all --reflog -- <paths>` finds no earlier committed task-1.2 child. Commit `8fdfb89` is a docs/governance materialization whose exact parent is `3315ffb`; it does not materialize a task-1.2 source transition. Therefore the first required link—foundation terminal to task-1.2 pre-state—is absent, and the zero source diff from 7b to 8f cannot be relabeled as the whole task-1.2 implementation.

## Historical external chain inspected

The three known evidence directories exist and were inspected read-only:

- `C:\Users\Usuario\AppData\Local\Temp\sdd6-task12-final-remediation-20260819\`
- `C:\Users\Usuario\AppData\Local\Temp\sdd6-task12-current-marker-20260819\`
- `C:\Users\Usuario\AppData\Local\Temp\sdd6-task12-base-definition-20260819\`

They provide a byte-contiguous **historical remediation chain**, but not the required child start:

| Stage | Pre bytes (source/test) | Pre SHA-256 (source/test) | Patch | Patch SHA-256 | Reported target bytes |
|---|---:|---|---|---|---:|
| Initial remediation | 10397 / 21978 | `89c262b3...` / `f9122925...` | `remediation-only-final2.patch`, 20761 bytes | `1AC341044000456A0BB98136CBD72418C3A3CBFA733A0B9278A985EBA14828FC` | 12748 / 29765 |
| Current-marker remediation | 12748 / 29765 | `fc1d598a...` / `fdeb3f78...` | `remediation-only-final.patch`, 10670 bytes | `370A0E61941BAD7ABA6747A4957F317B1C4D4F580AF3C717B29069CD4132B2D3` | 13723 / 34425 |
| Base-definition remediation | 13723 / 34425 | `3d57df0f...` / `3dde5292...` | `remediation-only-final.patch`, 8252 bytes | `34FC5A79C4BEBC464D017FC53A90CD15953D135341857BC4D6EF905B2AE7873F` | 15471 / 36046 |

The final stage's target bytes equal the 7b/8f Git blobs above. The first stage's pre-state is not the approved foundation terminal state and has no Git commit/blob or approved immutable base link. The patches use `.pre`/working-file unified-diff names rather than a native Git commit patch; consequently no exact-base `git apply --check --binary` proof from the approved foundation terminal can be claimed.

## Exhaustion commands and results

- `git log --all --graph --decorate --oneline`: no task-1.2 child commit; `8fdfb89` subject is `docs(sdd6): accept audited foundation baseline`.
- `git log --all --reflog -- <task-1.2 paths>`: only baseline introduction at `7b74a0a`.
- `git ls-tree 7b74a0a <paths>` and `git ls-tree 8fdfb89 <paths>`: identical final blobs.
- `git worktree list --porcelain`, branch/tag inspection, and reflog inspection: no alternate task-1.2 branch or tag.
- `git fsck --full --no-reflogs --unreachable`: no unreachable commit; dangling objects do not provide a task-1.2 commit/base link.
- External patch SHA-256 and pre-file byte/hash inspection: completed above; they establish only an unanchored remediation subchain.

## Runtime support for the narrow exception

The approved task-1.2 v2 report records the independently green runtime facts supporting this narrow exception: **41 focused schema/startup/compiled-model tests passed**, **1,111/1,111** full Service regression passed for the approved slice, and **100% changed-scope line and branch coverage** for the remediation scope. Later integrated evidence records **1,156/1,156** full Service regression green. These are cited historical report facts; no product test was rerun for this docs-only remediation.

## Gate conclusion

The predecessor link remains missing, but is now **WARNING — AUTHORIZED HISTORICAL EXCEPTION**. The internal task-1.2 patch chain and final equality to the `7b74a0a` / `8fdfb89` blobs remain preserved above. Whole-child no-double-count numstat is **UNKNOWN under authorized historical boundary exception**; it is not reported as 94, 76, 194, or any partial total. No exception applies to any other child.
