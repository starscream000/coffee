# Review 0002: review fixes and the final names

- Date: 2026-10-09
- Written by: reviewer
- Instruction: [0002](../instructions/0002-review-fixes-and-names.md)
- Report: `handoff/reports/0002-review-fixes-and-names.md` on the branch
  `chore/apply-product-names` (it reaches `main` when pull request #4 is merged)
- Verdict: **Approved**

## Pull requests

| Pull request                                         | Branch                      | Reviewed at | CI                                | Verdict  |
| ---------------------------------------------------- | --------------------------- | ----------- | --------------------------------- | -------- |
| [#3](https://github.com/starscream000/coffee/pull/3) | `docs/review-0001-fixes`    | `fb18b0e`   | green on Linux, Windows and macOS | Approved |
| [#4](https://github.com/starscream000/coffee/pull/4) | `chore/apply-product-names` | `49b9b19`   | green on Linux, Windows and macOS | Approved |

Merge #3, then #4.

## What I checked

- **Each of the nine findings of review 0001**, in the named file on
  `docs/review-0001-fixes`. All nine are fixed as asked.
- **The rename, word by word.** I listed every distinct word the branch changed
  in the documents and every changed line in code and config. All of them are
  the three names, their derived forms (`cfe.config.yaml`, `CFE_`), or the
  sentences that described the names as pending. Nothing else moved.
- **The lockfile**: two lines, both a workspace package name.
- **`product.test.ts`** is unchanged and passes.
- **CI** on both pull requests, on all three systems.
- **The stack**: #3 sits on `main`, #4 sits on #3.

## What I could not check

- Task 1 (the implementer's local branches and `gh auth status`) happened on the
  owner's machine. The pull requests the implementer opened are evidence that
  `gh` works.

## Findings

1. **Note. `getEngineInfo()` held the package name as a literal string.** The
   report flagged this itself and changed the literal to the new name, which
   was the right call for this branch. The later branch `feat/engine-rpc`
   already builds the name from `PRODUCT.npmScope`, so nothing is left to do.

The implementer's two departures (the literal above, and bullets instead of a
numbered list for open questions, because Prettier renumbers lists) are both
sound.

## Done-when checks

| Check                                                           | Result                             |
| --------------------------------------------------------------- | ---------------------------------- |
| `pnpm verify` passes on both branches                           | Yes (CI, and implementer)          |
| Both pull requests open, CI green on three systems              | Yes                                |
| Each of the nine findings fixed in the named file               | Yes                                |
| `PRODUCT` holds `cfe`, `.cfe`, `@cfe`; tests pass               | Yes, tests unchanged               |
| Only names and import specifiers changed under `packages/*/src` | Yes, plus the literal in finding 1 |
| Lockfile differs only in workspace package names                | Yes                                |
| The `git grep` list is in the report                            | Yes, and every entry is justified  |
| No file under `handoff/` changed except the new report          | Yes                                |
| The report exists and follows the template                      | Yes                                |

## Owner decisions needed

None.

## For the next instruction

Nothing from this instruction. Milestone 0 is finished when #3 and #4 are
merged.
