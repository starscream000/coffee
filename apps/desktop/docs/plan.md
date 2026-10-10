# Desktop plan

> Status: **Accepted (owner, 2026-10-10)**. Milestone D1 and the text editor
> of D4 are on `main`. [Instruction D0003](../handoff/instructions/D0003-a-complete-app-from-edit-to-run.md)
> is in pull requests (see its [report](../handoff/reports/D0003-a-complete-app-from-edit-to-run.md)):
> runs (D2), run history (D3, without comparing runs and flaky or slow tests),
> the rest of D4 (step list, step forms, targets editor, new files; without
> completion and colouring in the text editor) and the place of recording
> (D5). The owner wants the app low-code, even no-code: a tester creates,
> edits and runs tests without writing YAML, which stays the source of truth
> underneath.

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

The repository's plan puts the desktop app at product version v0.3.0. The
engine's runner is on `main`, so D2 is built against real events. Where the
engine is not ready (screenshots and page states, most built-in actions, the
recorder), the app shows an honest placeholder in the place the feature will
take, so the layout does not move when the engine catches up.

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
- **The engine is still growing.** Screenshots and page states, most
  built-in actions and the recorder are not in the engine yet; the app shows
  placeholders for them where they will go.
