# Desktop plan

> Status: **Proposed** (2026-10-09). Needs the owner's go-ahead before work
> beyond milestone D1 starts. Milestone D1 is the owner's brief of 2026-10-09
> ([instruction D0001](../handoff/instructions/D0001-desktop-foundation.md)).

The desktop app is a test-suite management client for the engine. It never
runs tests itself: everything it knows about a project, a test or a run comes
from the engine over the protocol, or from the files the engine writes
(`docs/protocol.md`, ADR 0015 of the repository). So each desktop milestone
can only go as far as the engine allows; the table says what each one waits
for.

## Milestones

| #   | Milestone      | Delivers                                                                                                                                                                                                                                                                           | Needs from the engine                                                     |
| --- | -------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| D1  | Foundation     | Solution, tooling, docs and handoff; the protocol in C# with contract tests; the engine host (start, handshake, requests, events, crash handling); the workspace shell: start page, open project, test explorer, problems, step file view, action catalogue, engine log, settings. | Nothing new: `initialize`, `openProject`, `validate`, `listActions`.      |
| D2  | Runs           | Choose tests (selection, tags), environment and browser; start and cancel a run; live run view: tests and steps as they run, errors with expected and actual, locator fallbacks, screenshots per step, open a step's snapshot; a run summary.                                      | `startRun`, the events, `cancelRun`, `openSnapshot` (plan branches 8–14). |
| D3  | Run history    | Every run in `.cfe/runs/` listed and reopened from its `events.ndjson`, with the same views as a live run; compare a test across runs; flaky and slow tests; locators that needed a fallback, gathered across runs.                                                                | The run folder of ADR 0015 (plan branch 9).                               |
| D4  | Editing        | Edit step files with validation as you type (`validate` with `content`), action and target completion from `listActions` and the targets files, a form view for a step's parameters from its `paramsSchema`, a targets editor showing the candidates in order.                     | Nothing new.                                                              |
| D5  | Recording      | Start the recorder, see steps arrive, pick targets in the test browser, save into a step file.                                                                                                                                                                                     | The recorder and its protocol (engine v0.2.0, not yet designed).          |
| D6  | Packaging      | One installer per system with a Node runtime and the engine build inside (ADR 0009 of the repository); first-run checks; updates.                                                                                                                                                  | A distributable engine build.                                             |
| D7  | Release v0.3.0 | Protocol `1.0.0` together with the engine; docs status lines; release notes.                                                                                                                                                                                                       | Protocol 1.0.0.                                                           |

The repository's plan puts the desktop app at product version v0.3.0. D1, D3
(reading finished run folders) and D4 can be built before that; D2 follows the
engine's runner branch by branch, so the live run view is tried against real
events as soon as they exist.

## How each milestone is cut

- One branch per unit of work, named `desktop/<type>/<name>`, small enough to
  review in one sitting (roughly 1,500 changed lines, not counting lock files
  and generated files).
- Each branch keeps the desktop checks and the root `pnpm verify` green.
- New NuGet packages are added only in the branch that first needs them, at an
  exact version, in `Directory.Packages.props`.

## Risks

- **The protocol is still `0.x`.** A minor engine release may break the
  client. The contract tests read the committed JSON Schemas, so a protocol
  change fails the desktop tests in the same pull request that makes it, once
  CI runs them ([request R0001](../handoff/requests/R0001-ci-job-for-the-desktop-app.md)).
- **No CI for C# yet.** Until R0001 is done, Windows and macOS are not checked.
- **Test explorer before `listTests`.** The engine answers `listTests` with
  "method not found" until its plan branch 9. D1 shows test files found on
  disk in the meantime, without names or tags
  ([request R0003](../handoff/requests/R0003-list-tests-before-the-runner.md)).
