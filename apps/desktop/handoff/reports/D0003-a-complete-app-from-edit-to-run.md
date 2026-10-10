# Report D0003: a complete app, from editing to running

- Date: 2026-10-10
- Written by: desktop implementer
- Instruction: [D0003](../instructions/D0003-a-complete-app-from-edit-to-run.md)

## Summary

Every task of D0003 is done, in eleven stacked branches with one pull request
each (#31 to #41), every one green on all nine CI jobs (Linux, Windows,
macOS) except #41, whose CI started with this report. A tester can now open a
project, create a test, build it in the step list and step forms without
writing YAML, edit targets, run tests with the browser shown, read a failure
(with a link from a failed target to its definition), cancel, and reopen
earlier runs. The engine of protocol 0.1.0 sends no screenshots and saves no
page states yet; the app is ready for both. Next needed: the owner's word on
ADR D0006 and D0007 (proposed), answers to requests R0004 to R0007, and the
browser in the `desktop` CI job so that the real-browser tests run there.

## Branches and pull requests

Merge in this order; each branch sits on the one before. Lines are each
branch's own change.

| Branch                           | Last commit | Pull request | Pushed | CI (9 jobs, 3 systems) | Lines (+/−)                       |
| -------------------------------- | ----------- | ------------ | ------ | ---------------------- | --------------------------------- |
| `desktop/fix/review-d0002`       | `835c1f3`   | #31          | yes    | green                  | +403 −175                         |
| `desktop/feat/run-model`         | `c0e6055`   | #32          | yes    | green                  | +963                              |
| `desktop/feat/runs`              | `f8f50d5`   | #33          | yes    | green                  | +1633 −30                         |
| `desktop/feat/run-history`       | `df72765`   | #34          | yes    | green                  | +888 −6                           |
| `desktop/feat/step-reader`       | `463bf95`   | #35          | yes    | green (after a fix)    | +996 −1                           |
| `desktop/feat/step-builder`      | `371c4d7`   | #36          | yes    | green (see Checks)     | +863 −11                          |
| `desktop/feat/step-forms`        | `185d879`   | #37          | yes    | green                  | +1408 −46, plus 2378 of test data |
| `desktop/feat/new-files`         | `7ce5add`   | #38          | yes    | green                  | +742 −2                           |
| `desktop/feat/targets-editor`    | `8b6b01f`   | #39          | yes    | green                  | +1477 −24                         |
| `desktop/feat/failure-to-target` | `4e3b2f3`   | #40          | yes    | green                  | +278 −10                          |
| `desktop/feat/finishing`         | see #41     | #41          | yes    | running                | about +700 with this report       |

Part B and Part D were split (run model and runs; step reader, step list,
forms and new files) to stay near 1500 lines; `runs` is 1633 lines, half of
them tests. `step-forms` carries `list-actions.json`, a snapshot of the real
engine's `listActions` used as test data.

## Tasks

| Task | State | Notes                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| ---- | ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1–4  | done  | Review D0002 findings 1 to 4, each with a test: `ReviewD0002FixesTests.Text_typed_while_the_project_is_opened_again_is_kept` (1), `Reopening_keeps_the_same_tab_with_its_undo_history_caret_and_changed_on_disk_state` and the caret test in `MainWindowTests` (2), `After_keep_my_version_the_tab_stays_unsaved_while_its_text_differs_from_the_disk` (3), `If_asking_about_unsaved_changes_fails_the_window_stays_open` (4). All four were run against `main`'s code (in a worktree of `origin/main`) and fail there; for finding 2 without its caret and scroll lines, since `CaretOffset` and `VerticalScroll` came with the fix. |
| 5    | done  | The README says that saving replaces the file (#31).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| 6    | done  | The explorer's fallback is gone; it shows `listTests`' names, tags and row counts; `RealEngineAppTests` checks only what it needs (#31).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 7    | done  | ADRs D0001, D0003, D0004, D0005 and the plan say "Accepted (owner, 2026-10-10)"; the plan follows this instruction's order (#31).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 8    | done  | Run all, Run selected (a test, or a folder's tests), Run tag, Run in a test's tab; environment; Show browser. Without a browser, running is off and the install command is shown (#33).                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| 9    | done  | The run view, built only from events: tests, steps by section and nesting, durations, pages, error code, message, hint, expected, actual, candidates, warnings, fallbacks, data rows, skip reasons, open the step's file at its line (#32, #33).                                                                                                                                                                                                                                                                                                                                                                                      |
| 10   | done  | Cancel (`cancelRun`); an engine that stops mid-run keeps what it reported; one run at a time; the question about unsaved files (#33).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| 11   | done  | Screenshot or "No screenshot was recorded."; "Open page state" when saved, with `SnapshotUnavailable`'s screenshot as fallback (#33). See "Not verified".                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 12   | done  | Run history from `events.ndjson` only, newest first; unfinished runs shown as such (#34).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 13   | done  | Damaged and half-written lines, unknown events, folders without events, folders deleted while listed (#34).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| 14   | done  | Step list beside the text, one text for both, one undo; YamlDotNet 18.1.0 and ADR D0007 (proposed); only the step's lines change (#35, #36).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| 15   | done  | Forms from `paramsSchema` with a target picker; problems by line and column next to their field (#37).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| 16   | done  | Add (picker with search), remove, duplicate, move up and down, move between sections (#36).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| 17   | done  | New test, flow and targets file; rename; delete with a question (#38).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| 18   | done  | Targets editor: names, candidates in order with kind, value, accessible name, exact and nth, added, removed and moved; frame and within; comments kept (#39).                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| 19   | done  | "Open target" from a failed or fallen-back target, with the failure's match counts (#40).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 20   | done  | Record opens a tab that says recording arrives later and what it will do (#41).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| 21   | done  | `NuGet.Config` (nuget.org only), `scripts/publish.sh` and `scripts/publish.ps1` (#41).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| 22   | done  | README walk-through; followed from a fresh clone, see "Checks" (#41).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| 23   | done  | Requests R0004 (structure of step files), R0005 (install command), R0006 (skipped steps without a start), R0007 (a test that fails by chance) (#41).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |

## Checks

All on the last branch, on Linux (the cloud container), with Node 24.11,
.NET SDK 10.0.112 and Chromium of Playwright's earlier build (see "Not
verified").

| Command                                                                                                | Result                                             |
| ------------------------------------------------------------------------------------------------------ | -------------------------------------------------- |
| `apps/desktop/scripts/verify.sh` with `DESKTOP_TESTS_REQUIRE_ENGINE=1 DESKTOP_TESTS_REQUIRE_BROWSER=1` | passed: 146 + 48 + 252 tests, none skipped         |
| `corepack pnpm verify` (root)                                                                          | passed: 388 tests                                  |
| `apps/desktop/scripts/publish.sh`                                                                      | passed; prints `apps/desktop/dist/app/Desktop.App` |
| `pwsh apps/desktop/scripts/publish.ps1` (PowerShell 7.6.6 on Linux)                                    | passed; prints the same path                       |

CI failures met on the way, both handled:

- #35 `desktop` jobs failed `dotnet format --verify-no-changes` on
  `YamlTree.cs`, committed before formatting when the branch was split. Fixed
  in `463bf95`; #36 took it by a merge.
- #36 `integration (windows-latest)` failed in the engine's test I11, which
  fails by chance (a random hash contains `ada`); not this track's code. A
  comment on the pull request says so; the next run passed; request R0007.

**The walk-through, followed once** from a fresh clone (`git clone` of this
branch into a new folder), on Linux:

- Steps 1 to 3: done as written; `publish.sh` built the engine and the app.
- Step 4: `playwright install chromium` fails in this container because its
  network policy refuses `cdn.playwright.dev`; the browsers already set up
  here were used instead (`PLAYWRIGHT_BROWSERS_PATH`).
- Step 5: done as written; the server answered on `http://127.0.0.1:4310`.
- Step 6: done under Xvfb (no screen here); the app showed the start page
  with "Engine ready · browsers: chromium", using the engine of the clone.
- Steps 7 to 11 need clicks, which this container cannot make in a real
  window. They are run by `RealEngineBuildTests` through the same view models
  the buttons call, with the real engine and Chromium: create the test, build
  it in the step list and forms, save, run (passed), set `equals` to `2`, run
  (failed with expected `"2"` and actual `"1"`, and the `after` step passed),
  and both runs in the history. The browser was headless there, not shown.

## Departures from the instruction

- **Branch names.** Parts B and D were split for size into
  `desktop/feat/run-model` + `desktop/feat/runs` and
  `desktop/feat/step-reader` + `desktop/feat/step-builder` +
  `desktop/feat/step-forms` + `desktop/feat/new-files`; task 19 has its own
  branch, `desktop/feat/failure-to-target`.
- **The install command** shown without a browser is
  `pnpm --filter @cfe/engine exec playwright install chromium`, built from
  `Product.NpmScope`, because the engine says its own command only when a run
  is refused (request R0005).

## Decisions I made

- **YamlDotNet's parser only, changes made on the text** (ADR D0007,
  proposed): no YAML library for .NET keeps comments through a document
  model, so the app finds positions with the parser and rewrites only the
  lines a change is about.
- **Target parameters** are those whose schema allows an object with
  `candidates`; every built-in target parameter is found this way (tested
  against the engine's own schemas). Request R0004 asks for a mark.
- **Shared targets files** are found by their ending (`.targets.yaml`), not
  by the config's globs, which only the engine reads (R0004).
- **New tests and flows** start with one `goto: /` step, because the engine
  refuses an empty `steps` ("needs at least one step").
- **Comments inside a target** go with the candidate below them, so moving a
  candidate moves its comment.
- **A step skipped before it started** (after a failure) is listed in its
  test's messages, since the event has no title or line (R0006).
- **Form fields write when they lose focus**, so typing does not make one
  undo step per key.
- **The published app lives in `apps/desktop/dist/app`**, which the root
  `.gitignore` and `.prettierignore` already leave out.

## Questions for the owner

1. **ADR D0006 (AvaloniaEdit) and D0007 (YamlDotNet)** are proposed. Accept
   both (recommended), or ask for another way.
2. **Browser in the `desktop` CI job.** The instruction says the reviewer adds
   it; until then the real-browser tests skip in CI (they run here). Add it
   with `DESKTOP_TESTS_REQUIRE_BROWSER=1` (recommended), or keep them local.
3. **Avalonia's build telemetry.** Avalonia's build tasks try to reach
   `av-build-tel-api-v1.avaloniaui.net` during builds (seen refused in this
   container). Turn it off for the desktop with `AVALONIA_TELEMETRY_OPTOUT=1`
   in `Directory.Build.props` (recommended, no effect on the app), or leave
   it.

## Not done, not pushed, not verified

- **Screenshots and page states with the real engine**: not verified, because
  the engine of protocol 0.1.0 sends no `screenshotReady`, saves no page
  states (every step's snapshot is `skipped`) and does not implement
  `openSnapshot`. The app's side is tested with scripted events only.
- **A run with the browser shown**: not verified here (no screen); every real
  run here was headless. The option is sent as `options.headed: true`
  (tested).
- **`playwright install chromium`**: not run successfully here (network
  policy); the tests used Chromium of Playwright's earlier build, set up by
  hand in this container.
- **Steps 7 to 11 of the walk-through by hand**: run through view models, not
  by clicking (see Checks).
- **The app on Windows and macOS by hand**: not tried; CI builds and tests it
  on both, headless.

## Suggestions

- When the engine records screenshots, add their real-engine test next to
  `RealEngineRunTests` (the view is ready).
- A `step` file's form could offer a `call` step's flows and their parameters
  once the engine can list flows (part of R0004's `outlineFile` for flows).
