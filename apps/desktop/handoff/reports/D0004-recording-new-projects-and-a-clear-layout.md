# Report D0004: recording, new projects and a clear layout

- Date: 2026-10-11
- Written by: implementer (desktop track)
- Instruction: [D0004](../instructions/D0004-recording-new-projects-and-a-clear-layout.md)

## Summary

The app now has named regions with a visible edge, a design system with four kinds of button, and three views of each step file with a details panel. It also has a start page with New project, recording with an automatic check, a project panel that lists everything, and forms for a test's settings and a `call` step's flow. One real-engine test does the whole path:

- create a project;
- record a test in the browser;
- see it checked;
- run it;
- edit a step in its form;
- run it again.

Task 21, using the published build by hand, is **not done by me**: I cannot drive a desktop window. It needs the owner (see Questions). The screenshots of task 22 are rendered headless, not captured from the real window.

While rendering them I found and fixed a crash: showing a run in the history crashed the view. I introduced it on `desktop/feat/design-system` (#58). The fix is a new commit there, merged forward into every branch above it.

## Branches and pull requests

All stacked, in this order. Each pull request targets `main`, and its description names the branch it sits on. Lines changed count against the branch below, without images and lock files.

| Branch                        | Last commit | Pull request | Pushed | Lines changed       | CI (Linux, Windows, macOS) |
| ----------------------------- | ----------- | ------------ | ------ | ------------------- | -------------------------- |
| `desktop/feat/protocol-0-1-1` | `e02fd5a`   | #57          | yes    | +533 −60            | 9 of 9 green               |
| `desktop/feat/design-system`  | `f1592f9`   | #58          | yes    | +1125 −380          | 9 of 9 green               |
| `desktop/feat/shell-layout`   | `d87590d`   | #59          | yes    | +897 −264           | 9 of 9 green               |
| `desktop/feat/step-views`     | `6e53786`   | #60          | yes    | +729 −267           | 9 of 9 green               |
| `desktop/feat/new-project`    | `2637dbe`   | #61          | yes    | +848 −27            | 9 of 9 green               |
| `desktop/feat/record`         | `08eaefd`   | #62          | yes    | +1211 −128          | 9 of 9 green               |
| `desktop/feat/record-check`   | `7984829`   | #63          | yes    | +701 −42            | 9 of 9 green               |
| `desktop/feat/project-tree`   | `d08763b`   | #64          | yes    | +512 −70            | 9 of 9 green               |
| `desktop/feat/test-forms`     | `1e7fa7f`   | #65          | yes    | +881 −15            | 9 of 9 green               |
| `desktop/docs/screens`        | this report | #66          | yes    | +24 −2 and 8 images | 9 of 9 green               |

The jobs are `verify`, `integration` and `desktop` on each system. The CI results are those of the last push of each branch: the merge of the history fix, which re-ran every job.

**Splits** (the instruction's rule of roughly 1,500 lines):

- Part B, task 8 to 11 → `shell-layout` (regions, top bar, bottom panel, remembered sizes) and `step-views` (task 9 and the details panel).
- Part D → `record` (tasks 14, 15, 17) and `record-check` (task 16 and the end-to-end test).
- Part E → `project-tree` (task 18) and `test-forms` (tasks 19, 20).
- `design-system` was 1,482 lines when it was pushed; the later fix brought it to 1,505. I did not split it for 23 lines.

## Tasks

| Task | State    | Notes                                                                                                                                                                                           |
| ---- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | done     | `CLAUDE.md`, `handoff/README.md` and the README of `apps/desktop/` say how the two tracks work.                                                                                                 |
| 2    | done     | Protocol 0.1.1 types; the contract tests pass (#57).                                                                                                                                            |
| 3    | done     | Paths from the engine are compared as paths (`LocalPaths`). The desktop tests pass on Windows with `DESKTOP_TESTS_REQUIRE_ENGINE=1` and `DESKTOP_TESTS_REQUIRE_BROWSER=1`.                      |
| 4    | done     | An empty `AvaloniaStats` target in `Directory.Build.targets`, guarded by `BuildSettingsTests`.                                                                                                  |
| 5    | done     | The install command comes from `capabilities.installCommand`. A step skipped without starting shows in its place, greyed out.                                                                   |
| 6    | done     | `Themes/Tokens.axaml`; ADR D0008. `DesignTokenTests` fails on a colour, size or spacing written in a view. It now also fails when a grid track names a number token (the history crash).        |
| 7    | done     | main, normal, quiet, danger; every control shows keyboard focus.                                                                                                                                |
| 8    | done     | Regions as sketched, with the departures listed below. Splitters drag; details and bottom panels collapse; sizes and states are remembered.                                                     |
| 9    | done     | Steps, Targets and Text views. Step cards grouped by section, each group with Add step. The form of the selected step or target is in the details panel. Step actions are on the selected card. |
| 10   | done     | Empty states: no tests, no matching test, no problems, no runs yet, no flows or targets files. The start page shows the engine starting and not found.                                          |
| 11   | done     | Every earlier feature is reachable; the earlier tests pass, some adapted to the new places (listed in the PRs).                                                                                 |
| 12   | done     | Start page: New project, Open project, recent projects, the engine's state.                                                                                                                     |
| 13   | done     | New project form, `createProject`, the engine's refusals by their field, "Record your first test".                                                                                              |
| 14   | done     | Record form; the install command when there is no browser. The start address defaults to `/`, not the environment's address (see below and R0008).                                              |
| 15   | done     | Record tab: live step cards, changes in place, review marks, notices beside them, Stop, the closed browser.                                                                                     |
| 16   | done     | Automatic `verifyRecording`, followed as a run. "This test runs", or the failed step and why with Open test at that step. Passwords explained.                                                  |
| 17   | done     | One recording at a time, none during a run, runs wait while recording, the engine stopping keeps the file and says so, the test is listed at once.                                              |
| 18   | done     | Tests, Flows, Targets and Config groups; New per group; Rename and Delete for the selection.                                                                                                    |
| 19   | done     | Name, tags and skip reason in the details panel. Left to the Text view: data rows, variables, pages, logins, `description`, `freshLogin`.                                                       |
| 20   | done     | The flow picked from the project's flows; the flow's `params` as fields of `with`.                                                                                                              |
| 21   | not done | I cannot use a desktop window by hand. The real-engine test `RealEngineRecordTests` does the same path headless (below). The owner's run of the published build is asked for below.             |
| 22   | done     | Eight screenshots in `apps/desktop/docs/screens/`, shown in the README. **Rendered headless**, not captured from the real window.                                                               |

## Checks

| Command                                                                    | Result                                                                                                                                                                                                                |
| -------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `apps/desktop/scripts/verify.ps1` on every branch                          | passes. On `test-forms`, with `DESKTOP_TESTS_REQUIRE_ENGINE=1` and `DESKTOP_TESTS_REQUIRE_BROWSER=1`: 187 + 49 + 320 tests                                                                                            |
| `pnpm verify`                                                              | not run locally on these branches: nothing outside `apps/desktop/` changed. CI's `verify` job runs it and is green on every pull request above                                                                        |
| `RealEngineRecordTests.A_new_project_records_checks_runs_and_edits_a_test` | passes on Windows with the real engine, Chromium and the demo server. Path: create a project, Record your first test, type a to-do, Stop, the check passes, run, change the fill's value in its form, save, run again |

`RealEngineBuildTests.A_test_built_in_the_step_list_runs_and_passes` failed about 3 times in 30 full local runs. When I caught it with the output kept, the cause was a race in the test, not in the app:

- The run history is read again when a run starts and when it ends.
- The test asserted after the first read, which shows the second run as "Running…".
- The test now waits for the read after the end (#65).

## Departures from the instruction

- **The sketch's top bar.** Run ▾ is a button with a menu: the open test, the selected tests, one item per tag, all tests, and "Show the browser".
  - Run is the region's one **main** button. Record is a normal button with a red dot, because ADR D0008 allows at most one main button per region.
  - Validate, Save all, New project, Open another and Close are in the project ▾ menu.
  - The engine's ● opens its detail and Restart engine.
- **The start address of a recording** defaults to `/` ("a path under the environment's address"), not to the environment's base address. The protocol gives the app only the environments' names. Request [R0008](../requests/R0008-the-environments-addresses-in-openproject.md) asks for the addresses.
- **Where the check of a recording shows.** The check runs as a normal run: it is in the run history and opens in a run tab from the Record tab. The Record tab stays selected and shows the verdict, rather than switching to the run's tab.
- **The details panel for runs and recordings** says what to do instead of showing anything: a run's step details stay in the run tab. It can be hidden from its header, and Show details in the status bar brings it back.
- **The test itself is selected from a card** at the top of its steps view. It is not selected from the project panel, where selecting a test opens it.
- **The bottom panel collapses to its header strip** (Problems, Runs, Engine log), not to nothing, so it can be opened again where it was.

## Decisions I made

- Revert, Rename and Delete of a file moved into its tab's File ▾ menu, so the header strip holds the view switch.
- A file opens on the first view it has: steps for tests and flows, targets for targets files, text for the rest.
  - A problem opens the Text view at its line.
  - A run's step opens the Steps view with that step selected, falling back to the text when no step holds the line.
- A recording's tab cannot close while it records. A recording tab does not survive an engine restart, which reopens the project; the recorded file does.
- The New project form names the project's folder from the project's name until it is changed. It offers the parent of the most recent project as the place.
- The config file shows in the Config group but cannot be renamed or deleted from there.
- Tags are written as one flow list (`[smoke, checkout]`), also when they were a block list. An empty tags or skip field removes the key.
- A `call` step written in the long form stays long when the flow changes (the writer's rule); choosing another flow drops the old flow's `with` values.
- The real-engine end-to-end test drives the headless recording browser through its debugging port with .NET's own `ClientWebSocket`. No package is added (`BrowserInput`).
- The history crash was fixed on the branch that brought it in (#58) and merged forward with merge commits. No pushed history was rewritten.
- `#pragma warning disable`: none added.

## Questions for the owner

1. **Task 21 by hand.** Could you run the published build once and walk the path?
   - Build it: `powershell -NoProfile -ExecutionPolicy Bypass -File apps/desktop/scripts/publish.ps1`.
   - Steps: new project for the demo app (`http://localhost:4310`, demo server running), record a to-do, see it checked, run with the browser shown, edit a step, run again, open the run from the history.
   - Options:
     - (a) you do it and tell me what you find; recommended, since the real-engine test already covers the same path headless;
     - (b) the reviewer does it;
     - (c) it waits for a later instruction.
2. **R0008**: should the engine give the environments' addresses?
   - Options: (a) yes, as proposed, a compatible 0.1.2 addition (recommended); (b) no, `/` stays the default.

## Not done, not pushed, not verified

- Task 21 (see above).
- The screenshots are headless renders with a fake engine, not the real window.
- CI is green on all nine jobs of every pull request, #57 to #66. Each of #57 to #65 also passed a second, re-run, round on its last push.

## What of the engine the app still cannot use

- **Screenshots and page states**: the engine does not produce them yet; the app's places for them are unchanged.
- **The environments' addresses**: not in the protocol (R0008).
- **`listTests` with tags**: the app filters by tag itself.
- **Data rows, variables, pages, logins and the config file**: edited as text, as the instruction allows.

## Suggestions

- A visual test that renders each view's realized rows (as `DocScreensTests` now does for the history) would have caught the history crash. One rendering test per panel with realistic data is cheap.
- The run view inside a run tab is narrow next to the project and details panels; a later instruction could move a run's step details into the details panel.
