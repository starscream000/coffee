# Instruction D0002: review fixes and the step file editor

- Date: 2026-10-09
- Written by: reviewer
- Based on `main` at: `a30b459` or later
- Replaces: none
- Follows review: [D0001](../reviews/D0001-desktop-foundation.md)

## Goal

Pull request #16 is green on Linux, Windows and macOS and can be merged; the
weak spots that review D0001 found in the engine host and the workspace are
fixed and tested; and a step file can be edited and saved in the app, with the
engine's validation shown while typing.

## Owner decisions

1. 2026-10-09: the owner asked the reviewer to check the desktop pull request,
   merge it, and keep the next desktop instruction ready. #16 is not merged
   yet: its C# tests fail on Windows (review D0001, finding 1).
2. **Not decided:** the desktop plan beyond D1 and ADRs D0001, D0003, D0004
   and D0005. This instruction assumes that editing (milestone D4 of the plan)
   comes next, as report D0001 recommended. If the owner decides otherwise,
   this instruction is replaced.
3. One pull request per branch from now on. #16 stays as it is.

## Before you start

The desktop folder's `CLAUDE.md` and handoff README are not on `main` yet; they
are on `desktop/feat/workspace-shell`. So this once, after
`git switch main && git pull --ff-only`, switch to that branch and work from
there. Everything else in those two files applies.

## Branches

| Branch                          | Based on                                   | What it holds         |
| ------------------------------- | ------------------------------------------ | --------------------- |
| `desktop/feat/workspace-shell`  | itself (existing branch, pull request #16) | Tasks 1 to 3          |
| `desktop/fix/review-d0001`      | `desktop/feat/workspace-shell`             | Tasks 4 to 8          |
| `desktop/feat/step-file-editor` | `desktop/fix/review-d0001`                 | Tasks 9 to 15, report |

Add commits; never rewrite what is pushed. Push `desktop/feat/workspace-shell`
as soon as tasks 1 to 3 are done and checked, before starting the next branch,
so that #16 can be reviewed and merged on its own.

Open a pull request for each new branch into `main` and say in its description
which branch it sits on.

If `desktop/feat/step-file-editor` passes roughly 1,500 changed lines (not
counting lock files), split it in two stacked branches and say so in the
report.

## Tasks

The finding numbers are those of review D0001. Each fix comes with a test that
fails without it.

### Part A: make #16 mergeable (`desktop/feat/workspace-shell`)

1. **Take in `main`.** Merge `origin/main` into the branch with a merge commit.
   `apps/desktop/handoff/STATUS.md` will conflict: take `main`'s version, which
   is the reviewer's. Do not change the review or this instruction. `main` now
   holds the engine's instruction 0005 (masking, locators, Playwright), so run
   `pnpm install` and `pnpm build` again before the desktop checks.
2. **Finding 1.** The three `EngineLocatorTests` that fail on Windows. The
   test input must describe the system the test runs on (the executable's
   name, the `PATH` separator, real path shapes). Cover the Windows rules
   (`node.exe`, `;`) and the others (`node`, `:`) in a way that passes on every
   system.
3. **Finding 2.** Find out why
   `RealEngineAppTests.Opens_the_demo_project_through_the_real_engine` ends
   with the engine `Ready` after shutdown. The review's reading: the engine is
   started twice, once by the test and once by the window's `Opened` handler.
   Confirm or correct that, fix the test so it cannot happen, and run that
   test at least 30 times in a row to show it is stable. Say in the report
   what the cause was and how you know. (The product side is task 4.)

Nothing else goes on this branch.

### Part B: fixes (`desktop/fix/review-d0001`)

4. **Finding 3.** Starting and stopping the engine happen one at a time, in
   the order asked. Two starts at once leave exactly one engine. A stop during
   a start leaves no engine and the state `Stopped`. Once the app has begun to
   shut down, a later start does nothing. Test each case with the in-memory
   transport, counting engines started and killed.
5. **Finding 4.** An exception thrown by a handler of `EventReceived` or
   `ProblemReported` does not end the read loop: it is reported as a protocol
   problem and reading goes on. If the loop ends for any other reason, every
   waiting request fails and `Closed` completes.
6. **Finding 5.** No command and no discarded task (`_ = …`) can lose an
   exception. Whatever goes wrong while opening, reopening, listing,
   validating or reacting to file changes ends in the notice bar or the status
   line, with the detail in the engine log. Cover at least: the engine stops
   between `openProject` and the requests that follow it, and a result that
   does not fit its C# type.
7. **Finding 6.** One refresh at a time after file changes. Batches that
   arrive during a refresh are joined into one more refresh, and an answer is
   never applied over a newer one.
8. **Finding 7.** A change to an action source file reopens the project, like
   a change to the config: any file ending in `.ts`, `.mts`, `.cts`, `.js`,
   `.mjs` or `.cjs` under the project root, outside dot-folders and
   `node_modules`. Say so in the desktop README.

### Part C: the step file editor (`desktop/feat/step-file-editor`)

The first part of milestone D4. Whatever file can be opened in a tab today
(from the explorer or from a problem) becomes editable.

9. **The editor control.** Use `Avalonia.AvaloniaEdit`, at one exact version
   that works with the pinned Avalonia, added in `Directory.Packages.props`.
   Record the choice as a desktop ADR (Proposed). If no release works with the
   pinned Avalonia, stop and say so in the report; do not change the Avalonia
   version. No syntax colouring in this instruction, and no package for it.
10. **Editing.** The step file tab holds an editor with line numbers, undo and
    redo, and the problem lines marked as today. Selecting a problem still
    opens the file at its line. The view model owns the text and the state;
    the view only binds.
11. **Saving.** A changed tab is marked in its title. Save (Ctrl+S, Cmd+S on
    macOS) and Revert are commands of the tab; Save all is a command of the
    workspace. Saving writes UTF-8 without a byte-order mark, keeps the line
    endings the file had when it was read, and replaces the file in one step
    (write a temporary file next to it, then move it over), so a crash cannot
    leave half a file. A failed save keeps the text and says why.
12. **Validation while typing.** 300 ms after the last change, send `validate`
    with `content` for that file. Apply only the newest answer; drop answers
    to older text. While a tab has unsaved changes, the problems shown for its
    file are those of the text in the editor, in the tab and in the problems
    panel. After a save, the file is validated from disk as before. The
    app's own save must not show up as a change from outside.
13. **Changes from outside.** When a file changes on disk and its tab has no
    unsaved changes, reload it, as today. When the tab has unsaved changes,
    keep the user's text and show a bar in the tab with two choices: reload
    from disk, or keep this version. Saving over a file that changed on disk
    asks first. A file deleted on disk is said to be deleted; saving creates
    it again.
14. **Closing.** Closing a tab, the project or the window with unsaved changes
    asks: save, discard or cancel. Use a dialog service behind an interface, so
    view models are tested with a fake.
15. **Tests.**
    - View models, without a window: changed state, save, revert, line
      endings, the failed save, the delay before validating (with a clock or
      dispatcher you control, not real waiting), older answers dropped, each
      case of task 13, each answer of task 14.
    - Headless: typing in the editor marks a problem line.
    - Real engine: in a **temporary copy** of `examples/demo-app`, typing an
      invalid step shows the engine's diagnostic, and saving writes the file.
      No test may write inside `examples/`.

## Done when

- [ ] `apps/desktop/scripts/verify.sh` and the root `pnpm verify` pass on every
      branch.
- [ ] `desktop/feat/workspace-shell` holds tasks 1 to 3 and nothing else new,
      and is pushed.
- [ ] Each of findings 1 to 7 has a test that fails without its fix. The
      report names the test for each.
- [ ] The flaky test of finding 2 passed 30 times in a row.
- [ ] A step file can be edited, saved and reverted, and shows the engine's
      problems for the text being typed.
- [ ] Every pull request is open. Root CI (`verify`) is green on each.
- [ ] File header comments and XML documentation on every public type and
      member; no `dynamic`; every `#pragma warning disable` listed in the
      report with its reason.
- [ ] The only package added is `Avalonia.AvaloniaEdit`, at an exact version,
      with the lock files updated.
- [ ] Nothing outside `apps/desktop/` changed.
- [ ] The desktop `CHANGELOG.md` is updated in each branch.
- [ ] `apps/desktop/handoff/reports/D0002-review-fixes-and-the-step-file-editor.md`
      exists on the last branch and follows the root report template.

## Out of scope

- Completion of action and target names, parameter forms, the targets editor
  (the rest of milestone D4).
- Syntax colouring.
- Creating, renaming or deleting files from the app.
- Starting runs, run history, the recorder, packaging.
- Anything outside `apps/desktop/`, including CI. The `desktop` CI job exists
  on the reviewer's branch `chore/desktop-ci` (pull request #18) and reaches
  `main` after #16. Until then the reviewer runs it on your branches at the
  next check; you cannot see Windows or macOS results, so say in the report
  what was checked on Linux only.
- Merging into `main`, pushing to it, or force-pushing anything.

## Report back

- For each branch: its pull request number, the result of both check scripts,
  and its changed-line count.
- For each finding: the test that proves the fix.
- The cause of finding 2.
- The AvaloniaEdit version, and anything in it that did not work with the
  pinned Avalonia.
- Every place where you had to choose because this instruction or a document
  was unclear, wrong or silent.
- Anything in this instruction you think is wrong.
