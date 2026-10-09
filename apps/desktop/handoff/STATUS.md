# Status (desktop)

Written only by the reviewer. The desktop implementer reads this first.

- Updated: 2026-10-10
- **Open instruction:** none. The next one (D0003) is published on the owner's
  word `next`.
- Waiting on: owner

## Instructions

| No.   | Title                                 | State  | Report                                                          | Review                                                          | Pull requests      |
| ----- | ------------------------------------- | ------ | --------------------------------------------------------------- | --------------------------------------------------------------- | ------------------ |
| D0001 | Desktop foundation                    | merged | [D0001](reports/D0001-desktop-foundation.md)                    | [D0001](reviews/D0001-desktop-foundation.md)                    | #16                |
| D0002 | Review fixes and the step file editor | merged | [D0002](reports/D0002-review-fixes-and-the-step-file-editor.md) | [D0002](reviews/D0002-review-fixes-and-the-step-file-editor.md) | #16, #20, #22, #24 |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

## Requests

| No.   | Title                                                                                                            | To       | State                                                     |
| ----- | ---------------------------------------------------------------------------------------------------------------- | -------- | --------------------------------------------------------- |
| R0001 | [A CI job for the desktop app](requests/R0001-ci-job-for-the-desktop-app.md)                                     | reviewer | done: the `desktop` job is on `main` (pull request #18)   |
| R0002 | [Tell the engine track about the desktop track](requests/R0002-tell-the-engine-track-about-the-desktop-track.md) | reviewer | done: the root documents are on `main` (pull request #19) |
| R0003 | [`listTests` before the runner](requests/R0003-list-tests-before-the-runner.md)                                  | reviewer | done: `listTests` is on `main` (pull request #25)         |

## Where the plan stands

Desktop milestone D1 (foundation) is on `main`, and so is the first part of
D4 (editing): step files can be edited, saved and reverted, with the engine's
validation shown while typing. CI builds and tests the app on Linux, Windows
and macOS for every pull request.

Next, in instruction D0003: findings 1 to 5 and 7 of
[review D0002](reviews/D0002-review-fixes-and-the-step-file-editor.md), then
either the rest of D4 (completion, parameter forms, the targets editor) or
runs (D2) once the engine's runner is on `main`.

## Owner decisions on record

- 2026-10-10: the desktop plan is approved. ADRs D0001, D0003, D0004 and D0005
  are accepted. Their status lines, and the plan's, still say "Proposed"; the
  next desktop instruction has them updated.
- 2026-10-10: editing step files comes first for the desktop.
- 2026-10-10: the owner's `merge` for the desktop work covered pull request
  #18, the reviewer's CI job.

## Notes for the next desktop instruction

- The track is on hold on the owner's word (2026-10-10).
- The engine on `main` now runs tests and answers `listTests`, so runs
  (milestone D2) are possible and the explorer's fallback can be removed.
- `RealEngineAppTests` pins the demo config's whole list of environments; it
  should check only what it needs (review 0007 of the engine track, finding 2).
- The owner's notes in the root status file: the app is meant to be low-code
  or no-code, so parameter forms and the targets editor come before
  text-editing extras.

## Waiting on the owner

- Accept ADR D0006 (AvaloniaEdit as the editor)? Recommended: yes.
- A keyboard shortcut for "Save all" (Ctrl+Shift+S, Cmd+Shift+S on macOS)?
  Recommended: yes.
- What the desktop builds after the fixes: the rest of editing, or runs once
  the engine's runner is merged.
- Run the app once on a real screen: after `pnpm build`, with the .NET 10 SDK,
  `dotnet run --project apps/desktop/src/Desktop.App`, then open
  `examples/demo-app`. Nobody has seen it outside headless rendering.
