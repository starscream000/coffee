# Report D0001: desktop foundation

- Date: 2026-10-09
- Written by: desktop implementer
- Instruction: [D0001](../instructions/D0001-desktop-foundation.md)

## Summary

All thirteen tasks are done, on four stacked branches instead of three (the
workspace shell was split for size). The app starts the engine of the
checkout, opens a project folder through it, and shows the tests, the
problems (re-checked when files change), the step files and the action
catalogue. It cannot run tests yet because the engine's runner does not exist.
Nothing outside `apps/desktop/` changed. Four things are needed next: the owner's
answers to the questions below, a review, the three
[requests](../requests/). The owner opened one pull request for the whole
stack: [#16](https://github.com/starscream000/coffee/pull/16).

## Branches and pull requests

Stacked; merge in this order. Line counts leave out the lock files.

| Branch                         | Last commit                                   | Pull request | Pushed | Changed lines (of which tests; docs) |
| ------------------------------ | --------------------------------------------- | ------------ | ------ | ------------------------------------ |
| `desktop/chore/scaffold`       | `815d2c8`                                     | #16          | yes    | +1,553 −7 (180; 1,001 docs)          |
| `desktop/feat/protocol-client` | `72c51f3`                                     | #16          | yes    | +3,865 −18 (1,286)                   |
| `desktop/feat/app-view-models` | `6902f3e`                                     | #16          | yes    | +1,983 (512)                         |
| `desktop/feat/workspace-shell` | the commit that adds this report (branch tip) | #16          | yes    | +1,583 −18 (475), before this report |

`desktop/feat/protocol-client` is far over the 1,500-line guideline, and I
noticed only after pushing it. Splitting it now would rewrite pushed history,
so it stays one branch. It reviews well in two halves by folder:
`Desktop.Protocol` with its tests (+2,020) and `Desktop.Engine` with its tests
(+1,834). The protocol half is long but repetitive: one documented record per
message.

CI: the repository's CI checks only the pnpm side, so no C# is checked in CI
yet ([request R0001](../requests/R0001-ci-job-for-the-desktop-app.md)).

## Tasks

| Task | State | Notes                                                                                                                                                                                                                                                                                                                                    |
| ---- | ----- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | done  | `Desktop.slnx`: Protocol, Engine, App, one xUnit project each. `Directory.Build.props` (nullable, warnings as errors, analyzers, XML docs), `Directory.Packages.props` (exact versions), lock files, `.editorconfig`, `scripts/verify.sh` and `.ps1`.                                                                                    |
| 2    | done  | `Product` in `Desktop.Protocol`; `ProductTests` reads `product.ts` and fails when they differ.                                                                                                                                                                                                                                           |
| 3    | done  | README, `CLAUDE.md` for the folder, [architecture](../../docs/architecture.md), [plan](../../docs/plan.md) (D1 to D7), ADRs D0001 to D0005, CHANGELOG.                                                                                                                                                                                   |
| 4    | done  | This folder: README, STATUS (bootstrapped), templates, requests R0001 to R0003.                                                                                                                                                                                                                                                          |
| 5    | done  | One record per schema file (44), registered by schema key. The contract tests check names, optionality, enum values, nested types, examples read and written back valid, and the version in the schema titles. Mutation check: renaming `Diagnostic.Hint` fails the contract test of every schema that uses `Diagnostic` or `ErrorInfo`. |
| 6    | done  | `MessageLineReader`/`Writer`: split chunks, CRLF, multi-byte characters across chunks, empty lines, over-limit lines discarded both ways. `ProtocolVersion.IsCompatible` and `ErrorCodes` are checked against `version.ts`, `errors.ts` and `limits.ts`.                                                                                 |
| 7    | done  | `EngineLocator` (ADR D0004), `EngineProcess`, `JsonRpcConnection`, `EngineClient`, `EngineSession` with states and crash detection.                                                                                                                                                                                                      |
| 8    | done  | 20 tests against an engine in memory, 10 against the real engine (handshake, openProject, validate files and content, listActions, listTests, ProjectNotOpen, ProjectInvalid, refusal with exit code 3, shutdown with exit code 0).                                                                                                      |
| 9    | done  | Start page with recent projects (missing folders greyed out, "Forget"), the folder dialog, notices.                                                                                                                                                                                                                                      |
| 10   | done  | Folder tree, search by name or path, tag filter, problem counts per folder. Falls back to `*.test.yaml` files while the engine answers "method not found" ([R0003](../requests/R0003-list-tests-before-the-runner.md)).                                                                                                                  |
| 11   | done  | Problems from `openProject` and `validate`, sorted, with hints; selecting one opens the file at the line. YAML changes re-validate; a config change reopens the project.                                                                                                                                                                 |
| 12   | done  | Read-only step file view with line numbers and problem lines marked; action catalogue with search, short form, parameters read from the JSON Schema, and the raw schema.                                                                                                                                                                 |
| 13   | done  | Engine status in the top bar and on the start page; engine log (app, stderr, protocol problems; last 2,000 lines); settings panel for the Node and engine paths, which restarts the engine.                                                                                                                                              |

## Checks

All run on Linux (Ubuntu 24.04, .NET SDK 10.0.112, Node 24.11.0) on
`desktop/feat/workspace-shell`, and on each branch before its push.

| Command                                            | Result                                                                                   |
| -------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| `apps/desktop/scripts/verify.sh`                   | pass: format, build (no warnings), 212 tests (146 protocol, 30 engine, 36 app), Prettier |
| `pnpm verify` (root)                               | pass: 286 tests                                                                          |
| `git diff --name-only main... -- ':!apps/desktop'` | empty: nothing outside `apps/desktop/` changed                                           |

The real-engine tests need `pnpm build` at the root; without it they are
skipped, and with `DESKTOP_TESTS_REQUIRE_ENGINE=1` (proposed for CI) they fail
instead. All of them ran and passed here.

## Departures from the instruction

1. **Four branches instead of three.** The workspace shell (tasks 9 to 13)
   came to 3,400 lines, so it is split into `desktop/feat/app-view-models`
   (services and the panels' view models, tested without a window) and
   `desktop/feat/workspace-shell` (the workspace and shell view models, the
   views and the UI tests).
2. **`desktop/feat/protocol-client` is over the size guideline**, as explained
   above.
3. **The instruction itself was written by me**, transcribing the owner's
   brief, because there was no reviewer instruction yet (see
   [Bootstrapping](../README.md#bootstrapping)). Likewise `STATUS.md`.

## Decisions I made

1. **.NET 10 and Avalonia 12.1.3** (ADR D0001, Proposed). .NET 8 support ends
   in November 2026.
2. **Hand-written protocol types with contract tests** instead of code
   generation (ADR D0003, Proposed). `docs/protocol.md` says the C# client
   uses the schemas "for code generation and contract tests". If D0003 is
   accepted, that sentence should drop "code generation"; I will raise a
   request then.
3. **Unknown enum values read as `Unknown`** instead of failing the message.
   The protocol calls new enum values compatible only for enums "documented
   as open", and documents none as open. Being tolerant costs nothing and
   keeps an older app working with a newer engine.
4. **Requests without parameters send `{}`** (`shutdown`, `listActions`).
   `docs/protocol.md` says "No params"; the schema allows either, and the
   engine accepts `{}`.
5. **Branch names** `desktop/<type>/<name>` (ADR D0002), so the two tracks'
   branches are easy to tell apart.
6. **No editor component yet.** The step file view is a virtualised list.
   AvaloniaEdit (12.0.0 exists) can come with editing in D4, where it is
   needed.
7. **Engine stderr is shown as is.** Until review 0004's finding 4 is fixed
   on the engine side, the engine's stderr may contain unmasked secrets, and
   the engine log panel shows them. The panel is local to the user's machine
   and nothing is written to disk.
8. **The settings file** lives in the application data folder under the
   product's display name: `%APPDATA%\<name>\desktop-settings.json` on
   Windows, `~/.config/<name>/` on Linux and macOS.

## Questions for the owner

1. **Approve the desktop plan and ADRs?** The [plan](../../docs/plan.md) (D2
   runs, D3 run history, D4 editing, D5 recording, D6 packaging, D7 release)
   and ADRs D0001, D0003, D0004 and D0005 are Proposed. Recommended: approve
   them, and make D2 (runs) wait for engine plan branch 8, so the live run view
   is built against real events; until then, D4 (editing) can go first,
   because it needs nothing new from the engine.
2. **One pull request or four?** [#16](https://github.com/starscream000/coffee/pull/16)
   merges the whole stack (head `desktop/feat/workspace-shell`) as one merge
   commit; the four branches stay as the commit history inside it. The root
   process uses one pull request per branch, which keeps each feature visible
   as its own merge. Recommended: keep #16 as it is; the commits match the
   branches one to one, so review can still go branch by branch.

## Not done, not pushed, not verified

- **Not checked on Windows or macOS.** There is no CI job for C#
  ([R0001](../requests/R0001-ci-job-for-the-desktop-app.md)), and only Linux
  was available. Windows-specific code paths (`node.exe`, `;` in PATH) are
  tested only through the locator's fake file system.
- **The app was never shown on a real display.** This environment has none.
  Every screen was rendered with Avalonia's headless platform and Skia, and
  the screenshots checked by eye (start page, open project, step file with a
  problem, engine not found, settings, and the demo app through the real
  engine). Native folder dialogs, window chrome and fonts on each system are
  not verified.
- **File watching was tested on Linux only.** FileSystemWatcher behaves
  differently on macOS (FSEvents) and Windows.

## Suggestions

- Root CI could run the desktop job only when `apps/desktop/`,
  `packages/protocol/` or `packages/engine/` change (noted in R0001).
- When the engine adds a `listTests` that works before data rows exist
  (R0003), the explorer's fallback and its notice can be deleted. A test
  already covers both paths.
