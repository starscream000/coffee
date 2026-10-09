# Report D0002: review fixes and the step file editor

- Date: 2026-10-10
- Written by: desktop implementer
- Instruction: [D0002](../instructions/D0002-review-fixes-and-the-step-file-editor.md)

## Summary

All fifteen tasks are done, on four branches instead of three: the editor
was split in two for size, as the instruction allows. #16 now holds `main`
and the two test fixes (findings 1 and 2) and nothing else. Findings 3 to 7
are fixed on their own branch. Step files can be edited, saved, reverted and
validated while typing, and unsaved changes are never lost without a
question. Every finding has a test that fails without its fix (see
"Findings"). Everything was checked on Linux only.

## Branches and pull requests

Stacked; merge in this order. Line counts leave out the lock files.

| Branch                          | Last commit                                   | Pull request                                           | Pushed | Checks (desktop / root)                     | Changed lines                                  |
| ------------------------------- | --------------------------------------------- | ------------------------------------------------------ | ------ | ------------------------------------------- | ---------------------------------------------- |
| `desktop/feat/workspace-shell`  | `93cc446`                                     | [#16](https://github.com/starscream000/coffee/pull/16) | yes    | pass (222 tests) / pass (357)               | after the merge of `main`: +99 −37 (all tests) |
| `desktop/fix/review-d0001`      | `a68b844`                                     | [#20](https://github.com/starscream000/coffee/pull/20) | yes    | pass (247) / pass (357)                     | +1,000 −109                                    |
| `desktop/feat/step-file-editor` | `6ce7ecf`                                     | [#22](https://github.com/starscream000/coffee/pull/22) | yes    | pass (266) / pass (357)                     | +1,474 −171                                    |
| `desktop/feat/unsaved-changes`  | the commit that adds this report (branch tip) | opened with this report                                | yes    | pass (280) / pass (357), before this report | +336 −11, before this report                   |

"Checks" are `apps/desktop/scripts/verify.sh` (format, a build with warnings
as errors, all desktop tests, Prettier) and the root `pnpm verify`. CI: root
`verify` runs on each pull request. The `desktop` job of #18 is not on these
branches, so I saw no Windows or macOS result.

## Tasks

| Task | State | Notes                                                                                                                                                                                                                         |
| ---- | ----- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | done  | `origin/main` merged into `desktop/feat/workspace-shell` (merge commit `060debf`); `STATUS.md` taken from `main`; `pnpm install` and `pnpm build` run again. The protocol contract tests pass against `main`'s schemas.       |
| 2    | done  | Finding 1. See "Findings".                                                                                                                                                                                                    |
| 3    | done  | Finding 2. See "Cause of finding 2". The test passed 30 runs in a row.                                                                                                                                                        |
| 4    | done  | Finding 3: `EngineSession` queues its starts, stops and close; `CloseAsync`; `EngineNotReadyException`; app side `IEngineService.ShutdownAsync`.                                                                              |
| 5    | done  | Finding 4: handler errors become protocol problems; any other end of reading fails waiting requests (`EngineConnectionFailedException`) and completes `Closed`; a result that does not fit is a `ProtocolViolationException`. |
| 6    | done  | Finding 5: catch-all with a short message (notice bar or status line) and the full detail in the engine log; the tasks still discarded can no longer fail.                                                                    |
| 7    | done  | Finding 6: one refresh at a time, batches joined; generation numbers drop older answers to `validate` and `listTests`.                                                                                                        |
| 8    | done  | Finding 7: `ProjectFileKinds`; the watcher reports action sources; a change reopens the project. The desktop README says so.                                                                                                  |
| 9    | done  | `Avalonia.AvaloniaEdit` 12.0.0, ADR D0006 (Proposed).                                                                                                                                                                         |
| 10   | done  | The view model owns a `TextDocument` (text and undo history); the view binds it and draws the problem lines.                                                                                                                  |
| 11   | done  | Save (Ctrl+S, Cmd+S), Revert, Save all; UTF-8 without BOM; line endings kept; a temporary file, then a move; a failed save keeps the text.                                                                                    |
| 12   | done  | 300 ms after the last change, `validate` with `content`; the newest answer only; a per-file override in the problems panel while unsaved; validation from disk after a save.                                                  |
| 13   | done  | Reload, the reload-or-keep bar, the question before overwriting, "deleted on disk". See decision 3.                                                                                                                           |
| 14   | done  | Tab, project, other project and window ask: save, discard or cancel. `IDialogService` with a fake in tests.                                                                                                                   |
| 15   | done  | View-model tests with `ManualDelay` (no real waiting); headless typing test; real-engine test in a temporary copy of `examples/demo-app`.                                                                                     |

## Findings

How I checked that each test fails without its fix: for findings 3 to 7 I put
the old behaviour back in the product code and ran the tests. For findings 1
and 2 the fault was in the tests themselves, as explained below.

| Finding | Test that proves the fix                                                                                                                                                                                                                                                                                                                                                                                 | Without the fix                                                                                                                          |
| ------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| 1       | `EngineLocatorTests` (8 theories, each run with the Windows rules and the others).                                                                                                                                                                                                                                                                                                                       | The old tests failed on Windows only (review D0001). The new ones use paths with no drive letter, so both rule sets run on every system. |
| 2       | `RealEngineAppTests.Opens_the_demo_project_through_the_real_engine`, which now asserts that the engine was started exactly once.                                                                                                                                                                                                                                                                         | The old test started it twice (seen in the engine log), so that assertion fails.                                                         |
| 3       | `EngineLifecycleTests`: `Two_starts_at_once_leave_exactly_one_engine`, `A_stop_during_a_start_leaves_no_engine_and_the_state_Stopped`, `Start_stop_start_in_one_go_ends_with_one_ready_engine`, `After_close_a_start_does_nothing_even_one_queued_before_the_close`; app side `ReviewD0001FixesTests.After_shutdown_starting_the_engine_does_nothing`.                                                   | Without the queue, all four engine-side tests fail.                                                                                      |
| 4       | `JsonRpcConnectionTests.A_throwing_event_handler_is_reported_and_reading_goes_on`, `A_read_failure_fails_waiting_requests_and_completes_Closed`, `A_result_that_does_not_fit_is_a_protocol_violation`.                                                                                                                                                                                                   | The first two hang without the fix (stopped by the test host's hang timeout).                                                            |
| 5       | `ReviewD0001FixesTests`: `The_engine_stopping_between_openProject_and_the_next_request_ends_in_the_notice_bar`, `A_result_that_does_not_fit_ends_in_the_notice_bar`, `An_unexpected_error_while_opening_is_reported_not_thrown`, `Validation_and_file_change_failures_end_in_the_status_line_and_the_log`; and `EngineLifecycleTests.The_client_of_a_session_that_is_not_ready_is_a_typed_engine_error`. | Without the catch-alls, two of them fail. The other two rest on the new typed exceptions, which did not exist before.                    |
| 6       | `ReviewD0001FixesTests.Batches_during_a_refresh_are_joined_into_one_more_refresh`, `An_older_validation_answer_never_replaces_a_newer_one`.                                                                                                                                                                                                                                                              | Both fail.                                                                                                                               |
| 7       | `ReviewD0001FixesTests.A_changed_action_source_reopens_the_project` (5 cases), `The_watcher_reports_yaml_and_action_sources_outside_ignored_folders`, and `ServicesTests.Project_files_reports_yaml_changes_together` (now with a `.ts` file).                                                                                                                                                           | The three "reopens" cases fail.                                                                                                          |

## Cause of finding 2

The review's reading was right, and here is how I confirmed it. A probe in
the test counted the "Starting …" lines in the engine log after
`window.Show()`: there were **two**. The test had called `InitializeAsync()`
itself, and `Show()` raised `Opened`, whose handler started the engine again.
The second start stops the first engine (and with it the project the test
had opened) and starts another. If it is still running when the test shuts
down, the session ends `Ready` (or `Stopping`, as on Linux in the fifth CI
run).

The race did not happen here in 30 runs of the old test. The probe shows the
double start, and the session fix of finding 3 now makes a start after
shutdown impossible.

The test fix: the window is shown before it gets its view model, so `Opened`
finds no shell to start, and the test asserts that there was exactly one
start.

## Checks

| Command                                            | Result                                                               |
| -------------------------------------------------- | -------------------------------------------------------------------- |
| `apps/desktop/scripts/verify.sh` (each branch)     | pass; 280 tests on the last branch (146 protocol, 48 engine, 86 app) |
| `pnpm verify` (root, each branch)                  | pass, 357 tests                                                      |
| The finding 2 test, 30 runs in a row               | 30 of 30 pass                                                        |
| Engine tests, 10 runs in a row                     | 10 of 10 pass                                                        |
| `git diff --name-only main... -- ':!apps/desktop'` | empty                                                                |
| `git status examples/` after the real-engine tests | clean: the editor test works in a temporary copy                     |

## AvaloniaEdit

- **Version 12.0.0**, MIT. It needs Avalonia ≥ 12.0.0 and works with the
  pinned 12.1.3. The Fluent theme is included from
  `avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml`.
- **Everything asked for worked**: binding `Document`, line numbers, undo and
  redo, a background renderer for the problem lines, caret and scrolling,
  and text typed through Avalonia's headless input.
- **Three things to know:**
  - `TextDocument` may only be used from the thread that created it. The app
    does everything on the UI thread. The tests that use documents run as
    `[AvaloniaFact]`, on the headless UI thread.
  - The package ships no XML documentation.
  - `TopLevel.PlatformSettings` was not accessible from a `UserControl`, so
    the command key (Ctrl or Cmd) comes from `Application.PlatformSettings`.

## Departures from the instruction

1. **Four branches instead of three.** The editor branch came to about 1,880
   lines, so it is split into `desktop/feat/step-file-editor` (tasks 9 to 13)
   and `desktop/feat/unsaved-changes` (task 14), stacked.
2. **A small extra in the session**: stopping a session that had failed now
   moves it to `Stopped`. Without this, an app that shut down after a failed
   start still showed "Engine not running" as a failure.
3. **A test fake fixed for a race**: the in-memory engine of the engine tests
   could exit before writing its refusal of the handshake. It now exits only
   after the answer is written (`FakeEngine.ExitAfterReply`). One test failed
   once because of this, before the fix.

## Decisions I made

1. **Keyboard**: Save is bound to the platform's command key (Ctrl+S, or
   Cmd+S on macOS) on the step file view. It works while the focus is
   anywhere in that tab, including the editor, but not when the focus is in
   the explorer. Save all has a button in the top bar and no shortcut.
2. **"Keep my version"** takes the file as it now is on disk as the new
   baseline, so the next save overwrites it without asking again. The user
   has already decided, so asking twice would be noise.
3. **The app's own save** is recognised by content, not by timing. A tab
   compares the disk with the text it last read or saved, so a watcher event
   for its own save changes nothing. The project is still validated from
   disk, as the instruction asks.
4. **Line endings**: the file's line ending is taken from its first line
   break (`\r\n` or `\n`; `\n` when there is none). On save, every line break
   is written that way, including ones pasted in another style. A file that
   already had mixed endings is written with one style.
5. **A per-file override** in the problems panel replaces every problem of
   that file while its tab has unsaved changes, including those that came
   from `openProject`. Problems the engine reports for other files while
   validating the text (for example a flow) are not shown; they come with
   the next validation from disk.
6. **Reopening the same project**, after a config or action-file change or
   an engine restart, keeps the open tabs, their order, the selected tab and
   the unsaved text, and asks nothing. Opening a different project asks
   first. The instruction did not say what an automatic reopen should do
   with unsaved text; losing it silently seemed wrong.
7. **Default dialogs**: a `ShellViewModel` built without a dialog service
   answers every question "cancel", so nothing is ever lost. The app passes
   the real one.
8. **`IDialogService`** lives on the editor branch, with both questions,
   because the question before overwriting (task 13) needs it. The question
   about unsaved changes is only used from the next branch on.
9. **The status line** still reports the last validation from disk ("no
   problems"), while the problems panel shows the edited text's problems.
   This is visible in screenshot 07. I left it, because the line says
   "Validated N test files", which stays true.
10. **ADR status lines** still say "Proposed" (D0001, D0003, D0004, D0005, and
    the plan), although the owner accepted them on 2026-10-10. `STATUS.md`
    says the next desktop instruction has them updated, and this instruction
    does not ask for it. The new ADR D0006 is Proposed.

## Questions for the owner

1. **Accept ADR D0006 (AvaloniaEdit)?** Recommended: accept. It is the only
   mature code editor for Avalonia, and it works with the pinned version.
2. **A shortcut for Save all** (Ctrl+Shift+S, Cmd+Shift+S)? Recommended: yes,
   in a later instruction. It is small, but nobody asked for it.

## Not done, not pushed, not verified

- **Windows and macOS are not checked** for any branch. The `desktop` CI job
  of #18 is not on these branches. The keyboard shortcut on macOS (Cmd+S) and
  the file watcher on macOS and Windows are untested.
- **The real dialogs** (`AvaloniaDialogService`, small modal windows) are not
  exercised by any test. The view models use the fake. They have not been
  seen on a screen.
- **The app has still not been run on a real display.** Every screen,
  including the editor, was rendered headless only.
- **The pull request for `desktop/feat/unsaved-changes`** is opened right
  after this report is pushed. Its number is in the reply to the owner, not
  in this file.

## Suggestions

- Once #18 is merged, run the `desktop` job on all four pull requests before
  merging. Windows matters most here: the temporary file and the move, and
  how `FileSystemWatcher` reports a replaced file.
- A small "unsaved" marker in the explorer, next to files with unsaved
  changes, would help when many tabs are open.
