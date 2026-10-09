# Status (desktop)

Written only by the reviewer. The desktop implementer reads this first.

- Updated: 2026-10-10
- **Open instruction:** [D0003: a complete app, from editing to running](instructions/D0003-a-complete-app-from-edit-to-run.md)
- Waiting on: desktop implementer

## Instructions

| No.   | Title                                   | State  | Report                                                          | Review                                                          | Pull requests      |
| ----- | --------------------------------------- | ------ | --------------------------------------------------------------- | --------------------------------------------------------------- | ------------------ |
| D0001 | Desktop foundation                      | merged | [D0001](reports/D0001-desktop-foundation.md)                    | [D0001](reviews/D0001-desktop-foundation.md)                    | #16                |
| D0002 | Review fixes and the step file editor   | merged | [D0002](reports/D0002-review-fixes-and-the-step-file-editor.md) | [D0002](reviews/D0002-review-fixes-and-the-step-file-editor.md) | #16, #20, #22, #24 |
| D0003 | A complete app, from editing to running | open   | –                                                               | –                                                               | –                  |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

## Requests

| No.   | Title                                                                                                            | To       | State                                                     |
| ----- | ---------------------------------------------------------------------------------------------------------------- | -------- | --------------------------------------------------------- |
| R0001 | [A CI job for the desktop app](requests/R0001-ci-job-for-the-desktop-app.md)                                     | reviewer | done: the `desktop` job is on `main` (pull request #18)   |
| R0002 | [Tell the engine track about the desktop track](requests/R0002-tell-the-engine-track-about-the-desktop-track.md) | reviewer | done: the root documents are on `main` (pull request #19) |
| R0003 | [`listTests` before the runner](requests/R0003-list-tests-before-the-runner.md)                                  | reviewer | done: `listTests` is on `main` (pull request #25)         |

## Where the plan stands

Desktop milestone D1 (foundation) and the first part of D4 (the text editor)
are on `main`. Instruction D0003 makes the app complete from editing to
running: runs (D2), run history (D3), building steps and targets without YAML
(the rest of D4), and a placeholder for recording (D5).

## Owner decisions on record

- 2026-10-10: the desktop plan is approved. ADRs D0001, D0003, D0004 and D0005
  are accepted. Their status lines, and the plan's, still say "Proposed"; the
  next desktop instruction has them updated.
- 2026-10-10: editing step files comes first for the desktop.
- 2026-10-10: the owner's `merge` for the desktop work covered pull request
  #18, the reviewer's CI job.
- 2026-10-10: the track resumes. The owner wants a workable end-to-end desktop
  app now, with placeholders where the engine is not ready. The app is meant
  to be low-code or no-code.

## Waiting on the owner

- Accept ADR D0006 (AvaloniaEdit as the editor)? Recommended: yes.
- A keyboard shortcut for "Save all" (Ctrl+Shift+S, Cmd+Shift+S on macOS)?
  Recommended: yes.
