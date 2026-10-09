# Review D0002: review fixes and the step file editor

- Date: 2026-10-10
- Written by: reviewer
- Instruction: [D0002](../instructions/D0002-review-fixes-and-the-step-file-editor.md)
- Report: `apps/desktop/handoff/reports/D0002-review-fixes-and-the-step-file-editor.md`
  on the branch `desktop/feat/unsaved-changes` (it reaches `main` when pull
  request #24 is merged)
- Verdict: **Approved**, all four pull requests. This also settles review
  D0001: its two "must fix" findings are fixed, so #16 can be merged.

## Pull requests

| Pull request                                           | Branch                          | Reviewed at | CI                                                                   | Verdict  |
| ------------------------------------------------------ | ------------------------------- | ----------- | -------------------------------------------------------------------- | -------- |
| [#16](https://github.com/starscream000/coffee/pull/16) | `desktop/feat/workspace-shell`  | `93cc446`   | `verify`, `integration` green; `desktop` green, all on three systems | Approved |
| [#20](https://github.com/starscream000/coffee/pull/20) | `desktop/fix/review-d0001`      | `a68b844`   | `verify`, `integration` green on three systems; `desktop` see below  | Approved |
| [#22](https://github.com/starscream000/coffee/pull/22) | `desktop/feat/step-file-editor` | `6ce7ecf`   | `verify`, `integration` green on three systems; `desktop` see below  | Approved |
| [#24](https://github.com/starscream000/coffee/pull/24) | `desktop/feat/unsaved-changes`  | `f36fddc`   | `verify`, `integration` green; `desktop` green, all on three systems | Approved |

Merge in this order: #16, #20, #22, #24, then #18 (the `desktop` CI job).

## What I checked

- **Scope and stack.** The four branches sit on each other in the order
  above, the first holds `main`, and no file outside `apps/desktop/` changed
  on any of them.
- **The `desktop` job on Linux, Windows and macOS**, through my CI branch
  (`chore/desktop-ci`, pull request #18), because the job is not on `main`
  yet:
  - with #16's branch merged in (commit `c2ab412`): format, build and all C#
    tests pass on all three systems. The three tests that failed on Windows
    in review D0001 pass there now, in both variants.
  - with the top of the stack merged in (commit `8683109`, which holds #20,
    #22 and #24): the same, on all three systems, twice in a row.
- **Root CI** (`verify` and `integration`) is green on three systems for each
  of the four pull requests.
- **The source, line by line**, for everything under `apps/desktop/src` that
  changed: the engine session and its queue, the connection, the new
  exceptions, the engine service, the file kinds and the watcher, the shell,
  workspace, problems and step file view models, the dialog service, the code
  of the editor view and of the main window.
- **Three test files**: the rewritten `EngineLocatorTests`, the changed
  `RealEngineAppTests`, and the new `RealEngineEditorTests` (it works in a
  temporary copy, as asked).
- **The report's account of finding 2** matches the code: the test had started
  the engine itself and then shown a window whose `Opened` handler started it
  again. The session now queues starts and stops, and refuses a start after
  the app began to shut down.

## What I could not check

- **The `desktop` job on the two middle branches by themselves.** I ran it at
  the bottom of the stack and at the top, not with only #20 or only #22 on
  top of #16. The implementer ran the desktop checks on each branch on Linux.
- **Most of the tests and the `.axaml` views.** I read the three test files
  named above and the main window's markup change, and relied on the rest
  passing.
- **The app on a real screen.** Still nobody has seen it outside headless
  rendering. The new dialogs (`AvaloniaDialogService`) are not exercised by
  any test either; the report says so.
- That each fix's test fails without the fix. The report says how the
  implementer checked it; I confirmed the fixed behaviour through CI only.
- Cmd+S on macOS and the file watcher's behaviour on macOS and Windows beyond
  what the tests cover.

## Findings

None of these blocks a merge. All go into the next desktop instruction.

1. **Should fix first. Text typed while the project is being opened again can
   be lost** (`ShellViewModel.OpenProjectAsync`). For the same project, the
   open tabs and their unsaved text are captured before the engine is asked
   to open the project and before the new workspace loads. Whatever the user
   types in between is not in the capture and disappears when the workspaces
   are swapped. The window is short, but a reopen happens on every save of the
   config or of an action file. Capture at the moment of the swap, or keep the
   same tab objects (finding 2).
2. **Should fix. Opening the same project again rebuilds every tab.** Undo
   history, caret and scroll position are lost, and so is the "changed on
   disk" state: a file that changed on disk while it had unsaved text gets the
   new disk content as its baseline, so the next save overwrites it without
   the question. Carry the tab view models over instead of recreating them.
3. **Note. "Keep my version", then undo back to the text first read.** The tab
   then says it has no unsaved changes and Save is disabled, while the disk
   holds the other version. The baseline for "unsaved" must move together
   with the baseline for "changed on disk".
4. **Note. If asking about unsaved changes itself fails, the window closes**
   (`ConfirmCloseWindowAsync` answers "yes" in its catch). Unsaved text is
   then lost. Staying open is the safer failure.
5. **Note. Saving replaces the file with a new one.** The original's
   permissions are not kept, and a symbolic link would be replaced by a plain
   file. Fine for now; say so in the README.
6. **Note. Any source file change reopens the project.** This is the rule
   instruction D0002 asked for, and it is too broad when step files live in
   the same repository as the application under test: every source save
   there reopens the project. The right fix is for the engine to say which
   files it loaded; that is a protocol addition for the engine track.
7. **Note. ADR status lines** for D0001, D0003, D0004, D0005 and the plan
   still say "Proposed" although the owner accepted them on 2026-10-10.

## Rulings on the report's "Decisions I made"

| No. | Decision                                                        | Ruling                                           |
| --- | --------------------------------------------------------------- | ------------------------------------------------ |
| 1   | Save on the platform's command key, within the tab              | Accepted.                                        |
| 2   | "Keep my version" makes the disk content the new baseline       | Accepted, with finding 3.                        |
| 3   | The app's own save is recognised by content, not by timing      | Accepted. Better than what I asked for.          |
| 4   | One line-ending style per file, taken from its first line break | Accepted.                                        |
| 5   | While a tab is unsaved, its file's problems are the text's only | Accepted.                                        |
| 6   | Reopening the same project keeps tabs and unsaved text          | Accepted, with findings 1 and 2.                 |
| 7   | Without a dialog service every question is answered "cancel"    | Accepted.                                        |
| 8   | `IDialogService` arrives on the editor branch                   | Accepted.                                        |
| 9   | The status line keeps reporting the last validation from disk   | Accepted.                                        |
| 10  | ADR status lines left as they are                               | Accepted; the next instruction has them updated. |

The departures are accepted: four branches instead of three, a failed session
that is stopped becomes `Stopped`, and the in-memory test engine answers
before it exits.

## Done-when checks

| Check                                                                  | Result                                                       |
| ---------------------------------------------------------------------- | ------------------------------------------------------------ |
| Both check scripts pass on every branch                                | Yes per the report (Linux); root CI green on each            |
| `desktop/feat/workspace-shell` holds tasks 1 to 3 and nothing else new | Yes: the merge of `main` and two test commits                |
| Each of findings 1 to 7 has a test that fails without its fix          | Yes per the report                                           |
| The flaky test passed 30 times in a row                                | Yes per the report; green in three CI runs on three systems  |
| A step file can be edited, saved, reverted and validated while typing  | Yes (code read; headless and real-engine tests pass)         |
| Every pull request open, root CI green                                 | Yes                                                          |
| Headers, XML documentation, no `dynamic`, suppressions listed          | Yes; there is no `#pragma warning disable` in the folder     |
| Only `Avalonia.AvaloniaEdit` added, exact version, lock files updated  | Yes (12.0.0); restore in locked mode passes on three systems |
| Nothing outside `apps/desktop/` changed                                | Yes                                                          |
| The desktop `CHANGELOG.md` updated in each branch                      | Yes                                                          |
| The report exists and follows the template                             | Yes. It is clear about what was checked on Linux only.       |

## Owner decisions needed

1. **Accept ADR D0006 (AvaloniaEdit as the editor)?** Recommended: accept. It
   is the established code editor for Avalonia, it has a release for Avalonia
   12, and it built and ran on all three systems.
2. **A keyboard shortcut for "Save all"?** Recommended: yes, Ctrl+Shift+S
   (Cmd+Shift+S on macOS), in the next instruction.

## For the next instruction

Instruction D0003:

- Findings 1 to 5 and 7.
- The owner's answers above.
- The rest of milestone D4 (completion of action and target names, parameter
  forms, the targets editor), or runs (D2) if the engine's runner is on
  `main` by then. The owner chooses.
