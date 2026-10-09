# Instruction D0003: a complete app, from editing to running

- Date: 2026-10-10
- Written by: reviewer
- Based on `main` at: `e5d215d` or later
- Replaces: none
- Follows review: [D0002](../reviews/D0002-review-fixes-and-the-step-file-editor.md)

## Goal

The owner can use the desktop app from start to finish without leaving it:
open a project, create a test, build its steps and targets without writing
YAML, run it in a browser, watch it step by step, read why it failed, and
look at earlier runs. Where the engine cannot do something yet, the app shows
an honest placeholder in the right place, so that nothing in the app's layout
has to move when the engine catches up.

## Owner decisions

1. **The desktop track resumes** (2026-10-10). The owner wants a workable
   end-to-end desktop app now, with placeholders where the engine is not
   ready.
2. **The app is meant to be low-code, or even no-code** (2026-10-10). A tester
   must be able to create, edit and run tests without writing code or YAML.
   The YAML files stay the source of truth underneath, and the text editor
   stays available for those who want it.
3. **Later, not now:** recording a flow and turning it into a runnable test is
   the heart of the product for the owner. The engine has no recorder yet, so
   this instruction only reserves its place (task 20).
4. The desktop plan and ADRs D0001, D0003, D0004 and D0005 are accepted
   (2026-10-10). Task 7 updates their status lines.
5. **Not decided:** ADR D0006 (AvaloniaEdit), which stays "Proposed", and a
   keyboard shortcut for "Save all". Do not add the shortcut.
6. The demo project will get a committed demo-only `.env` from the engine
   track. Until it is on `main`, the demo project needs the environment
   variable `DEMO_PASSWORD` (any value of 4 characters or more); set it in the
   tests that open or run the demo.

## What the engine can do today

All of this is on `main`; read `docs/protocol.md` for the details.

- `listTests` works, with names, tags and row counts.
- `startRun` runs tests in Chromium (headless, or headed with
  `options.headed`) and sends the events `runStarted`, `testStarted`,
  `stepStarted`, `stepPassed`, `stepFailed`, `stepSkipped`, `testSkipped`,
  `pageOpened`, `log`, `testFinished` and `runFinished`. `cancelRun` works.
- Every run writes a folder under the project's data folder
  (`runs/<run id>/`), whose `events.ndjson` holds exactly the events that were
  sent.
- `capabilities.browsers` is empty when Playwright's Chromium is not installed;
  `startRun` is then refused with a message that gives the install command.

What the engine cannot do yet, and what the app does about it:

| Missing in the engine                                               | In the app                                                                                                                                                                                         |
| ------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Screenshots and page snapshots (plan branch 14)                     | Build the real thing: show a step's screenshot when `screenshotReady` arrives and offer "Open page state" when a step's snapshot is saved. Until then the step shows "No screenshot was recorded." |
| Most built-in actions (only `goto`, `click`, `fill`, `expect.text`) | Nothing special: such a step fails with `NotImplemented`, and the app shows that like any other failure.                                                                                           |
| The recorder                                                        | A placeholder screen (task 20).                                                                                                                                                                    |
| A request that returns a file's steps in their canonical form       | The app reads the YAML itself (task 14) and says so in a request to the engine track.                                                                                                              |

## Branches

All stacked, in this order. One pull request per branch into `main`; say in
each description which branch it sits on.

| Branch                        | Based on                      | What it holds          |
| ----------------------------- | ----------------------------- | ---------------------- |
| `desktop/fix/review-d0002`    | `main`                        | Tasks 1 to 7           |
| `desktop/feat/runs`           | `desktop/fix/review-d0002`    | Tasks 8 to 11          |
| `desktop/feat/run-history`    | `desktop/feat/runs`           | Tasks 12 and 13        |
| `desktop/feat/step-builder`   | `desktop/feat/run-history`    | Tasks 14 to 17         |
| `desktop/feat/targets-editor` | `desktop/feat/step-builder`   | Tasks 18 and 19        |
| `desktop/feat/finishing`      | `desktop/feat/targets-editor` | Tasks 20 to 23, report |

If a branch passes roughly 1,500 changed lines (not counting lock files),
split it in two stacked branches and say so in the report. Push each branch
when it is done and its checks pass, before starting the next. If a branch
cannot be finished, stop there and report; the branches below it can still be
merged.

Every pull request now runs the `desktop` CI job on Linux, Windows and macOS.
Wait for it, and fix what it finds, before you move on.

## Tasks

The finding numbers are those of review D0002. Each fix comes with a test that
fails without it.

### Part A: fixes (`desktop/fix/review-d0002`)

1. **Finding 1.** Text typed while the project is being opened again is never
   lost.
2. **Finding 2.** Opening the same project again keeps each tab as it is: its
   text, undo history, caret, scroll position and its "changed on disk" state.
3. **Finding 3.** After "keep my version", the tab counts as unsaved for as
   long as its text differs from the file on disk.
4. **Finding 4.** If asking about unsaved changes fails, the window stays
   open.
5. **Finding 5.** The README says that saving replaces the file (permissions
   and symbolic links are not kept).
6. **The explorer's fallback goes.** The engine answers `listTests`, so remove
   the search for `*.test.yaml` files and its notice. Show each test's name,
   tags and row count. `RealEngineAppTests` checks the environments it needs,
   not the whole list (review 0007 of the engine track, finding 2).
7. **Status lines.** ADRs D0001, D0003, D0004, D0005 and the plan say
   "Accepted (owner, 2026-10-10)". Update the plan to the order of this
   instruction.

### Part B: runs (`desktop/feat/runs`)

8. **Choosing what to run.** Run one test, the tests of a folder, the tests
   with a tag, or all, from the explorer and from an open test's tab. Choose
   the environment (as today), and whether the browser is shown (`headed`).
   When the engine reports no browser, running is disabled and the app shows
   the engine's install command.
9. **The run view.** While a run is going and after it: the tests with their
   status, and for the selected test its steps in order, grouped by section
   (`before`, `steps`, `after`), each with status, title, duration and page.
   For a failed step: the error's code, message, hint, expected and actual
   values, and the candidates that were tried. Locator fallbacks and other
   warnings are visible on the step they belong to. Selecting a step can open
   its file at its line. Data rows show as separate test instances; skipped
   tests show their reason.
10. **Cancelling, and the engine going away.** A cancel button that calls
    `cancelRun` and shows the run ending as cancelled. If the engine stops
    during a run, the run view says so and keeps what it had received. A
    second run cannot be started while one is going. Unsaved files: ask to
    save before a run, because the engine runs what is on disk.
11. **Screenshots and page states**, built against the protocol as it is
    defined, as the table above says.

### Part C: run history (`desktop/feat/run-history`)

12. **Earlier runs.** List the run folders of the project, newest first, with
    start time, environment, result and totals. Open one in the same run view
    as a live run. Read only `events.ndjson`, which is protocol events; do not
    rely on the shape of `run.json` or `test.json`. A folder whose events end
    without `runFinished` is shown as a run that did not finish.
13. **Robustness.** A damaged or half-written events file, a line that is not
    a known event, and a folder that disappears while it is shown (the engine
    deletes old runs) must not break the list.

### Part D: building tests without YAML (`desktop/feat/step-builder`)

14. **A step list for test and flow files.** Next to the text editor, a second
    view of the same file: its sections with their steps as a list. Each step
    shows its action and its main value. The two views show the same text:
    a change in one is in the other at once, and undo covers both. The app
    reads the YAML itself to find the steps (the three ways a step can be
    written are in `docs/step-format.md`; an action's `shorthand` comes from
    `listActions`). Add a YAML library at an exact version and record the
    choice in an ADR (Proposed). A change in the step list rewrites only that
    step's lines; comments and layout elsewhere in the file stay as they
    were. A file the app cannot read as steps shows the text editor only, with
    a line saying why.
15. **A form for each step.** Selecting a step shows a form built from the
    action's `paramsSchema`: text, number, yes/no, a choice from a list, and a
    target picker that offers the targets of the file and the shared ones. A
    parameter the form cannot show is edited as YAML text in the same form.
    The step's own settings (name, timeout, page) are in the form too. The
    engine's problems for the step show in the form, next to the field where
    possible.
16. **Adding, removing and ordering.** Add a step by picking an action from
    the catalogue (with search), remove, duplicate, move up and down, and move
    between sections.
17. **New files.** Create a test, a flow or a targets file from the app: ask
    for the name and folder, write a minimal valid file, open it. Rename and
    delete a file, with a question before deleting. The engine validates the
    result as for any file.

### Part E: targets (`desktop/feat/targets-editor`)

18. **A targets editor**, for targets files and for the targets inside a test
    or flow: the targets by name, and for each its candidates in order, since
    the order is the order of reliability. Add, remove and reorder
    candidates; choose a candidate's kind and value, and `exact` and `nth`;
    set `frame` and `within` by picking another target. It writes the same
    YAML the text editor shows, by the rules of task 14.
19. **From a failure to its target.** In the run view, a failed or
    fallen-back target links to that target in the editor, with the candidate
    counts of the failure shown beside the candidates.

### Part F: finishing (`desktop/feat/finishing`)

20. **The recorder's place.** A "Record" entry where recording will start,
    opening a screen that says in plain words that recording arrives with a
    later engine version, and what it will do. No fake recording.
21. **A build anyone can make.** A `NuGet.Config` in `apps/desktop/` that
    names nuget.org, so restoring does not depend on the machine's own
    settings. Scripts `scripts/publish.sh` and `scripts/publish.ps1` that
    build the engine and publish the app into one folder inside the
    repository, and print the path of the executable.
22. **A walk-through in the README:** from a fresh clone to a passing run of
    the demo project, step by step, including starting the demo web server
    (`node examples/demo-app/server/server.ts`), creating a test in the app,
    running it with the browser shown, and reading a failure.
23. **A request to the engine track** (`requests/R0004-…`): what the app had
    to work out by reading YAML itself in tasks 14 and 18, and the request you
    would want from the engine in its place. Raise another request for
    anything else you had to guess.

### Tests, for every part

- View models without a window, with the fake engine sending scripted events:
  a passing run, a failing one with every kind of error detail, a cancelled
  one, an engine that stops mid-run.
- Headless tests for the views that hold logic in their bindings.
- Real engine, real browser, in a **temporary copy** of `examples/demo-app`
  with the demo web server started on a free port (the engine track's harness
  in `packages/engine/src/testing/demo-app.ts` shows how): run a passing
  sample, a failing fixture and a cancelled run, and open the finished run
  from the history. Build a test with the step list and run it. These tests
  skip when the engine reports no browser, unless
  `DESKTOP_TESTS_REQUIRE_BROWSER=1`. The reviewer adds the browser to the
  `desktop` CI job at the next check; install it locally with
  `pnpm --filter @cfe/engine exec playwright install --only-shell chromium`.
- No test writes inside `examples/`.

## Done when

- [ ] `apps/desktop/scripts/verify.sh` and the root `pnpm verify` pass on every
      branch.
- [ ] Every pull request is open and all CI jobs are green on Linux, Windows
      and macOS.
- [ ] Findings 1 to 4 each have a test that fails without the fix, named in
      the report.
- [ ] With the real engine and a real browser: a test built in the step list
      runs and passes; a failing fixture shows its error with expected and
      actual; a cancelled run ends as cancelled; a finished run opens from the
      history.
- [ ] A change made in the step list or the targets editor leaves every other
      line of the file as it was, comments included.
- [ ] The walk-through in the README was followed once from a fresh clone, and
      the report says on which system.
- [ ] File header comments and XML documentation on every public type and
      member; no `dynamic`; every `#pragma warning disable` listed in the
      report with its reason.
- [ ] The only package added is the YAML library, at an exact version, with
      the lock files updated.
- [ ] Nothing outside `apps/desktop/` changed.
- [ ] The desktop `CHANGELOG.md` is updated in each branch.
- [ ] `apps/desktop/handoff/reports/D0003-a-complete-app-from-edit-to-run.md`
      exists on the last branch and follows the root report template.

## Out of scope

- Any recording, real or pretended.
- Completion and syntax colouring in the text editor.
- Comparing runs, and lists of flaky or slow tests.
- Installers, a bundled Node or engine, updates (milestone D6).
- Any change outside `apps/desktop/`, including the protocol, the engine, the
  demo project and CI.
- Merging into `main`, pushing to it, or force-pushing anything.

## Report back

- For each branch: its pull request number, the CI result of all jobs on three
  systems, and its changed-line count.
- For each finding: the test that proves the fix.
- A short account of how the step list and the text stay the same file, and
  what happens to comments and layout.
- The YAML library, its version and licence, and why it.
- Every placeholder in the app, and what in the engine each one waits for.
- What you could not see or verify, above all on a real screen.
- Every place where you had to choose because this instruction or a document
  was unclear, wrong or silent.
- Anything in this instruction you think is wrong.
