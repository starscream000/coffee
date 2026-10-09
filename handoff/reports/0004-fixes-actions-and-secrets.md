# Report 0004: review fixes, user actions, variables and secrets

- Date: 2026-10-09
- Written by: implementer
- Instruction: [0004](../instructions/0004-fixes-actions-and-secrets.md)

## Summary

All 23 tasks are done on four stacked branches (#10 to #13), and CI is green on
Linux, Windows and macOS for each. Each of the eight findings of review 0003 has
a test that I ran against the unfixed code and saw fail. Plan branches 4 and 5
exist; checks **F9**, **I12** and **I13** pass through the protocol. No branch
passed the 1,500-line guide, so none was split. One conflict with the
instruction: the type of `ctx` needs Playwright's types, which may not be added
yet (see "Departures").

## Branches and pull requests

Merge in order #10, #11, #12, #13. Line counts leave out fixtures (`examples/`,
`packages/engine/test/`), generated schema files and the lockfile.

| Branch                     | Last commit                                   | Pull request                                           | Pushed | CI (Linux, Windows, macOS)                     | Changed lines (of which tests)   |
| -------------------------- | --------------------------------------------- | ------------------------------------------------------ | ------ | ---------------------------------------------- | -------------------------------- |
| `fix/review-0003-findings` | `a502bd7`                                     | [#10](https://github.com/starscream000/coffee/pull/10) | yes    | pass, pass, pass                               | +478 −112 (≈ 250)                |
| `docs/review-0003-rulings` | `42bba43`                                     | [#11](https://github.com/starscream000/coffee/pull/11) | yes    | pass, pass, pass                               | +116 −13 (docs, one schema line) |
| `feat/actions-sdk`         | `9c824b3`                                     | [#12](https://github.com/starscream000/coffee/pull/12) | yes    | pass, pass, pass                               | +1,385 −84 (435)                 |
| `feat/context-secrets`     | the commit that adds this report (branch tip) | [#13](https://github.com/starscream000/coffee/pull/13) | yes    | pass, pass, pass (before this report's commit) | +1,147 −12 (421)                 |

## Tasks

| Task | State                    | Notes                                                                                                                                                                                                                       |
| ---- | ------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | done                     | `main` fast-forwarded to `27397f1`; seven merged local branches deleted.                                                                                                                                                    |
| 2–9  | done                     | One test per finding; see the next table.                                                                                                                                                                                   |
| 10   | done                     | Every ruling marked Accepted or Document is in step-format.md, actions.md or protocol.md; ruling 6 is implemented in task 20; ruling 17 was fixed in task 8.                                                                |
| 11   | done                     | `log.data` added to the schema, JSON Schema regenerated (only `event.log.json` changed), protocol.md updated; `PROTOCOL_VERSION` stays `0.1.0`.                                                                             |
| 12   | done, with one departure | `@cfe/engine/sdk`: `defineAction`, `target()`, `z`, `ActionError`, `AssertionError` (with `sdkError`), and the `ctx` type. Playwright's types are placeholders.                                                             |
| 13   | done                     | `ActionRegistry` with `RESERVED_NAMESPACES` (`expect`, `wait`, `api`, `cfe`); `ActionNameNotNamespaced`, `ActionNamespaceReserved`, `ActionNameTaken`, plus `InvalidActionName`, `InvalidShorthand`, `InvalidActionExport`. |
| 14   | done                     | esbuild bundles into `.cfe/cache/actions/` with source maps; cache by content; SDK redirected; Playwright redirect marked with a comment.                                                                                   |
| 15   | done                     | `openProject` loads actions and returns their diagnostics, including `SdkVersionMismatch`; `validate` checks user-action steps and reports `ActionNotLoaded`.                                                               |
| 16   | done                     | `listActions` with JSON Schema parameters.                                                                                                                                                                                  |
| 17   | done                     | `demo.addTodo` and `tests/user-action.test.yaml`; `examples/demo-app-bad-actions/`; protocol tests for F9, I12, I13; unit tests for registry, names, cache, syntax error.                                                   |
| 18   | done                     | `VariableStore`, `interpolate`, `InterpolationError` (`variable` set for an unset one), `unsetVariables` for the runner.                                                                                                    |
| 19   | done                     | `selectEnvironment`: name, default, or first; settings resolved environment → defaults → built-in.                                                                                                                          |
| 20   | done                     | `SecretStore`: environment, then `.env`; under 4 characters rejected; `validate` reports `SecretNotSet` / `SecretTooShort` where a test uses the secret.                                                                    |
| 21   | done                     | `SecretRegistry` and `mask`, five encoded forms, longest first, values addable at run time.                                                                                                                                 |
| 22   | done                     | The writer masks the whole message before measuring or truncating it.                                                                                                                                                       |
| 23   | done                     | Unit tests for each rule; protocol test with the secret in the engine's environment.                                                                                                                                        |

### Each finding and the test that proves its fix

I ran each test against `main`'s version of the files the fix changed; each
failed there and passes with the fix.

| Finding                          | Test                                                                                                                                                                      |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1 console to stdout              | `rpc/protocol.test.ts`: "review 0003 finding 1: no console method writes to stdout" (child engine calls every console method)                                             |
| 2 exit before answering          | `rpc/protocol.test.ts`: "review 0003 finding 2: answers requests already received before exiting when stdin closes" (slow handler)                                        |
| 3 folder in `validate`           | `project/project.test.ts`: "finding 3: validate on a folder is a diagnostic, not an internal error"                                                                       |
| 4 unclosed `${`                  | `project/project.test.ts`: "finding 4: an unclosed ${ is reported at its value"                                                                                           |
| 5 regular expressions            | `stepfile/validate-file.test.ts`: "finding 5: regular expressions must compile, and extract needs one group"                                                              |
| 6 messages                       | `validate-file.test.ts`: "finding 6: a duration given as a number…" and "…the target: hint"; `rpc/session.test.ts`: "finding 6: validate with neither files nor content…" |
| 7 stale shared targets           | `project/project.test.ts`: "finding 7: validate re-reads shared targets files"                                                                                            |
| 8 reserved env values            | `project/project.test.ts`: "finding 8: an environment value may not be named name or baseUrl"                                                                             |
| Task 22 (mask before truncating) | `rpc/message-writer.test.ts`: "masks a secret before truncating…" (fails on the old writer)                                                                               |

### `openProject` timing (task 15)

Measured through a real engine process on Windows, from the `initialize`
answer to the `openProject` answer, on `examples/demo-app` (one action file),
five runs each:

- cold cache (`.cfe/` deleted): median **60 ms** (59–70 ms)
- warm cache: median **29 ms** (29–30 ms)

## Checks

| Command                                                                  | Result                                        |
| ------------------------------------------------------------------------ | --------------------------------------------- |
| `pnpm verify` on each branch (Windows, Node 24.11.0)                     | passed; on the last branch 310 tests          |
| CI on #10, #11, #12, #13                                                 | `verify` passed on all three systems for each |
| `git grep` for `eslint-disable`, `@ts-expect-error`, `@ts-ignore`, `any` | none                                          |
| Committed JSON Schema files match the generator                          | yes (the schema test passes)                  |
| No client imports engine code                                            | yes (lint rule unchanged and passing)         |
| Dependencies added                                                       | `esbuild` 0.28.2 (engine) only                |

## Departures from the instruction

1. **`ctx`'s type uses placeholders for Playwright's types.** Task 12 asks for
   `ctx` "exactly as docs/actions.md describes it", and that document types
   `page`, `request` and `locate`'s result as Playwright's `Page`,
   `APIRequestContext` and `Locator`. The instruction also forbids adding
   Playwright. I declared opaque placeholder interfaces with those names in
   `sdk/context.ts`, marked for replacement in the branch that adds Playwright,
   and said so in actions.md. A user action cannot call `ctx.page.click()` in
   type-checked code until then.
2. **Root `package.json` allows esbuild's install script**
   (`pnpm.onlyBuiltDependencies: ["esbuild"]`). pnpm 10 skips install scripts
   by default. esbuild works without it, using its platform package, but the
   script is its documented fallback.
3. **The cache key is the entry file's content, but reuse also checks every
   input file.** ADR 0008 says "keyed by file content hash". An action that
   imports a local helper would otherwise stay stale when only the helper
   changes; the loader records each input's hash and rebuilds when any
   differs.
4. **Finding 7's fix re-finds files too**, not only shared targets: a new test
   or flow file is also seen without reopening the project.
5. **Tooling for the examples:** `tsconfig.check.json` type-checks
   `examples/*/actions` with a path mapping for `@cfe/engine/sdk`; ESLint
   ignores `packages/*/test/projects/**` (deliberately noisy test code) and
   `.cfe/` at any depth.
6. **One local commit was amended** to fix a lint error before the branch was
   pushed. Nothing pushed was rewritten.

## Decisions I made

Places where `docs/` was unclear, wrong or silent:

1. **`/regex/` URL patterns clash with path globs.** The docs say URL patterns
   are globs "unless written as /regex/". So `wait.url: /orders/` is read as the
   regular expression `orders`, not the path `/orders/`. I implemented the rule
   as written. It will surprise people, so I recommend a marker that cannot be a
   path (for example `regex: …`, or `~orders~`). **This is the most important item.**
2. **`SdkVersionMismatch` points at `package.json` line 1** (or at the installed
   package's manifest if there is no `package.json`).
3. **Secrets:** the environment variable wins over `.env`. `.env` lines may use
   `export`, quotes and `#` comments. A too-short secret is reported like a
   missing one, at its use sites in `validate`, not in `openProject`. The secret
   registry is engine-wide and is cleared when another project is opened.
4. **Interpolation into text:** `null` and objects become JSON text; an
   undefined value becomes empty text. A missing `env` value at run time is a
   `PathNotFound` error.
5. **Action-name suggestions** take the namespace from the file name
   (`actions/auth.ts` → `auth.fillOtp`).
6. **A step calling an unknown namespaced action** while some action files
   failed to load gets `UnknownAction` with those files in its hint. A step
   calling a _rejected_ action gets `ActionNotLoaded` with the reason.
7. **`extract`'s pattern**: named groups count as capturing groups.
8. **Masking also covers mapping keys**, not only values.
9. **`listActions` without an open project** returns the built-ins.

## Questions for the owner

1. **The `/regex/` rule (decision 1).** Options: (a) keep it as documented;
   (b) require an explicit marker for regular expressions. Recommended: **b**,
   before any sample step file depends on it.

## Answer: ordering plan branches 6 and 7

Move the CI job from branch 7 into branch 6, and keep the demo web server in 7.
`ctx.locate` can be tested against static HTML loaded with `page.setContent`,
with no server. Branch 6 therefore needs only Playwright, `playwright install
chromium` in CI, and an `integration` job that runs those tests on all three
systems. Branch 7 then adds the demo app server and its harness to a job that
already exists. This keeps each branch reviewable on its own and proves
Chromium on Windows and macOS as early as possible. The alternative, swapping 6
and 7, would build the demo server before anything can use it.

## Not done, not pushed, not verified

- CI for this report's own commit on #13 had not finished when the report was
  written; the commit only adds this file.
- The timings come from one Windows machine.

## Suggestions

- User code calling `process.stdout.write` directly would still corrupt the
  protocol stream; only the console is guarded. A later branch could give the
  writer a private handle to stdout and redirect `process.stdout.write` to
  stderr.
- Review 0003's note 9 (cross-check each flow once per `validate`) is still open;
  not yet measurable on the demo app.
