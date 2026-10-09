# Status (desktop)

Written only by the reviewer. The desktop implementer reads this first.

- Updated: 2026-10-10
- **Open instruction:** [D0002: review fixes and the step file editor](instructions/D0002-review-fixes-and-the-step-file-editor.md)
- Waiting on: desktop implementer

The rest of this folder (its README, instruction D0001, report D0001 and the
requests) is on the branch `desktop/feat/workspace-shell` and reaches `main`
with pull request #16. Instruction D0002 says how to start from there.

## Instructions

| No.   | Title                                 | State             | Report                                   | Review                                       | Pull requests |
| ----- | ------------------------------------- | ----------------- | ---------------------------------------- | -------------------------------------------- | ------------- |
| D0001 | Desktop foundation                    | changes requested | on branch `desktop/feat/workspace-shell` | [D0001](reviews/D0001-desktop-foundation.md) | #16 open      |
| D0002 | Review fixes and the step file editor | open              | –                                        | –                                            | continues #16 |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

## Since review D0001

A fifth run of the `desktop` CI job, after `main` (with the engine's
instruction 0005) was merged into the reviewer's CI branch:

- Linux: the test of finding 2 failed again, this time with "Expected:
  Stopped, Actual: Stopping". So it is not a macOS problem, and it fits the
  review's reading that a start and a stop overlap.
- Windows: only the three tests of finding 1 fail.
- macOS: everything passes.

The desktop tests otherwise pass against the engine now on `main`.

## Requests

The request files are on the branch too. The answers are in
[review D0001](reviews/D0001-desktop-foundation.md) and are copied into the
files once #16 is merged.

| No.   | Title                                         | To       | State                                                                   |
| ----- | --------------------------------------------- | -------- | ----------------------------------------------------------------------- |
| R0001 | A CI job for the desktop app                  | reviewer | accepted; the job is in pull request #18, to be merged after #16        |
| R0002 | Tell the engine track about the desktop track | reviewer | accepted; root handoff README done, the rest in engine instruction 0006 |
| R0003 | `listTests` before the runner                 | reviewer | accepted as option 2; arrives with engine instruction 0006              |

## Where the plan stands

Desktop milestone D1 (foundation) is reviewed and waits for two test fixes
before it is merged. The owner approved the plan for D2 onward. Instruction
D0002 starts milestone D4 (editing), which needs nothing new from the engine;
runs (D2) follow once the engine's runner is on `main`.

## Owner decisions on record

- 2026-10-10: the desktop plan is approved. ADRs D0001, D0003, D0004 and D0005
  are accepted. Their status lines, and the plan's, still say "Proposed" on
  the branch; the next desktop instruction has them updated.
- 2026-10-10: editing step files comes next for the desktop, as instruction
  D0002 assumes. D0002 stands as published.

## Waiting on the owner

- Run the app once on a real screen: after `pnpm build`, with the .NET 10 SDK,
  `dotnet run --project apps/desktop/src/Desktop.App`, then open
  `examples/demo-app`. Nobody has seen it outside headless rendering.
- Say `merge` for pull request #18 (the desktop CI job) once #16 is merged.
