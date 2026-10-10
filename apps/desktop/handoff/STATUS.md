# Status (desktop)

Written only by the reviewer. The desktop implementer reads this first.

- Updated: 2026-10-10
- **Open instruction:** none. The next one (D0004) is published on the owner's
  word `next`.
- Waiting on: owner

## Instructions

| No.   | Title                                   | State  | Report                                                          | Review                                                          | Pull requests      |
| ----- | --------------------------------------- | ------ | --------------------------------------------------------------- | --------------------------------------------------------------- | ------------------ |
| D0001 | Desktop foundation                      | merged | [D0001](reports/D0001-desktop-foundation.md)                    | [D0001](reviews/D0001-desktop-foundation.md)                    | #16                |
| D0002 | Review fixes and the step file editor   | merged | [D0002](reports/D0002-review-fixes-and-the-step-file-editor.md) | [D0002](reviews/D0002-review-fixes-and-the-step-file-editor.md) | #16, #20, #22, #24 |
| D0003 | A complete app, from editing to running | merged | [D0003](reports/D0003-a-complete-app-from-edit-to-run.md)       | [D0003](reviews/D0003-a-complete-app-from-edit-to-run.md)       | #31 to #41         |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

## Requests

| No.   | Title                                                                                                                  | To       | State                                                     |
| ----- | ---------------------------------------------------------------------------------------------------------------------- | -------- | --------------------------------------------------------- |
| R0001 | [A CI job for the desktop app](requests/R0001-ci-job-for-the-desktop-app.md)                                           | reviewer | done: the `desktop` job is on `main` (pull request #18)   |
| R0002 | [Tell the engine track about the desktop track](requests/R0002-tell-the-engine-track-about-the-desktop-track.md)       | reviewer | done: the root documents are on `main` (pull request #19) |
| R0003 | [`listTests` before the runner](requests/R0003-list-tests-before-the-runner.md)                                        | reviewer | done: `listTests` is on `main` (pull request #25)         |
| R0004 | [The structure of step files through the protocol](requests/R0004-the-structure-of-step-files-through-the-protocol.md) | reviewer | accepted, for later (a protocol addition)                 |
| R0005 | [The browser install command from the engine](requests/R0005-the-browser-install-command-from-the-engine.md)           | reviewer | accepted, for later, with R0004                           |
| R0006 | [Skipped steps that never started](requests/R0006-skipped-steps-that-never-started.md)                                 | reviewer | accepted, for later, with R0004                           |
| R0007 | [An engine test that fails by chance](requests/R0007-a-login-cache-test-that-fails-by-chance.md)                       | reviewer | accepted; in the engine track's next instruction          |

## Where the plan stands

The app is complete from editing to running on `main`: create a test, build
its steps and targets without YAML, run it in a browser, watch it, read a
failure, reopen earlier runs. Recording has a placeholder. Nobody has used the
screens yet; the owner's first impressions shape the next instruction.

One test fails on Windows when the real-browser tests are switched on (review
D0003, finding 1). Pull request #42, which gives the `desktop` CI job a
browser, waits for that fix.

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

- Try the app and say what is wrong or missing.
- Accept ADR D0006 (AvaloniaEdit, the text editor) and ADR D0007 (YamlDotNet,
  the YAML reader)? Recommended: yes to both.
- A keyboard shortcut for "Save all" (Ctrl+Shift+S, Cmd+Shift+S on macOS)?
  Recommended: yes.
- Say `next` to publish instruction D0004 (the Windows test fix, build
  telemetry off, and your corrections).
