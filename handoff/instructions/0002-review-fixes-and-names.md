# Instruction 0002: review fixes and the final names

- Date: 2026-10-09
- Written by: reviewer
- Based on `main` at: `311d891` or later
- Replaces: none
- Follows review: [0001](../reviews/0001-finish-milestone-0-docs.md)

## Goal

The nine findings of review 0001 are fixed in the documents, and the product's
command, data folder and npm scope carry their final names everywhere. No
Milestone 1 code is written.

## Owner decisions

1. Pull request #2 is merged. `main` now holds the Milestone 0 documents.
2. **The command name is `cfe`.** The owner left the other two names to the
   reviewer, who chose them to match:

   | Name        | Final value | Derived from it                            |
   | ----------- | ----------- | ------------------------------------------ |
   | Display     | `Coffee`    |                                            |
   | Command     | `cfe`       | `cfe.config.yaml`, `CFE_` variable prefix  |
   | Data folder | `.cfe`      |                                            |
   | npm scope   | `@cfe`      | `@cfe/protocol`, `@cfe/engine`, `@cfe/cli` |

   Checked by the reviewer on registry.npmjs.org on 2026-10-09: no npm
   organisation or user owns `cfe`, and no package exists in the `@cfe` scope.
   An unscoped package named `cfe` does exist and belongs to someone else.

3. The GitHub CLI is installed on the implementer's machine. From now on the
   implementer opens its own pull requests.
4. **Not decided yet:** ADR 0018 stays Proposed, and the Milestone 1 plan still
   waits for the owner's go-ahead.

## Branches

| Branch                      | Based on                 | What it holds          |
| --------------------------- | ------------------------ | ---------------------- |
| `docs/review-0001-fixes`    | `main`                   | Tasks 2 to 10          |
| `chore/apply-product-names` | `docs/review-0001-fixes` | Tasks 11 to 13, report |

Open one pull request per branch, both into `main`. The second one will also
show the first one's commits until the first is merged; say so in its
description. The reviewer merges them in order.

## Tasks

### Part A: housekeeping (no files change)

1. Run `git switch main` and `git pull --ff-only`. It should fast-forward now.
   If it does not, stop and say why in the report. Then delete the local
   branches that are fully contained in `main` (the four listed in report 0001,
   and `docs/milestone-0-review-changes`). Run `gh auth status` and put its
   result in the report.

### Part B: fixes from review 0001 (`docs/review-0001-fixes`)

2. **Finding 1.** In `docs/step-format.md`, "Saved logins", describe the cache
   key exactly as ADR 0018 does: HMAC-SHA-256, covering the login flow and every
   flow it calls.
3. **Finding 2.** In `docs/adr/0014-secret-masking.md`: variables keep the real
   value of a header. When `api`, `wait.response` or `expect.response` stores a
   `Cookie`, `Set-Cookie` or `Authorization` header, the engine registers its
   value (for cookies, each cookie's value) as a secret for the rest of the
   run, so it is masked wherever the engine writes it out. Values shorter than
   4 characters follow the existing rule. Say the same in `docs/actions.md`
   where those actions' `as` variables are described. Add a check to the
   definition of done: a test reads a token from a response header, sends it in
   a later request that succeeds, and the A9 scan finds the token nowhere.
4. **Finding 3.** In `docs/adr/0018-saved-logins.md`: at the start of a run the
   engine deletes saved states older than their `maxAge`, and states whose
   metadata names an environment or login the config no longer has. A state
   that cannot be deleted is a warning, never a failed run. Extend check I11 to
   prove it. Keep the ADR's status as Proposed.
5. **Finding 5.** In the same ADR, say what the HMAC key does and does not
   protect: it helps when a file name leaks without the folder; it does not
   help when the whole folder is copied, because the key is in it.
6. **Finding 4.** Make the status line of `docs/step-format.md` and
   `docs/actions.md` match `docs/architecture.md` and `docs/protocol.md`
   ("Accepted design"). Leave the definition of done and the plan as drafts.
7. **Finding 6.** In `docs/architecture.md`: review decision 5 must no longer
   say "awaiting approval", and the open question about ADR 0018 must not reuse
   the number 15.
8. **Finding 7.** In `docs/actions.md`, "Trust model": one opening sentence.
9. **Finding 8.** In the definition of done, A7 lists the expected diagnostic
   for all three bad actions in F9, including `click`.
10. **Finding 9.** In `docs/milestones/v0.1.0-plan.md`, split branch 8 into two
    branches now, and renumber. Every check must still appear exactly once.

### Part C: apply the names (`chore/apply-product-names`)

11. Apply the names from owner decision 2:
    - `packages/protocol/src/product.ts`: the three values, and remove the word
      "interim" from its comments.
    - Every workspace `package.json` name, the CLI's `bin` entry, and every
      import specifier that uses the scope. Rename the root package to
      `coffee`.
    - `.gitignore`, `.prettierignore`, `eslint.config.js`, and the lockfile
      (regenerate it with `pnpm install`; no dependency may be added, removed
      or upgraded).
    - `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md` and everything under
      `docs/`, including the ADRs.
    - `CLAUDE.md`: only the sentence that says the names are pending, and the
      example names in "Engineering standards" if any are affected. Change
      nothing else in that file.
12. In `docs/adr/0019-four-product-names.md`: record the final values, the
    date, and one warning for the docs to follow: the unscoped npm package
    `cfe` belongs to someone else, so instructions always name the scoped
    package (`@cfe/cli`) and never suggest `npx cfe`. ADR 0017 and 0019 may
    still mention the old interim names where they describe the history.
13. After the rename, run `git grep` for `coffee.config`, `.coffee`,
    `@test-tool`, `test-tool` and a whole-word, case-sensitive `coffee`. List
    every remaining match in the report with the reason it stays. Expected to
    stay: files under `handoff/`, the history passages in ADRs 0017 and 0019,
    and the repository's own name in URLs.

## Done when

- [ ] `pnpm verify` passes on both branches.
- [ ] Both pull requests are open, and CI is green on Linux, Windows and macOS
      for each. Wait for CI (`gh pr checks <number> --watch`) and put the
      result in the report.
- [ ] Each of the nine findings can be shown fixed in the named file.
- [ ] `PRODUCT` holds `cfe`, `.cfe` and `@cfe`, and its tests pass unchanged or
      with changes the report explains.
- [ ] The only changes under `packages/*/src` are the values and comments in
      `product.ts` and import specifiers.
- [ ] The lockfile differs only in workspace package names.
- [ ] The `git grep` list from task 13 is in the report.
- [ ] No file under `handoff/` is changed except the new report.
- [ ] `handoff/reports/0002-review-fixes-and-names.md` exists on
      `chore/apply-product-names` and follows [the template](../templates/report.md).

## Out of scope

- Any Milestone 1 code, including `feat/protocol-types`.
- Marking ADR 0018 Accepted, or changing the plan's or the definition of done's
  draft status.
- Raising the Node pin.
- Pushing to `main`, merging into `main`, or force-pushing anything.

## Report back

- The result of task 1, including `gh auth status`.
- The pull request numbers and the CI result for each.
- The task 13 list.
- Anything in this instruction you think is wrong.
