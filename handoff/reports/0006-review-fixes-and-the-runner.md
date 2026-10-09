# Report 0006: review fixes and the runner

- Date: 2026-10-10
- Written by: implementer
- Instruction: [0006](../instructions/0006-review-fixes-and-the-runner.md)

## Summary

The findings of review 0005 are fixed: each has a test that fails without its
fix, except finding 5. Finding 5 was a documentation change, and a browser
test now holds the rule it documents.

Plan branches 8 and 9 exist, and a test now runs:

- `startRun` validates the selected files, starts Chromium and runs each test
  instance (one per data row) in a fresh context;
- it reports every step over the event stream and writes the run folder of
  ADR 0015;
- `listTests` answers the desktop app.

Branch 8 passed 1,500 changed lines, so it is split in two stacked branches.
Checks S6, S10 to S15 and F1, F2, F3, F7 pass in Chromium, and so do I8, I9
and I10.

One part was not built as far as the instruction might expect. CSV is read
only as far as step-format.md defines it, because the document does not cover
quoting. A file with a double quote is rejected, and the owner is asked below
whether to adopt RFC 4180.

## Branches and pull requests

| Branch                     | Based on                   | Last commit | Pull request | Pushed | Changed lines\* | CI (`verify`, `integration`)   |
| -------------------------- | -------------------------- | ----------- | ------------ | ------ | --------------- | ------------------------------ |
| `fix/review-0005-findings` | `main`                     | `3a4ab9f`   | #19          | yes    | 269             | green on Linux, Windows, macOS |
| `feat/runner-core`         | `fix/review-0005-findings` | `eb3d63a`   | #21          | yes    | 1,877           | green on Linux, Windows, macOS |
| `feat/runner-actions`      | `feat/runner-core`         | `3752ed7`   | #23          | yes    | 772             | green on Linux, Windows, macOS |
| `feat/runner-rows-results` | `feat/runner-actions`      | this report | #25          | yes    | 1,210 + report  | green on Linux, Windows, macOS |

\* Insertions plus deletions from `git diff --shortstat` against the branch
below, without the lockfile and fixtures. No JSON Schema file changed, and no
dependency was added.

**The split.** `feat/runner-core` reached about 2,490 changed lines, so I cut
it as the instruction suggests:

- `feat/runner-core` holds the runner, its events, `ctx`, the browser and the
  stdio guard;
- `feat/runner-actions` (stacked) holds the four built-ins with the samples and
  the browser tests that need them.

The first part is still 1,877 lines; about 650 of them are tests, which I kept
with the code they test. Merge order: #19, #21, #23, #25. Later fixes were
committed on the branch that owns the code and merged up with merge commits;
nothing pushed was rewritten.

**A macOS-only engine bug, found by CI.** The integration job failed on
macOS for #21, #23 and #25. The engine reported no browser there, so every
`startRun` was refused.

- **The cause:** to check for Chromium without starting it, the engine walks
  from Playwright's expected executable to the `chromium-<revision>` folder
  and looks for the headless shell beside it. It assumed the executable lies
  two folders down, which is true on Windows and Linux. On macOS it lies
  inside `Chromium.app`.
- **The fix:** `eb3d63a` on `feat/runner-core` walks up to that folder, with unit
  tests for all three layouts (`runner/browser.test.ts`).
- **How it showed:** the cleanup tests failed by waiting for a step that could
  never start, and I first misread that as a problem in the test. On the way I
  changed the cleanup tests to find the browser's processes by process id
  below the engine, instead of by a marker in the engine's `TMPDIR`. I kept
  that change: it no longer alters the engine's environment. A refused
  `startRun` now fails those tests at once, with the reason.

## Tasks

| Task | State  | Notes                                                                                                                                                                                                                                                |
| ---- | ------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | done   | Local branches contained in `main` deleted.                                                                                                                                                                                                          |
| 2    | done   | `paramsSchema` is kept. The property test is described below. ADR 0014 updated.                                                                                                                                                                      |
| 3    | done   | A log event's `data` is walked (`params.data`).                                                                                                                                                                                                      |
| 4    | done   | `locate` makes one attempt with every candidate before giving up. ADR 0010 updated.                                                                                                                                                                  |
| 5    | done   | `InvalidSelector`, failing at once. Added to architecture.md, actions.md and protocol.md, which now has a "Step error codes" table.                                                                                                                  |
| 6    | done   | step-format.md ("Candidates") and ADR 0010.                                                                                                                                                                                                          |
| 7    | done   | `CLAUDE.md` and `README.md`. Nothing under `apps/desktop/` changed.                                                                                                                                                                                  |
| 8    | done   | `rpc/stdio-guard.ts`. Tests at load time and inside `run` are in `errors.integration.test.ts` (on `feat/runner-actions`, since they run a test).                                                                                                     |
| 9    | done   | `capabilities.browsers`, the install command, one browser per run, a context per instance with viewport, locale and timezone (plan row updated). `cleanup.integration.test.ts` covers shutdown, closed stdin, an internal error and a killed engine. |
| 10   | done   | `runner/context.ts`. The runner implements `LocatorReporter`.                                                                                                                                                                                        |
| 11   | done   | `runner/run.ts`.                                                                                                                                                                                                                                     |
| 12   | done   | Every event in the tests is checked against its JSON Schema file.                                                                                                                                                                                    |
| 13   | done   | `runner/test-run.ts`.                                                                                                                                                                                                                                |
| 14   | done   | `runner/errors.ts`. The mapping of `candidates` is described below.                                                                                                                                                                                  |
| 15   | done   | `actions/builtins/index.ts`. Other built-ins fail their step with `NotImplemented` (below).                                                                                                                                                          |
| 16   | done   | `samples.integration.test.ts`, 13 tests. What `startRun` did with rows, skips and user actions in branch 8 is described below.                                                                                                                       |
| 17   | partly | Inline, CSV and YAML rows work. CSV quoting is not defined, so it is refused (question 1).                                                                                                                                                           |
| 18   | done   | One `testSkipped` per row.                                                                                                                                                                                                                           |
| 19   | done   | S13: `demo.addTodo` now really adds the item.                                                                                                                                                                                                        |
| 20   | done   | `runner/run-folder.ts`.                                                                                                                                                                                                                              |
| 21   | done   | `runner/keep-runs.ts`.                                                                                                                                                                                                                               |
| 22   | done   | `Project.listTests`.                                                                                                                                                                                                                                 |
| 23   | done   | `rows-results.integration.test.ts`: S6, S13, S15, I8, I9, I10.                                                                                                                                                                                       |

### The test for each finding

| Finding  | Test                                                                                                                                                                                                                             |
| -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1        | `rpc/masking.property.test.ts`: "with a secret taken from each string inside the schemas". It uses the real `listActions` result for every built-in, and every string of 4 or more characters in the schemas, `string` included. |
| 2        | `locate/locate.integration.test.ts`: "tries every candidate before giving up when the timeout is shorter than the grace period" (400 ms timeout, 1 s grace, found by candidate 1, with the warning).                             |
| 3        | `locate/locate.integration.test.ts`: "fails at once with InvalidSelector for a candidate Playwright rejects". A closed page is not mistaken for it; a third test covers that.                                                    |
| 4        | `protocol/src/masking-rules.test.ts`: "walks a log event's data: keeps known identifiers, masks every other field" (a `LocatorFallback` warning).                                                                                |
| 5        | Documentation only, so no test can fail without it. `locate/locate.integration.test.ts`: "role candidates do not see hidden elements; other kinds count them" holds the documented rule.                                         |
| 6 (note) | `runner/errors.integration.test.ts`: "sends writes to stdout to stderr, masked, at load time and inside run".                                                                                                                    |
| 7 (note) | `runner/errors.integration.test.ts`: "a page closed during a step is PageClosed (review 0005, finding 7)".                                                                                                                       |

I ran the tests for findings 1 to 4 against the old code; each failed.

## Checks

| Command                                             | Result                                                                                                                                               |
| --------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `pnpm verify`, `fix/review-0005-findings`           | passed, 360 tests                                                                                                                                    |
| `pnpm verify`, `feat/runner-core`                   | passed, 371 tests                                                                                                                                    |
| `pnpm verify`, `feat/runner-actions`                | passed, 371 tests                                                                                                                                    |
| `pnpm verify`, `feat/runner-rows-results`           | passed, 382 tests                                                                                                                                    |
| `pnpm test:integration`, `feat/runner-core`         | passed, 26 tests                                                                                                                                     |
| `pnpm test:integration`, `feat/runner-actions`      | passed, 45 tests                                                                                                                                     |
| `pnpm test:integration`, `feat/runner-rows-results` | passed, 52 tests                                                                                                                                     |
| No browser left behind                              | `cleanup.integration.test.ts`. The harness and every test close the engine, the server and the browser in `afterAll`/`finally`.                      |
| `pnpm generate:schemas`                             | no change; `PROTOCOL_VERSION` is still `0.1.0`                                                                                                       |
| CI                                                  | Green on Linux, Windows and macOS for all four pull requests, both jobs. The `verify` job installs no browser, so it shows `pnpm verify` needs none. |

### The `integration` job now

Whole job, including install, on the last CI run of #25 (52 browser tests):

| System  | `feat/runner-rows-results` (#25) |
| ------- | -------------------------------- |
| Linux   | 55 s                             |
| Windows | 1 min 48 s                       |
| macOS   | 1 min 11 s                       |

### S11 exactly as a client receives it

One line per message on stdout, from a run on this machine. The first line is
the answer to `startRun`.

```
{"jsonrpc":"2.0","id":50,"result":{"runId":"20261009-204936-9a07","resultsDir":"<temporary copy of examples/demo-app>/.cfe/runs/20261009-204936-9a07"}}
{"jsonrpc":"2.0","method":"runStarted","params":{"runId":"20261009-204936-9a07","seq":1,"env":"local","browser":"chromium","settings":{"viewport":{"width":1280,"height":720},"locale":"en-US","timezone":"UTC"},"startedAt":"2026-10-09T20:49:36.282Z","tests":[{"testId":"tests/locator-fallback.test.yaml#0","file":"tests/locator-fallback.test.yaml","name":"A renamed button is found by its test ID"}]}}
{"jsonrpc":"2.0","method":"testStarted","params":{"runId":"20261009-204936-9a07","seq":2,"testId":"tests/locator-fallback.test.yaml#0","startedAt":"2026-10-09T20:49:36.403Z"}}
{"jsonrpc":"2.0","method":"stepStarted","params":{"runId":"20261009-204936-9a07","seq":3,"testId":"tests/locator-fallback.test.yaml#0","stepId":"steps.0","section":"steps","action":"goto","params":{"url":"/fallback"},"page":"main","title":"goto /fallback","location":{"file":"tests/locator-fallback.test.yaml","line":6,"column":5}}}
{"jsonrpc":"2.0","method":"stepPassed","params":{"runId":"20261009-204936-9a07","seq":4,"testId":"tests/locator-fallback.test.yaml#0","stepId":"steps.0","durationMs":27,"locators":[],"snapshot":{"state":"skipped"}}}
{"jsonrpc":"2.0","method":"stepStarted","params":{"runId":"20261009-204936-9a07","seq":5,"testId":"tests/locator-fallback.test.yaml#0","stepId":"steps.1","section":"steps","action":"click","params":{"target":"order.placeOrder"},"page":"main","title":"click order.placeOrder","location":{"file":"tests/locator-fallback.test.yaml","line":7,"column":5}}}
{"jsonrpc":"2.0","method":"log","params":{"runId":"20261009-204936-9a07","seq":6,"level":"warn","code":"LocatorFallback","message":"Target \"order.placeOrder\" was found by its candidate 1 (counting from 0), not its first; the first candidate may be out of date.","testId":"tests/locator-fallback.test.yaml#0","stepId":"steps.1","location":{"file":"tests/locator-fallback.test.yaml","line":7,"column":5},"data":{"target":"order.placeOrder","candidateIndex":1}}}
{"jsonrpc":"2.0","method":"stepPassed","params":{"runId":"20261009-204936-9a07","seq":7,"testId":"tests/locator-fallback.test.yaml#0","stepId":"steps.1","durationMs":1051,"locators":[{"param":"target","target":"order.placeOrder","candidateIndex":1,"candidate":{"testId":"place-order"}}],"snapshot":{"state":"skipped"}}}
{"jsonrpc":"2.0","method":"stepStarted","params":{"runId":"20261009-204936-9a07","seq":8,"testId":"tests/locator-fallback.test.yaml#0","stepId":"steps.2","section":"steps","action":"expect.text","params":{"target":"order.status","equals":"Order placed"},"page":"main","title":"expect.text order.status","location":{"file":"tests/locator-fallback.test.yaml","line":8,"column":5}}}
{"jsonrpc":"2.0","method":"stepPassed","params":{"runId":"20261009-204936-9a07","seq":9,"testId":"tests/locator-fallback.test.yaml#0","stepId":"steps.2","durationMs":6,"locators":[{"param":"target","target":"order.status","candidateIndex":0,"candidate":{"testId":"order-status"}}],"snapshot":{"state":"skipped"}}}
{"jsonrpc":"2.0","method":"testFinished","params":{"runId":"20261009-204936-9a07","seq":10,"testId":"tests/locator-fallback.test.yaml#0","status":"passed","durationMs":1241}}
{"jsonrpc":"2.0","method":"runFinished","params":{"runId":"20261009-204936-9a07","seq":11,"status":"passed","durationMs":1410,"totals":{"passed":1,"failed":0,"cancelled":0,"skipped":0}}}
```

### What one step costs

The time from `stepStarted` to `stepPassed` for a `click` on the demo app,
measured on the client side over 20 clicks (`click todos.add` and
`click todos.clearAll` in S10, 10 runs) on this Windows machine:

- median 49 ms;
- range 35 to 64 ms;
- the engine's own `durationMs` median is 48 ms.

That includes locating the target with its first candidate. ADR 0010's revisit
threshold of 200 ms overhead per step is far off.

## How `ErrorInfo.candidates` is filled

`candidates` is a flat list of `{ candidate, matches }`, where `matches` must
be a number. It has no place for the level of a target (frame, `within`,
element) or for a candidate that was never tried. I mapped it like this:

- **The list** holds the candidates of the level where the search stopped: the
  outermost level whose candidates found nothing. When a `within` target is
  missing, that is the `within` target's candidates, not the element's, which
  were never searched.
- **Every candidate at that level has a count.** Since finding 2, each call
  makes at least one attempt with every candidate.
- **The message names every level** with its last counts, `not tried` included.
- `stepFailed.locators` still carries the nested `LocatorUse`, with `null` for
  the levels that failed.

**Proposed protocol change, not made:** give each entry an optional `level`
(`"element"`, `"frame"`, `"within"`, with a nesting path), and allow
`matches: null` for "not tried". Both are compatible additions under ADR 0011
(a new optional field, and a widened type a client already reads as a number
or absent). Then a client could show the same tree as the message.

## What `startRun` did in branch 8 with rows, skips and user actions

On `feat/runner-core` and `feat/runner-actions`:

- **Tests with `data`** made `startRun` fail with invalid params, listing them:
  "Tests with data rows cannot run in this engine version yet: …". Branch 9
  removes this.
- **Tests with `skip`** sent one `testSkipped` with the reason. That is the
  final behaviour for tests without rows.
- **Steps that call a user action** ran. See "Departures".

## Departures from the instruction

- **User actions already run in branch 8.** The runner calls whatever `run` the
  registry holds, so user actions ran from the first part on. Blocking them
  would have meant writing code only to delete it in branch 9. Branch 9 adds
  S13 as asked.
- **Task 8's tests live on `feat/runner-actions`.** Writing "inside `run`"
  needs a run in Chromium, so they sit with the other browser tests.
- **CSV (task 17):** see Summary and question 1. I stopped where the definition
  stops.
- **`ErrorInfo` and `Step error codes`:** the new table in `docs/protocol.md`
  lists the codes in 0.1.0. That is a documentation addition, not a protocol
  change.

## Decisions I made

1. **"Reported as a setup failure"** (step-format.md, rule 5) is defined nowhere
   else. The failing step's `stepStarted` already has `section: before`. The
   steps skipped after it say "skipped: the test's setup failed at before.N"
   instead of "skipped: steps.N failed".
2. **The generated step `title`** is the step's `name`, or else the action plus
   its URL, target name or first value (`click todos.add`, `goto /todos`). An
   inline target gives just the action.
3. **`seq` starts at 1** for `runStarted`.
4. **A failing `after` step makes the test fail.** The documents say it is
   "reported" but not whether it changes the test's status. An `after` step
   skipped for `variableNotSet` does not fail the test (F7).
5. **A step whose signal was aborted never passes**, even if its action returns
   normally on abort. It fails with `ActionTimeout` or `Cancelled`. The cleanup
   tests found this.
6. **Margins before the deadline.** `ctx.locate` gets the step's remaining time
   minus 150 ms, and `expect.text` keeps 150 ms back. A missing target then
   reports `TargetNotFound` with its candidates, and a failed assertion reports
   `AssertionFailed` with the actual text, rather than both being
   `ActionTimeout`.
7. **`expect.text`** compares `textContent` with whitespace collapsed and
   trimmed, like Playwright's `toHaveText`, and retries every 100 ms.
8. **A built-in without an implementation** fails its step with
   `NotImplemented`, naming the action and listing the built-ins that can run.
   The test goes on to its `after` steps. Named pages and `opens` fail the same
   way until branch 10.
9. **Is Chromium installed?** The engine reads the files Playwright installs
   (the full browser, or the headless shell's `INSTALLATION_COMPLETE`), without
   starting a browser, so `initialize` stays fast. A headed run with only the
   headless shell installed fails to launch, and the run reports it as a
   `BrowserError` log.
10. **`files` and `tags` together:** the files are filtered by the tags.
    Without `files`, all tests of the `tests` globs are filtered by the tags.
    A non-test file in `files` is the diagnostic `NotATestFile`.
11. **`openProject` during a run** is refused with `RunInProgress`.
12. **What `run.json` and `test.json` hold.** ADR 0015 lists their contents
    but not their shape, so both are built from the masked events. `run.json`
    holds versions, env, browser, settings, times, status, totals, and the
    tests with their status and folder. `test.json` holds the test's fields,
    status, steps (as their events merged) and logs.
13. **A test name with `${row.…}`** is filled in per row in `runStarted.tests`.
    `listTests` returns it as written, as the protocol example shows.
14. **`listTests`** lists every file of the `tests` globs, even one too broken
    to read (then named after its file, with `rows: 1`).
15. **A CSV value has spaces trimmed, and blank lines are skipped.** The
    document says neither; I chose the reading that matches what people write.
16. **The parameter name in `LocatorUse`** is found by comparing the target
    passed to `ctx.locate` with the step's parameters by identity. It is
    `target` when none matches, for example in a user action that builds its
    own target.

### Where the documents were unclear or silent

- "Setup failure", the step `title`, where `seq` starts, and whether a failing
  `after` step fails the test: decisions 1 to 4.
- **CSV:** only "first line is the header" (question 1).
- **ADR 0015's run ids** sort by time only to the second. Two runs in the same
  second are ordered by their random suffix, so `keepRuns` may delete the
  newer of the two. The I9 test keeps its runs a second apart. A millisecond
  in the run id, or sorting by folder creation time, would fix it; either
  needs a change to ADR 0015.
- **ADR 0015** gives the contents of `run.json` and `test.json`, not their
  shape: decision 12.

## Questions for the owner

1. **CSV data files: adopt RFC 4180?** step-format.md defines CSV only as
   "first line is the header". Real files need quoted values: commas, quotes or
   line breaks inside a value.
   - **(a) Adopt RFC 4180 (recommended).** Double quotes around a value,
     doubled quotes inside it, line breaks allowed inside quotes. It is what
     spreadsheets write, and about 40 lines of reader with no dependency.
   - **(b) Keep the current rule.** No quoting, and a file with a double quote
     fails validation (`DataFileInvalid`) with a pointer to YAML data files.
   - (c) Add a dependency for CSV.
2. **The `ErrorInfo.candidates` change proposed above:** an optional `level`
   and `matches: null`. Recommended for 0.1.0, before the desktop app reads the
   field.
3. **Hidden elements** (still open from review 0005). The rule is now written
   down and tested; nothing changed.

## Not done, not pushed, not verified

- **The `RunCleanupFailed` warning.** The path where a folder cannot be deleted
  is unit-tested (`keep-runs.test.ts`), and the warning's event is emitted by
  the runner. No integration test locks a folder to trigger it: that cannot be
  done the same way on all three systems.
- **A user action that ignores `ctx.signal`.** It keeps running after its step
  has failed with `ActionTimeout`. Closing its page and `StrayActionCode` are
  branch 10 (F8).
- `pnpm verify` was not run on this machine with Chromium uninstalled. CI's
  `verify` job has no browser and passes.

## Suggestions

- Put milliseconds in the run id (`20261009-054902-123-1a2b`), so runs sort
  exactly. This changes ADR 0015 and the documented format.
- Give `docs/protocol.md` a short section on what the run folder's JSON files
  hold, now that the desktop app will read them.
