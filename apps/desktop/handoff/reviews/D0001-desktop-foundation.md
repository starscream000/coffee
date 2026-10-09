# Review D0001: desktop foundation

- Date: 2026-10-09
- Written by: reviewer
- Instruction: `apps/desktop/handoff/instructions/D0001-desktop-foundation.md`
  on the branch `desktop/feat/workspace-shell`
- Report: `apps/desktop/handoff/reports/D0001-desktop-foundation.md` on the
  same branch (both reach `main` when pull request #16 is merged)
- Verdict: **Changes requested**. The work is good and close; two test
  problems keep it from being merged today.

## Pull requests

| Pull request                                           | Branch                         | Reviewed at | CI                                                                                         | Verdict           |
| ------------------------------------------------------ | ------------------------------ | ----------- | ------------------------------------------------------------------------------------------ | ----------------- |
| [#16](https://github.com/starscream000/coffee/pull/16) | `desktop/feat/workspace-shell` | `457e6ac`   | `verify` green on three systems. `desktop` (new, from #18): red on Windows, flaky on macOS | Changes requested |

`merge` is skipped for #16. The rule is that a pull request is merged only
after CI is green on Linux, Windows and macOS, and the first CI run of the C#
code is not.

## What I checked

- **Scope.** All 119 changed files are under `apps/desktop/`.
- **The root checks.** `pnpm verify` passes with the desktop files in place
  (Prettier covers their Markdown and JSON); CI `verify` is green on three
  systems.
- **The C# build and tests, on three systems, for the first time.** I cannot
  build .NET in my workspace, so I added the `desktop` job that request R0001
  asks for, on the branch `chore/desktop-ci`
  ([#18](https://github.com/starscream000/coffee/pull/18)), stacked on this
  work, and ran it four times:

  | System  | Format and build (warnings are errors) | Tests                                                 |
  | ------- | -------------------------------------- | ----------------------------------------------------- |
  | Linux   | pass, 4 of 4                           | pass 3 of 4; one failure I could not read (see below) |
  | macOS   | pass, 4 of 4                           | pass 3 of 4; one failure, finding 2                   |
  | Windows | pass, 4 of 4                           | fail 4 of 4; three tests, finding 1                   |

  I could read the failed tests in the last two runs only; on Windows they
  were the same three both times. Every other test passes on Windows and
  macOS, including the ones that start the real engine and the ones that open
  the window headless. That is the first evidence that the app works beyond
  Linux.

- **The source I read line by line:** all of `Desktop.Engine` except the
  request wrappers of `EngineClient` (signatures only); the line reader of
  `Desktop.Protocol`; in `Desktop.App`, the file service and watcher, the
  engine service, and the shell, workspace and engine-status view models; the
  main window's code-behind; the two test files behind findings 1 and 2.
- **The documents:** the folder's `CLAUDE.md`, the handoff README, the plan,
  the four proposed ADRs (decision and alternatives), the three requests.

## What I could not check

- **Most of the tests, the views (`.axaml`) and the C# protocol records.** I
  did not read them. For the records I rely on the contract tests, which
  compare them with the JSON Schema files and pass on three systems.
- **One Linux test failure.** In the second run the job failed in
  `dotnet test`, and my first version of the step that reports failed tests
  did not work, so the name of the test is lost. GitHub's log storage cannot be
  reached from my workspace. It is probably finding 2.
- **The app on a real screen.** Nobody has seen it outside headless
  rendering; the report says so too. Native dialogs, window chrome and fonts
  are unverified on every system.
- The screenshots the report mentions; they are not in the repository.

## Findings

1. **Must fix before merging. Three tests fail on Windows.**
   `EngineLocatorTests` (`Finds_the_engine_of_the_checkout_and_node_on_the_PATH`,
   `Settings_come_before_the_environment_variable_which_comes_before_the_checkout`,
   `A_configured_engine_that_does_not_exist_is_skipped_and_logged`). The test
   input says `IsWindows = false` but builds its paths from the real temporary
   folder, so on Windows the `PATH` value `C:\Users\…\repo\tools` is split at
   the colon and Node is "not found". The product code is right; the tests are
   not. Make the test input follow the system it runs on (`node.exe`, `;`), or
   use made-up paths for both systems and run both cases everywhere.
2. **Must fix before merging. `RealEngineAppTests` fails now and then**
   ("Expected: Stopped, Actual: Ready" at the last line; seen on macOS, 1 run
   in 4). From reading the code, the cause is that the engine is started
   twice: the test calls `shell.InitializeAsync()` itself, and then
   `window.Show()` raises `Opened`, whose handler in `MainWindow.axaml.cs`
   calls `InitializeAsync()` again. That second start is still running when
   the test shuts the engine down, and finishes afterwards. Confirm this, then
   fix both sides of it: the test, and finding 3.
3. **Should fix. Starting and stopping the engine are not serialised.**
   `EngineSession.StartAsync` and `StopAsync` can run at the same time (start
   twice, restart from Settings while a project opens, close the window while
   the engine starts). The session can then end up `Ready` after a shutdown, or
   with two engine processes. One operation at a time; a start requested after
   the app began shutting down does nothing.
4. **Should fix. A subscriber that throws ends the connection silently.** In
   `JsonRpcConnection.ReadLoopAsync`, an exception from an `EventReceived` or
   `ProblemReported` handler leaves the loop. Nothing reads the engine's
   output after that: waiting requests never finish and `Closed` never
   completes. Catch it, report it as a protocol problem, and go on; and make
   any other end of the loop fail the waiting requests and complete `Closed`.
5. **Should fix. Errors other than `EngineException` escape the commands.**
   `EngineSession.Client` throws `InvalidOperationException` when the engine
   is not ready, and a result that does not fit throws `JsonException`.
   `ShellViewModel.OpenProjectAsync`, `WorkspaceViewModel.ValidateAsync` and
   `OnFilesChangedAsync` catch only `EngineException`, and two of the callers
   discard the task (`_ = …`). An engine that stops between two requests
   therefore gives an unobserved exception instead of the notice bar.
6. **Should fix. An older answer can overwrite a newer one.** File-change
   batches each start their own list-and-validate, and nothing orders the
   answers. Run one refresh at a time and apply only the newest.
7. **Should fix. A changed action file is not noticed.** The watcher reports
   YAML files only, so after editing `actions/*.ts` the catalogue and the
   problems stay as they were until the project is reopened by hand. The
   engine reloads actions on `openProject`.
8. **Note. Decision 7 of the report is out of date, in a good way.** The
   engine now masks secrets in what its console writes to stderr (review 0005
   of the engine track).

## Rulings on the report's "Decisions I made"

| No. | Decision                                              | Ruling                                                                                                     |
| --- | ----------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| 1   | .NET 10 and Avalonia 12.1.3 (ADR D0001)               | **Owner decides**; recommended. The packages restore in locked mode on three systems.                      |
| 2   | Hand-written protocol types with contract tests       | **Owner decides** (ADR D0003); recommended. The root protocol document changes its sentence once accepted. |
| 3   | Unknown enum values read as `Unknown`                 | Accepted.                                                                                                  |
| 4   | Requests without parameters send `{}`                 | Accepted.                                                                                                  |
| 5   | Branch names `desktop/<type>/<name>`                  | Accepted.                                                                                                  |
| 6   | No editor component yet                               | Accepted; instruction D0002 adds it.                                                                       |
| 7   | Engine stderr is shown as is                          | Accepted; see finding 8.                                                                                   |
| 8   | Settings file in the system's application data folder | Accepted.                                                                                                  |

The departures are accepted: four branches instead of three, the oversized
`desktop/feat/protocol-client` (splitting it would rewrite pushed history), and
the bootstrapped instruction and status file. From this review on, the reviewer
writes both.

One pull request for the whole stack (question 2 of the report) is accepted
for this first delivery. From D0002 on: one pull request per branch, as in the
root process.

## Answers to the requests

The request files are on the unmerged branch, so the answers are here; I copy
them into the files' "Answer" sections once #16 is on `main`.

- **R0001, a CI job for the desktop app: accepted, in progress.** The job is on
  `chore/desktop-ci` (#18) as proposed, plus a step that shows each failed
  test on the pull request. It can only be merged after #16, and needs the
  owner's word like any change outside `handoff/`. No `paths` filter for now:
  the job takes a few minutes and a filter is one more thing to get wrong.
- **R0002, tell the engine track about the desktop track: accepted.** The root
  `handoff/README.md` is updated with this review. The root `CLAUDE.md` and
  `README.md` are changed by the engine implementer in instruction 0006.
- **R0003, `listTests` before the runner: accepted as option 2.** The plan
  stays as it is. Instruction 0006 of the engine track covers plan branches 8
  and 9, so `listTests` arrives with it, complete with `rows`. The desktop
  keeps its fallback until then.

## Owner decisions needed

1. **The desktop plan and ADRs D0001, D0003, D0004 and D0005.** All are
   "Proposed". Recommended: approve them. Each choice is conventional and the
   alternatives are fairly stated.
2. **What the desktop does next.** Recommended, and assumed by instruction
   D0002: editing step files (milestone D4 of the plan), because it needs
   nothing new from the engine. Runs (D2) follow once the engine's runner is
   on `main`.
3. **Try the app once on a real screen.** After `pnpm build`, with the .NET 10
   SDK installed: `dotnet run --project apps/desktop/src/Desktop.App`, then
   open `examples/demo-app`.

## For the next instruction

Instruction D0002:

- On `desktop/feat/workspace-shell` (pull request #16): take in `main`, then
  findings 1 and 2. Nothing else on that branch, so it can be merged as soon
  as it is green.
- A fix branch for findings 3 to 7.
- The step file editor: edit, save, and validation while typing.
