# Status (desktop)

Written only by the reviewer. The implementer reads the root
[handoff/STATUS.md](../../../handoff/STATUS.md) first; it says in which order
the two tracks' open instructions are carried out.

- Updated: 2026-10-11
- **Open instruction:** none
- Waiting on: owner (his trial of the app, then `next`)

## Instructions

| No.   | Title                                      | State  | Report                                                              | Review                                                              | Pull requests      |
| ----- | ------------------------------------------ | ------ | ------------------------------------------------------------------- | ------------------------------------------------------------------- | ------------------ |
| D0001 | Desktop foundation                         | merged | [D0001](reports/D0001-desktop-foundation.md)                        | [D0001](reviews/D0001-desktop-foundation.md)                        | #16                |
| D0002 | Review fixes and the step file editor      | merged | [D0002](reports/D0002-review-fixes-and-the-step-file-editor.md)     | [D0002](reviews/D0002-review-fixes-and-the-step-file-editor.md)     | #16, #20, #22, #24 |
| D0003 | A complete app, from editing to running    | merged | [D0003](reports/D0003-a-complete-app-from-edit-to-run.md)           | [D0003](reviews/D0003-a-complete-app-from-edit-to-run.md)           | #31 to #41         |
| D0004 | Recording, new projects and a clear layout | merged | [D0004](reports/D0004-recording-new-projects-and-a-clear-layout.md) | [D0004](reviews/D0004-recording-new-projects-and-a-clear-layout.md) | #57 to #66         |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

## Requests

| No.   | Title                                                                                                                  | To       | State                                                     |
| ----- | ---------------------------------------------------------------------------------------------------------------------- | -------- | --------------------------------------------------------- |
| R0001 | [A CI job for the desktop app](requests/R0001-ci-job-for-the-desktop-app.md)                                           | reviewer | done: the `desktop` job is on `main` (pull request #18)   |
| R0002 | [Tell the engine track about the desktop track](requests/R0002-tell-the-engine-track-about-the-desktop-track.md)       | reviewer | done: the root documents are on `main` (pull request #19) |
| R0003 | [`listTests` before the runner](requests/R0003-list-tests-before-the-runner.md)                                        | reviewer | done: `listTests` is on `main` (pull request #25)         |
| R0004 | [The structure of step files through the protocol](requests/R0004-the-structure-of-step-files-through-the-protocol.md) | reviewer | accepted, for later (a protocol addition)                 |
| R0005 | [The browser install command from the engine](requests/R0005-the-browser-install-command-from-the-engine.md)           | reviewer | done: on `main` with protocol 0.1.1 (pull request #56)    |
| R0006 | [Skipped steps that never started](requests/R0006-skipped-steps-that-never-started.md)                                 | reviewer | done: on `main` with protocol 0.1.1 (pull request #56)    |
| R0007 | [An engine test that fails by chance](requests/R0007-a-login-cache-test-that-fails-by-chance.md)                       | reviewer | done: fixed on `main` (pull request #48)                  |
| R0008 | [The environments' addresses in `openProject`](requests/R0008-the-environments-addresses-in-openproject.md)            | reviewer | accepted, for the next engine instruction                 |

## Where the plan stands

On `main` the app has a layout with named regions, a start page with "New
project", recording inside the app with its check, a project tree, a settings
form for tests and a flow picker for `call` steps. It speaks protocol `0.1.1`.

The real-browser tests now run in CI on Linux, Windows and macOS (pull request
#67): a missing browser fails the `desktop` job instead of skipping the tests.

Next, on the owner's `next`: what the owner finds in his trial, and review
D0004's findings 2 (room for a failure in the run view) and 3 (tags cut off
in the Project panel).

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
- 2026-10-10: the separate desktop implementer is let go; the engine
  implementer carries on this track, kept separate from the engine track.
- 2026-10-10: the owner asks for a better user interface with a clear panel
  hierarchy, for "create new project", and for the app to use everything the
  engine has, recording included.

## Waiting on the owner

- **Use the published app once by hand on Windows** (D0004, task 21; the
  steps are in [review D0004](reviews/D0004-recording-new-projects-and-a-clear-layout.md))
  and say what you find. Nobody has used this build in a real window yet.
- Accept ADR D0006 (AvaloniaEdit, the text editor) and ADR D0007 (YamlDotNet,
  the YAML reader)? Recommended: yes to both.
- A keyboard shortcut for "Save all" (Ctrl+Shift+S, Cmd+Shift+S on macOS)?
  Recommended: yes.
