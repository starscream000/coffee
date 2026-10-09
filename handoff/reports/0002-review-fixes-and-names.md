# Report 0002: review fixes and the final names

- Date: 2026-10-09
- Written by: implementer
- Instruction: [0002](../instructions/0002-review-fixes-and-names.md)

## Summary

All nine findings of review 0001 are fixed on `docs/review-0001-fixes` (#3). The
final names `cfe`, `.cfe` and `@cfe` are applied everywhere on
`chore/apply-product-names` (#4), which is stacked on #3. Both pull requests
are open and CI is green on Linux, Windows and macOS for each. One change under
`packages/*/src` goes beyond what the instruction lists (the engine's reported
name string); see "Departures".

## Branches and pull requests

| Branch                      | Last commit                                   | Pull request                                         | Pushed |
| --------------------------- | --------------------------------------------- | ---------------------------------------------------- | ------ |
| `docs/review-0001-fixes`    | `fb18b0e`                                     | [#3](https://github.com/starscream000/coffee/pull/3) | yes    |
| `chore/apply-product-names` | the commit that adds this report (branch tip) | [#4](https://github.com/starscream000/coffee/pull/4) | yes    |

#4 shows #3's commits until #3 is merged; its description says so. Merge #3
first.

## Tasks

| Task | State | Notes                                                                                                                                                                                                                                                          |
| ---- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | done  | `main` fast-forwarded to `6818080`. Deleted `chore/project-rules`, `chore/scaffold`, `chore/product-placeholder`, `chore/split-product-identity` and `docs/milestone-0-review-changes`, each after checking it is contained in `main`. `gh auth status` below. |
| 2    | done  | Finding 1: `docs/step-format.md`, "Saved logins": HMAC-SHA-256 over the login flow and every flow it calls, with a link to ADR 0018.                                                                                                                           |
| 3    | done  | Finding 2: `docs/adr/0014-secret-masking.md`, "Header values in variables"; `docs/actions.md` after the waiting table; new check S19 in the definition of done (also listed in A9), assigned to plan branch 13.                                                |
| 4    | done  | Finding 3: `docs/adr/0018-saved-logins.md`, "Clean-up at the start of every run" (new warning code `LoginCleanupFailed`, also added to `docs/protocol.md`); check I11 extended. Status stays Proposed.                                                         |
| 5    | done  | Finding 5: ADR 0018, "What the HMAC key protects, and what it does not".                                                                                                                                                                                       |
| 6    | done  | Finding 4: `step-format.md` and `actions.md` read "Accepted design". Definition of done and plan stay drafts.                                                                                                                                                  |
| 7    | done  | Finding 6: decision 5 says the format is settled by decision 15. Open questions are now bullets with fixed IDs Q14 and Q35, because Prettier renumbers ordered lists (which is how 35 became 15 last time).                                                    |
| 8    | done  | Finding 7: one opening sentence.                                                                                                                                                                                                                               |
| 9    | done  | Finding 8: A7 lists the diagnostics for `fillOtp`, `expect.priceFormat` and `click`.                                                                                                                                                                           |
| 10   | done  | Finding 9: branch 8 split into `feat/runner-core` (8) and `feat/runner-rows-results` (9); later branches renumbered to 16. A script confirmed every one of the 50 check IDs appears exactly once.                                                              |
| 11   | done  | Names applied (details below).                                                                                                                                                                                                                                 |
| 12   | done  | ADR 0019 records the final and interim values, the date and the warning about the unscoped `cfe` package.                                                                                                                                                      |
| 13   | done  | List below.                                                                                                                                                                                                                                                    |

### Task 1: `gh auth status`

`gh` was not on the PATH of my shell (it was installed after the shell
started), so I called it by its full path, `C:\Program Files\GitHub CLI\gh.exe`:

```
github.com
  ✓ Logged in to github.com account starscream000 (keyring)
  - Active account: true
  - Git operations protocol: https
  - Token scopes: 'gist', 'read:org', 'repo', 'workflow'
```

`gh --version`: 2.102.0 (2026-09-30).

### Task 11: what was renamed

- `packages/protocol/src/product.ts`: `command: 'cfe'`, `dataDir: '.cfe'`,
  `npmScope: '@cfe'`. The "interim" paragraph in the TSDoc was replaced by the
  warning not to refer to the unscoped `cfe` package.
- Package names `@cfe/protocol`, `@cfe/engine`, `@cfe/cli`; CLI `bin` `cfe`;
  root package `coffee`; workspace dependencies `@cfe/protocol`.
- Import specifiers in `packages/*/src` (4 lines).
- `.gitignore` and `.prettierignore` (`.cfe/`), `eslint.config.js` (ignore
  `.cfe/**`, dependency-rule patterns `@cfe/*`, `@cfe/engine`).
- `pnpm-lock.yaml`, regenerated with `pnpm install`: the only change is the two
  `'@test-tool/protocol'` keys becoming `'@cfe/protocol'`. No dependency added,
  removed or upgraded.
- All documents under `docs/`, `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`.
  The protocol example's client name is now `@cfe/cli`.
- `CLAUDE.md`: only the opening sentence that said the names were pending.
  "Engineering standards" mentioned no affected names.
- `product.test.ts` is unchanged and passes.
- `node packages/cli/dist/main.js --version` prints `cfe 0.0.0 (protocol 0.0.0)`.

### Task 13: remaining matches

`git grep -n -I -E "coffee\.config|\.coffee|@test-tool|test-tool|\bcoffee\b"` on
the branch tip, before this report was added:

| File and line(s)                                                                                  | Why it stays                                                                                                                                                                                       |
| ------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `package.json:2` `"name": "coffee"`                                                               | The root package name the instruction asks for.                                                                                                                                                    |
| `CHANGELOG.md:45`                                                                                 | States that the root package is `coffee`.                                                                                                                                                          |
| `docs/adr/0017-product-identity.md:17, 37, 38`                                                    | History: what ADR 0017 decided (`PRODUCT_ID` "coffee", scope `@test-tool`).                                                                                                                        |
| `docs/adr/0019-four-product-names.md:10–12, 45`                                                   | History: why `coffee` and `.coffee` could not stay (CoffeeScript).                                                                                                                                 |
| `docs/adr/0019-four-product-names.md:24–26`                                                       | The "interim value" column of the names table.                                                                                                                                                     |
| `docs/adr/0019-four-product-names.md:30`                                                          | States that the root package is `coffee`.                                                                                                                                                          |
| `docs/adr/0019-four-product-names.md:47`                                                          | The rejected options `coffeeqa`, `coffeetest`, `coffee-e2e`.                                                                                                                                       |
| `docs/architecture.md:10`                                                                         | Explains that `coffee` and `.coffee` belong to CoffeeScript.                                                                                                                                       |
| `handoff/STATUS.md`, `handoff/instructions/*`, `handoff/reports/0001-*`, `handoff/reviews/0001-*` | Handoff files are not mine to change (instructions, reviews, status) or are a published record (report 0001). They contain the old names and the repository URL `github.com/starscream000/coffee`. |

## Checks

| Command                                                                                  | Result                                                                                 |
| ---------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| `pnpm verify` on `docs/review-0001-fixes`                                                | passed (Windows): type check, lint, Prettier, 10 tests in 4 files                      |
| `pnpm verify` on `chore/apply-product-names`, after deleting `dist/` and `*.tsbuildinfo` | passed (Windows): same                                                                 |
| CI on #3 (`fb18b0e`)                                                                     | `verify` passed on ubuntu-latest (31 s), macos-latest (25 s), windows-latest (1 m 9 s) |
| CI on #4 (before this report's commit)                                                   | `verify` passed on ubuntu-latest (23 s), macos-latest (36 s), windows-latest (54 s)    |
| Plan covers every check exactly once (script)                                            | 50 IDs, none missing, none twice                                                       |
| Lockfile diff                                                                            | 2 lines changed, both workspace package names                                          |

The report's own commit triggers CI on #4 again; it changes only this file.

## Departures from the instruction

1. **One more change under `packages/*/src`.** `getEngineInfo()` in
   `packages/engine/src/index.ts` returned the literal string
   `'@test-tool/engine'`, and its test expects it. That literal is neither a
   `product.ts` value nor an import specifier, but leaving it would make the
   engine report a package name that no longer exists. I changed it to
   `'@cfe/engine'` in both files. (Writing it literally at all breaks the
   "never write names literally" rule; see "Suggestions".)
2. **Open questions in architecture.md are bullets, not a numbered list.**
   Prettier renumbers ordered lists, which is how "35" became "15" in the
   previous branch. Bullets with fixed IDs (Q14, Q35) keep the numbers.

## Decisions I made

- Check **S19** (`tests/header-token.test.yaml`) is a new sample rather than a
  new acceptance check, and the plan gives it to branch 13
  (`feat/actions-data-http-flows`), where `wait.response` and `api` arrive.
- New warning code `LoginCleanupFailed` for a saved login that cannot be
  deleted, named like the existing `RunCleanupFailed`.
- Saved-login clean-up also deletes a state file whose metadata cannot be
  read, and never deletes the `.key` file.
- A header value shorter than 4 characters is not registered as a secret, and
  the engine logs a warning that names the header but not its value.
- Plan split: branch 8 `feat/runner-core` turns S10, S11, S12, S14, F1, F2, F3
  and F7 green; branch 9 `feat/runner-rows-results` turns S6, S13, S15, I8, I9
  and I10 green.

## Questions for the owner

None for this instruction.

## Not done, not pushed, not verified

- The "Q14 names" open question in `docs/architecture.md` is now out of date,
  because the names are chosen. Instruction 0003, task 1, moves it to "Review
  decisions"; I left it for that task rather than doing it twice.
- CI for the report's own commit on #4 was still running when this report was
  written.

## Suggestions

- `getEngineInfo()` should build its name from `PRODUCT.npmScope` instead of a
  literal, so the next rename cannot miss it.
- Add `C:\Program Files\GitHub CLI` to the PATH for new shells (it is
  probably already there for shells started after the install).
