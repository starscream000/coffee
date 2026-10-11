# Review D0004: recording, new projects and a clear layout

- Date: 2026-10-11
- Written by: reviewer
- Instruction: [D0004](../instructions/D0004-recording-new-projects-and-a-clear-layout.md)
- Report: `apps/desktop/handoff/reports/D0004-recording-new-projects-and-a-clear-layout.md`
  on the branch `desktop/docs/screens` (it reaches `main` when pull request
  #66 is merged)
- Verdict: **Approved**, all ten pull requests (#57 to #66). **One task is
  still open and is the owner's: using the published app by hand on Windows
  (task 21).**

## Pull requests

| Pull requests | Branches, in merge order                                                                                                                                                      | Reviewed at (top) | CI                          | Verdict  |
| ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------- | --------------------------- | -------- |
| #57 to #66    | `feat/protocol-0-1-1`, `design-system`, `shell-layout`, `step-views`, `new-project`, `record`, `record-check`, `project-tree`, `test-forms`, `docs/screens` (all `desktop/…`) | `bef6904`         | all nine jobs green on each | Approved |

Merge in the order of their numbers, straight after the engine track's #53 to
#56. Then pull request
[#67](https://github.com/starscream000/coffee/pull/67), the reviewer's change
that gives the `desktop` CI job a browser (reviewed at `dd997ca`, all nine
jobs green).

## What I checked

- **CI** on all ten pull requests: green on Linux, Windows and macOS.
- **The real-browser tests on three systems**, by adding Chromium to the
  `desktop` job on top of the stack (#67) and making a missing browser a
  failure instead of a skip. All green. That run includes the test that
  creates a project, records a to-do, sees it checked, runs it, edits a step
  and runs again, against a real engine. Review D0003's finding 1 (the path
  compared as text on Windows) is fixed: the Windows job passes.
- **The layout, from five of the eight screenshots** (start page, new
  project, steps view, recording checked, failed run), against the owner's
  complaint that the parts and their hierarchy could not be told apart:
  - four regions with borders and headers: Project, the tabs, Details, and
    the bottom panel with Problems, Runs and Engine log;
  - a top bar with the project, the environment, Record, Run and the
    engine's state; a status bar;
  - one main (blue) button per region; Remove is red; values of state use
    green, amber and red;
  - the start page has "New project" and "Open project…", and the form asks
    for name, folder, address and first environment.
- **By search over the changes:** nothing outside `apps/desktop/` changed, no
  `#pragma warning disable`, no `dynamic`, no package added, telemetry
  switched off and checked by a test.

## What I did not check

- **The C# source** (about 7,600 added lines). I relied on CI and the
  screenshots.
- **The real window.** The screenshots are renders without a window and with
  a stand-in engine, as the report says. Nobody has yet used this build by
  hand; see finding 1.
- Three screenshots (targets view, recording in progress, run history).

## Findings

None that block a merge.

1. **Open task, the owner's. Task 21 was not done**: the implementer cannot
   use a desktop window. The automated test walks the same path without a
   window, so this does not hold the merge, but the instruction's goal is an
   app the owner finds clear, and only he can say so. Steps are under "Owner
   decisions needed".
2. **Should improve. A failed run has too little room for the failure.** In
   `run-failed.png` the error box, the most useful thing on the screen, sits
   in a small area that scrolls, beside an empty screenshot area. The report
   suggests the fix itself: show the selected step's details in the Details
   panel.
3. **Should improve. Tags are cut off in the Project panel** ("accou…" beside
   "Customer logs in"), and the panel then shows a sideways scroll bar. Wrap
   or shorten with "+1".
4. **Note.** The check of a recording says "This test runs" also when a step
   is marked for review. Both are shown, which is right; see engine review
   0010, finding 1, for what is behind it.

## Rulings on the report

- All six departures are accepted. Record as a normal button with a red dot
  beside the one main button, Run, follows ADR D0008.
- All "Decisions I made" are accepted.
- **Request R0008** (the environments' addresses in `openProject`): accepted
  for the next engine instruction. Until then `/` as the start address is
  fine.
- ADR D0008 (design tokens and four buttons) stands as accepted by the
  instruction.

## Done-when checks

| Check                                                    | Result                                        |
| -------------------------------------------------------- | --------------------------------------------- |
| CI green on three systems, every pull request            | Yes                                           |
| Real-engine and real-browser tests pass on three systems | Yes (pull request #67)                        |
| New project, recording, project tree, forms              | Yes per the report and its end-to-end test    |
| A layout whose parts can be told apart                   | Yes in the screenshots; the owner's word open |
| The published app used by hand (task 21)                 | **No, open with the owner**                   |
| Nothing outside `apps/desktop/` changed                  | Yes                                           |
| `CHANGELOG.md` updated; the report follows the template  | Yes                                           |

## Owner decisions needed

1. **Use the app once by hand** and say what you find:
   - `git pull`, then
     `powershell -NoProfile -ExecutionPolicy Bypass -File apps/desktop/scripts/publish.ps1`;
   - start the demo site: `node examples/demo-app/server/server.ts`;
   - in the app: New project (address `http://localhost:4310`), Record a
     to-do, Stop, see it checked, Run with the browser shown, change a step,
     run again, open the run from the history.
2. Still open from before: accept ADR D0006 (AvaloniaEdit) and ADR D0007
   (YamlDotNet)? A shortcut for "Save all"? Recommended: yes to all three.

## For the next instruction

Findings 2 and 3, what the owner finds in his trial, and the engine's next
additions (the environments' addresses, recorded checks) as they arrive.
