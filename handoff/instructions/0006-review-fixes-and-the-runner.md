# Instruction 0006: review fixes and the runner

- Date: 2026-10-09
- Written by: reviewer
- Based on `main` at: `a30b459` or later
- Replaces: none
- Follows review: [0005](../reviews/0005-masking-loader-locators-demo-app.md)

## Goal

The findings of review 0005 are fixed, and plan branches 8 and 9 exist: the
engine runs a test file against the demo app in a real Chromium and reports it
through the event stream, with data rows, skipped tests, user actions, the run
folder and `listTests`. From this instruction on, a test actually runs.

## Owner decisions

1. Pull requests #14, #13, #15 and #17 are merged. Plan branches 1 to 7 are on
   `main`.
2. **A second implementer now works on the desktop app**, in `apps/desktop/`,
   with its own handoff folder (`apps/desktop/handoff/`). Do not change any
   file under `apps/desktop/`, not even in a repository-wide rename or format
   run. Its instructions (`D0001`, …) are not yours.
3. **Not decided:** whether hidden elements count when `ctx.locate` looks for
   exactly one element. It stays as implemented (they count), and task 6
   writes that down. If the owner decides otherwise, a later instruction
   changes it.
4. **Not decided:** how a regular expression for a URL is written. Leave the
   current behaviour and its documents as they are.

## Branches

| Branch                     | Based on                   | Plan branch | What it holds          |
| -------------------------- | -------------------------- | ----------- | ---------------------- |
| `fix/review-0005-findings` | `main`                     | none        | Tasks 2 to 7           |
| `feat/runner-core`         | `fix/review-0005-findings` | 8           | Tasks 8 to 16          |
| `feat/runner-rows-results` | `feat/runner-core`         | 9           | Tasks 17 to 23, report |

Open one pull request per branch, all into `main`, and say in each description
which branch it sits on.

If a `feat/` branch passes roughly 1,500 changed lines (not counting fixtures,
generated files and the lockfile), split it in two stacked branches and say so
in the report. `feat/runner-core` is the likeliest to need it; a good cut is
the runner and its events first, then the four actions with the samples.

`docs/` is the specification. Where this instruction and a document under
`docs/` disagree, stop and say so in the report. If plan branch 8 cannot be
finished, stop there and report; do not start branch 9.

## Tasks

### Part A: housekeeping (no files change)

1. `git switch main` and `git pull --ff-only`. Delete the local branches that
   are now contained in `main`.

### Part B: fixes (`fix/review-0005-findings`)

The finding numbers are those of review 0005. Each fix comes with a test that
fails without it.

2. **Finding 1.** An action's `paramsSchema` is never changed by masking.
   Extend the property test: for the real `listActions` result with every
   built-in action, and a secret taken in turn from every string of 4
   characters or more inside the schemas, each `paramsSchema` in the masked
   message equals the original. Add the rule and its reason to ADR 0014.
3. **Finding 4.** The `data` of a `log` event is walked, not masked as a whole:
   the fields the masking table knows as identifiers are kept (`target`,
   `candidateIndex`), and every field it does not know is masked. Test both
   with a `LocatorFallback` warning.
4. **Finding 2.** Before `ctx.locate` gives up, it has made at least one
   attempt with every candidate allowed, also when the step's timeout is
   shorter than `fallbackGrace`. Browser test: timeout 400 ms, grace 1 s, the
   second candidate matches; the target is found by candidate 1, with the
   warning. Say in ADR 0010 what happens when the timeout is the shorter one.
5. **Finding 3.** A candidate that Playwright rejects as a selector fails at
   once, without retrying, with a typed error that names the target, the
   candidate and Playwright's reason. Give it a code of its own and add it to
   the documents that list step error codes.
6. **Finding 5.** Write the matching rule into `docs/step-format.md`
   ("Targets") and ADR 0010: a candidate matches when exactly one attached
   element fits, hidden elements included; role candidates never see hidden
   elements, as in Playwright; a page with a hidden copy needs `nth` or
   `within`.
7. **The desktop track in the root documents** (request R0002 of the desktop
   track):
   - `CLAUDE.md`, "Repository layout": the `apps/desktop` line becomes
     "Avalonia app; its own implementer and handoff folder
     (`apps/desktop/handoff/`)".
   - `CLAUDE.md`, "How to work": add "Do not change files under
     `apps/desktop/`; that folder has its own implementer. Ask through the
     report instead."
   - `CLAUDE.md`, "Git rules": the reviewer's exception also covers handoff
     files under `apps/desktop/handoff/`.
   - `README.md`, layout table: `apps/desktop` is "Avalonia desktop app (in
     progress, see `apps/desktop/README.md`)".

### Part C: the runner core (`feat/runner-core`, plan branch 8)

The specification is `docs/architecture.md` ("Lifecycle of a run",
"Cancellation and timeouts", "Errors"), `docs/protocol.md` (`startRun`,
"Events", "Identifiers", "Shared types"), `docs/step-format.md` ("Test files",
"Steps", "Execution rules") and `docs/actions.md` ("The context", "Built-in
actions").

8. **stdout and stderr belong to the engine** (review 0004's accepted
   suggestion, and finding 6). The message writer keeps a private handle to
   stdout; anything else written to stdout, by any code, goes to stderr
   instead. Everything written to stderr passes through `mask`, including
   direct `process.stderr.write` calls. Test both with a user action that
   calls `process.stdout.write` and `process.stderr.write` with a registered
   secret, at load time and inside `run`.
9. **The browser.** `initialize` reports `capabilities.browsers` as
   `["chromium"]` when Playwright's Chromium is installed, and as an empty
   list when it is not. `startRun` without an installed browser fails with a
   message that gives the install command. One browser per run, started when
   the run starts, headless unless `options.headed`; a fresh browser context
   per test instance, created with the configured viewport, locale and
   timezone (their checks stay in branch 10; update the plan row to say the
   settings are applied here). When the engine exits during a run, for any
   reason, the browser is closed first: no browser process may be left behind.
   Test that.
10. **`ctx`.** Build the object `docs/actions.md` describes: `page`,
    `request`, `vars`, `env`, `secrets`, `log`, `locate`, `signal`.
    `ctx.log` becomes `log` events. `ctx.locate` calls `locate` with the
    step's remaining time; the runner implements `LocatorReporter`, so each
    use lands in the step result's `locators` and each fallback is a `log`
    event with level `warn`, code `LocatorFallback`, the step's `location` and
    its `data`.
11. **`startRun`.** As `docs/protocol.md` says: every selected file is
    validated first (`StepFilesInvalid` with the diagnostics, nothing runs);
    one run at a time (`RunInProgress`); an unknown browser is `-32602`; the
    answer `{ runId, resultsDir }` is sent before the run starts. Tests are
    selected by `files` or by `tags`. Create the run's folder; its files come
    in branch 9.
12. **The event stream.** `runStarted`, `testStarted`, `stepStarted`,
    `stepPassed`, `stepFailed`, `stepSkipped`, `log`, `testFinished` and
    `runFinished`, with `seq` rising by one, the identifiers of "Identifiers",
    and `stepStarted.params` in the canonical long form with nothing
    interpolated. `snapshot` is `{ "state": "skipped" }` until branch 14.
    Every event the tests receive is checked against its JSON Schema.
13. **The rules of a test.** `before`, then `steps`; the first failing step
    stops the test and the rest are `stepSkipped` with `previousFailure`.
    `after` steps always run; a failing `after` step is reported and the
    others still run. An `after` step that uses a variable that was never set
    is `stepSkipped` with `variableNotSet`. A failure in `before` is reported
    as a setup failure. Each step has a timeout (its own or the default);
    when it passes, `ctx.signal` is aborted and the step fails with
    `ActionTimeout`. Playwright calls get at most the step's remaining time.
14. **Errors a tester can read.** Every failure reaches the client as an
    `ErrorInfo` with a stable code, a message, the step's location and, for
    assertions, `expected` and `actual`. Cover at least: `TargetNotFound` with
    its candidates, the new code of task 5, an action that throws something
    that is not an SDK error, and a page that is closed during a step (review
    0005, finding 7). `ErrorInfo.candidates` has no place for the levels of a
    target or for a candidate that was never tried; say in the report how you
    mapped them, and propose a protocol change if you think one is needed. Do
    not make it.
15. **Four built-in actions**, through the same `defineAction` mechanism as
    user actions: `goto`, `click`, `fill`, `expect.text`, each as
    `docs/actions.md` describes it and each honouring `ctx.signal`. A step
    that calls a built-in action without an implementation yet must not crash
    the engine; choose the simplest clear behaviour and report it.
16. **Samples and tests.** Add the demo step files of checks S10, S11, S12 and
    S14 and the fixtures of F1, F2, F3 and F7, with the file names the
    definition of done gives. Integration tests run each through the harness
    in Chromium and check the events received and the demo app's state
    (`/api/state`). The parts of F1 and F2 that concern the command line (A4,
    A5) wait for branch 15. In this branch, tests with `data` or `skip` and
    steps that call user actions are not run yet; say in the report what
    `startRun` does with them.

### Part D: rows and results (`feat/runner-rows-results`, plan branch 9)

The specification is `docs/step-format.md` ("Data rows", "Skipping a test"),
ADR 0015, ADR 0014 (the run folder row of its table) and `docs/protocol.md`
(`listTests`).

17. **Data rows.** One test instance per row, from inline `data`, a CSV file
    or a YAML file, with `${row.…}` and the `testId` of "Identifiers". No new
    dependency: read CSV as the step-format document defines it. If that
    definition is not enough to read real files (quotes, commas inside
    values, line breaks), stop and say so.
18. **`skip`.** One `testSkipped` per row with the reason; they count in
    `totals.skipped` and never make a run fail.
19. **User actions inside runs.** A step that calls a user action runs it with
    the same `ctx`. Sample `tests/user-action.test.yaml` (S13).
20. **The run folder.** The layout of ADR 0015: `run.json`, `test.json` per
    test instance, `events.ndjson`. The events file holds exactly the events
    sent over the protocol, masked the same way. No event carries file
    contents.
21. **`keepRuns`.** Old run folders are deleted at the start of a run as the
    documents say; a folder that is not a run is left alone; `0` deletes
    nothing; a folder that cannot be deleted is a `RunCleanupFailed` warning.
22. **`listTests`.** As `docs/protocol.md` describes, with the `tags` filter
    and `rows`. The desktop app is waiting for it.
23. **Tests.** Checks S6, S13, S15, I8, I9 and I10 pass. For I10, every
    message sent in the integration tests, responses included, validates
    against the JSON Schemas in `packages/protocol/schema/`.

## Done when

- [ ] `pnpm verify` passes on every branch, with no browser installed.
- [ ] `pnpm test:integration` passes on both runner branches.
- [ ] Every pull request is open, and CI (`verify` and `integration`) is green
      on Linux, Windows and macOS for each. Wait for CI and put the results in
      the report.
- [ ] Each finding of review 0005 (1 to 5) has a test that fails without its
      fix. The report names the test for each.
- [ ] Checks S10, S11, S12, S14, F1, F2, F3 and F7 pass on `feat/runner-core`,
      and S6, S13, S15, I8, I9 and I10 on `feat/runner-rows-results`.
- [ ] No browser process is left behind after any test, passed or failed.
- [ ] Committed JSON Schema files match the generator. `PROTOCOL_VERSION`
      stays `0.1.0` unless you had to stop for a protocol change.
- [ ] File header comments and TSDoc on every export, no `any`, and every lint
      or type suppression listed in the report with its reason.
- [ ] No npm dependency is added.
- [ ] No client package imports engine code. Nothing under `apps/desktop/`
      changed.
- [ ] `CHANGELOG.md` is updated in each branch.
- [ ] `handoff/reports/0006-review-fixes-and-the-runner.md` exists on the last
      branch and follows [the template](../templates/report.md).

## Out of scope

- Named pages, `opens`, saved logins, `cancelRun` (plan branch 10).
- Every built-in action other than the four of task 15.
- Screenshots, page snapshots and `openSnapshot` (plan branch 14).
- The CLI's `run` command (plan branch 15).
- The rule for URL regular expressions, and any change to how hidden elements
  count (owner decisions 3 and 4).
- Pointing load errors at their real line (review 0005, finding 8).
- Anything under `apps/desktop/`.
- Raising the Node pin.
- Pushing to `main`, merging into `main`, or force-pushing anything.

## Report back

- For each branch: its pull request number, CI result for both jobs, and
  changed-line count.
- For each finding: the test that proves the fix.
- The events of the S11 sample exactly as a client receives them, one per
  line.
- What one step costs: the time from `stepStarted` to `stepPassed` for a
  `click` on the demo app, on your machine. ADR 0010 asks to revisit locating
  when the overhead passes 200 ms per step.
- How long the `integration` job takes on each system now.
- Every place where a document under `docs/` was unclear, wrong or silent and
  you had to choose.
- Anything in this instruction you think is wrong.
