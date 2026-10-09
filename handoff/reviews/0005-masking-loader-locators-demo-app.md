# Review 0005: masking and loader fixes, locators, the demo app

- Date: 2026-10-09
- Written by: reviewer
- Instruction: [0005](../instructions/0005-masking-loader-locators-demo-app.md)
- Report: `handoff/reports/0005-masking-loader-locators-demo-app.md` on the
  branch `test/demo-app-harness` (it reaches `main` when pull request #17 is
  merged)
- Verdict: **Approved**, all four pull requests

## Pull requests

| Pull request                                           | Branch                   | Reviewed at | CI                                                        | Verdict  |
| ------------------------------------------------------ | ------------------------ | ----------- | --------------------------------------------------------- | -------- |
| [#14](https://github.com/starscream000/coffee/pull/14) | `fix/review-0004-loader` | `a619ca9`   | `verify` green on Linux, Windows and macOS                | Approved |
| [#13](https://github.com/starscream000/coffee/pull/13) | `feat/context-secrets`   | `4b222a6`   | `verify` green on Linux, Windows and macOS                | Approved |
| [#15](https://github.com/starscream000/coffee/pull/15) | `feat/locators`          | `4b6e0d2`   | `verify` and `integration` green on Linux, Windows, macOS | Approved |
| [#17](https://github.com/starscream000/coffee/pull/17) | `test/demo-app-harness`  | `535659b`   | `verify` and `integration` green on Linux, Windows, macOS | Approved |

Merge in this order: #14, #13, #15, #17.

## What I checked

- **`pnpm verify` from a clean build** on the top of the stack (`535659b`), on
  Linux with Node 24.11.0: 357 tests in 31 files, all passing.
- **`pnpm test:integration`** on the same commit: 18 tests in 2 files, all
  passing. See "What I could not check" for the browser I used.
- **CI** on all four pull requests, on all three systems, both jobs.
- **Finding 1 of review 0004, with my own probes** through a real engine
  process and the demo app. With the secret set to `jsonrpc`, `result`, `line`
  and `2.0","`, every message is valid JSON with its keys and identifiers
  unchanged; with `line`, only the free text is masked ("Add it as the first
  •••."). I also tried `error`, `tests` and `local` (a severity, a folder and an
  environment name): all kept.
- **Findings 2, 3 and 7, with my own project**: an action that imports a
  CommonJS package calling `require("path")` loads; after editing a helper
  file, a second `openProject` in the same engine uses the new code; the cache
  folder holds one bundle after several edits. A package that throws while
  loading and an action file that throws at its top level each get the hint
  that fits.
- **Finding 4**: an action file that logs a secret with `console.log`,
  `console.error` and `console.table` while loading shows `•••` on stderr.
- **The source, line by line**: the masking rules, the message writer, the
  console replacement, the changes to the stdio server, the loader, both files
  of `packages/engine/src/locate/`, the harness and the demo server. Of the
  tests I read the two that guard the masking rules
  (`masking-rules.test.ts`, `masking.property.test.ts`); the others I relied on
  passing.
- **`ctx.locate`, with my own probes** in a real browser: twelve cases beyond
  the implementer's tests (short timeouts, an invalid selector, a page closed
  or replaced during the wait, hidden duplicates, a test ID containing a
  quote, an unknown role). Findings 2, 3 and 5 come from these.
- **ADR 0014** says what the code does, including what is given up.

## What I could not check

- **The browser tests against the pinned Chromium.** My workspace has an older
  Chromium (revision 1194) than Playwright 1.64.0 expects (1248) and cannot
  download another, so I pointed Playwright at the older one. All 18 tests
  passed there. CI ran them against the right browser on three systems; that
  is the result that counts.
- That each fix's test fails without the fix. The report says the implementer
  checked each one that way; I confirmed the fixed behaviour only.
- The download sizes and job times in the report.
- Behaviour on Windows and macOS beyond CI.

## Findings

None of these can be reached from `main` in a way that breaks a message or
leaks a secret, so none holds a pull request. All go into instruction 0006.

1. **Should fix. A secret that equals a word of an action's parameter schema
   damages that schema** (`packages/protocol/src/masking.ts`, the rule
   `paramsSchema: 'mask'`). With the secret set to `string`, `listActions`
   returns `"type":"•••"` in 24 of the 26 actions of the demo app. With
   `value`, `fill` lists `•••` as a required parameter and `extract` has `•••`
   as an allowed value. The message still matches the protocol, but a client
   cannot use the schema, and the desktop app reads the parameters from it. A
   parameter schema comes from action code in the repository, like an
   identifier, so it should be kept. Weak test passwords make this more likely
   than it sounds: a secret `password` would damage the schema of any action
   with a parameter of that name.
2. **Should fix. A step timeout shorter than `fallbackGrace` never tries the
   fallbacks** (`packages/engine/src/locate/locate.ts`, the loop in `locate`).
   With a 400 ms timeout and the default 1 s grace, a target whose second
   candidate matches fails with "1: testId="pay" → not tried"; with a 2 s
   timeout the same call passes. Before giving up, `locate` must make one
   attempt with every candidate allowed.
3. **Should fix. An invalid selector leaves `locate` as a raw Playwright
   error.** The candidate `css: 'div[['` fails at once with "locator.count:
   Unexpected token …", with no target name and no step. It should be a
   `LocateError` that names the target and the candidate, without retrying.
4. **Note. The same holds for an identifier inside a log event's `data`.**
   `data` is masked as a whole, so the target name in a `LocatorFallback`
   warning is masked when a secret equals part of it. Walk `data` instead:
   known identifiers are kept and every unknown field is still masked.
5. **Note. Candidate kinds do not agree on hidden elements.** Playwright's
   `getByRole` ignores hidden elements; test ID, CSS, text, label and
   placeholder candidates count them. A hidden duplicate therefore skips a
   test ID candidate (2 elements) but not a role candidate. This is
   Playwright's behaviour, not a defect, but the documents must say it. See the
   owner decision below.
6. **Note. User code can still write to stderr unmasked** with
   `process.stderr.write`. The console is covered, as the instruction asked.
   Close this together with the stdout guard that review 0004 moved to the
   runner branch.
7. **Note. A page closed during `locate`** surfaces as Playwright's
   `TargetClosedError`. The runner must turn it into a step error a tester can
   read.
8. **Note. Load errors point at line 1.** Source maps are now on, so an
   `ActionLoadError` could point at the line that threw. Not asked for; left as
   a suggestion.

## Rulings on the report's "Decisions I made"

| No. | Decision                                                      | Ruling                                  |
| --- | ------------------------------------------------------------- | --------------------------------------- |
| 1   | "Matches" counts hidden elements                              | **Owner decides.** See below.           |
| 2   | `testId` uses an attribute selector, not `getByTestId`        | Accepted. A quote in the value works.   |
| 3   | A frame target must match an `<iframe>` or `<frame>`          | Accepted.                               |
| 4   | `locate` takes an optional `param`                            | Accepted.                               |
| 5   | Polling every 50 ms, waking at the end of the grace period    | Accepted.                               |
| 6   | On abort, `locate` throws the signal's reason                 | Accepted.                               |
| 7   | On `TargetNotFound`, outer levels keep the candidate found    | Accepted.                               |
| 8   | An unresolvable `${…}` in a candidate fails at once           | Accepted.                               |
| 9   | `reason` and `name` are kept; `snapshot.reason` is masked     | Accepted.                               |
| 10  | The demo server is TypeScript run directly by the pinned Node | Accepted; it relies on the pinned Node. |

The departures are accepted: #13 stays one branch (splitting it would rewrite
pushed history), `maskValue` is removed, a list position that does not exist is
`PathNotFound`, the console test waits for stderr, the Vitest projects are
defined inline, and the plan row for branch 7 says "child process".

The report's suggestion to merge #15 early for the browser cache is taken care
of by merging all four now.

## Done-when checks

| Check                                                           | Result                                                  |
| --------------------------------------------------------------- | ------------------------------------------------------- |
| `pnpm verify` passes on every branch, with no browser installed | Yes (top of stack by reviewer; each by CI)              |
| `pnpm test:integration` passes on the last two branches         | Yes (CI; and by reviewer on the top, older Chromium)    |
| Every pull request open, CI green on three systems, both jobs   | Yes                                                     |
| Each finding of review 0004 has a test that fails without it    | Yes per the report; fixes confirmed by my probes        |
| The masking property test and the four named cases pass         | Yes                                                     |
| Committed JSON Schema files match the generator                 | Yes (test)                                              |
| Headers, TSDoc, no `any`, suppressions listed                   | Yes; I found no suppression in the changed code         |
| Only `playwright` added, at an exact version                    | Yes (1.64.0)                                            |
| No client imports engine code                                   | Yes (lint)                                              |
| `CHANGELOG.md` updated in each branch                           | Yes                                                     |
| The report exists and follows the template                      | Yes. It says plainly what was not measured or verified. |

## Owner decisions needed

1. **Should hidden elements count when `ctx.locate` decides whether a
   candidate matches exactly one element?**
   - **(a) Count every attached element** (implemented). The element found is
     then the one a click acts on, and actions that wait for an element to
     appear or disappear can still find it while it is hidden. A page with a
     hidden copy of a button needs `nth` or `within` in the target; the error
     message shows the count.
   - (b) Count visible elements only. Friendlier to pages with hidden copies,
     but an element that is hidden could no longer be located at all, which
     `wait.element` and `expect.visible: false` need.

   Recommended: **a**. Until the owner says otherwise it stays as implemented,
   and instruction 0006 writes the rule, with finding 5, into the documents.

2. Still open from review 0004: how to write a regular expression for a URL
   (`/regex/` or the prefix `regex:`). Needed before plan branch 12.

## For the next instruction

Instruction 0006:

- A fix branch from `main` for findings 1 to 5.
- The root documents learn about the desktop track (request R0002 of the
  desktop track).
- Plan branch 8 (`feat/runner-core`), with the stdout and stderr guard
  (finding 6) and finding 7.
- Plan branch 9 (`feat/runner-rows-results`), which brings `listTests`
  (request R0003 of the desktop track).
