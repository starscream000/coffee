# Desktop architecture

> Status: **Proposed** (2026-10-09). Describes milestone D1 as built and the
> shape the later milestones of the [plan](plan.md) will fill in. Decisions
> with alternatives are in the [decision records](adr/).

The desktop app is a client of the engine, like the CLI. It follows the
repository's [architecture](../../../docs/architecture.md): it starts the
engine as a separate process, talks to it only through the
[protocol](../../../docs/protocol.md), and reads the files the engine writes
(runs, screenshots). It never references engine code and never parses step
files to decide what they mean: when it needs to know something about a
project, it asks the engine.

## Layers

```
┌───────────────────────────────────────────────────────────────┐
│ Desktop.App (Avalonia)                                        │
│   Views (.axaml)  ──bind──▶  ViewModels  ──▶  Services        │
│                                              (dialogs, files, │
│                                               settings, clock)│
└───────────────────────────────┬───────────────────────────────┘
                                │ EngineSession, EngineClient
┌───────────────────────────────▼───────────────────────────────┐
│ Desktop.Engine                                                │
│   EngineLocator ─▶ EngineProcess ─▶ JsonRpcConnection          │
│                                      └▶ EngineClient (typed)   │
└───────────────────────────────┬───────────────────────────────┘
                                │ message types, framing, JSON
┌───────────────────────────────▼───────────────────────────────┐
│ Desktop.Protocol                                              │
│   Product, ProtocolVersion, ErrorCodes, messages, events,     │
│   ProtocolJson, LineFraming                                   │
└───────────────────────────────────────────────────────────────┘
          │ stdin/stdout (JSON-RPC, one message per line)
          ▼
   engine process: node <engine>/dist/main.js --stdio
```

| Project            | Depends on                 | Job                                                                                                                                                                     |
| ------------------ | -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Desktop.Protocol` | .NET base library only     | The protocol in C#: every request, result, event and shared type; the version rule; error codes; JSON settings; newline framing with the 4 MiB limit. The C# `Product`. |
| `Desktop.Engine`   | `Desktop.Protocol`         | Finds Node and the engine, starts and stops the process, runs the handshake, sends typed requests, raises typed events, captures stderr, reports crashes.               |
| `Desktop.App`      | `Desktop.Engine`, Avalonia | Windows, view models and app services.                                                                                                                                  |

The dependency rule is enforced by tests (`DependencyRuleTests` in each test
project): `Desktop.Protocol` references nothing but the base library, and
`Desktop.Engine` references `Desktop.Protocol` only, so no user interface code
can leak into the engine host.

## The protocol in C#

[ADR D0003](adr/0003-hand-written-protocol-types.md) explains why the message
types are written by hand and how they are kept honest:

- One C# type per JSON Schema file in `packages/protocol/schema/`, registered
  under the schema's key (`request.openProject.result`, `event.stepFailed`, …).
- Contract tests read the schemas and the protocol examples. They fail when a
  schema has no C# type, when a schema property is missing from its C# type
  (or the other way round), when an example does not read into its C# type, or
  when what the C# type writes does not validate against the schema.
- Unknown fields are ignored and unknown events are passed on as
  `UnknownEngineEvent`, as the versioning rules require of clients.

## The engine host

- **Finding the engine** ([ADR D0004](adr/0004-finding-node-and-the-engine.md)):
  the engine path from the settings, then the product's `ENGINE` environment
  variable (`CFE_ENGINE`), then an engine bundled next to the app (D6), then a
  development checkout found above the app's folder. Node from the settings,
  else `node` on the `PATH`.
- **Process**: `node <main.js> --stdio` with stdin, stdout and stderr
  redirected. stdout is read line by line as UTF-8; a line over 4 MiB is
  discarded and reported, never parsed. stderr is collected into the engine
  log as it arrives.
- **Connection**: `JsonRpcConnection` sends requests with increasing ids,
  completes each pending request with its response, and passes notifications
  to the event stream. When the process exits, every pending request fails
  with `EngineExitedException`, carrying the exit code and the last lines of
  stderr.
- **Session**: `EngineSession` owns one engine process. `StartAsync` starts it
  and sends `initialize` with this client's protocol version; an
  `IncompatibleProtocol` refusal becomes a clear message saying which side to
  update. `StopAsync` sends `shutdown` and waits for the exit; if the engine
  does not exit in time it is killed. The app restarts the session after a
  crash only when the user asks.

## The app

- **MVVM** with CommunityToolkit.Mvvm ([ADR D0005](adr/0005-mvvm-with-the-community-toolkit.md)).
  View models hold all state and logic and are tested without a window;
  views only bind. Anything that touches the operating system (dialogs, file
  system, settings storage, time) goes through a small service interface so
  that tests can replace it.
- **Main window**: a start page (open a folder, recent projects) until a
  project is open; then a three-part layout:
  - left: the test explorer (folders and test files; search; tags);
  - centre: tabs for open step files and the action catalogue. A step file
    tab is an editor ([ADR D0006](adr/0006-avaloniaedit-for-step-files.md))
    with line numbers, undo and redo, and problem lines marked; Save (Ctrl+S,
    Cmd+S on macOS) writes UTF-8 without a byte-order mark, keeps the file's
    line endings, and replaces the file in one step (a temporary file next to
    it, then a move);
  - bottom: problems (diagnostics with file, line and column) and the engine
    log;
  - top: project, environment, engine status, and the run controls (disabled
    until the engine reports a browser in `capabilities.browsers`).
- **Problems**: the diagnostics of `openProject` plus those of `validate` over
  all test files. YAML changes under the project root (watched, gathered for
  300 ms) re-read the open tabs, re-list the tests when test files came or
  went, and validate all test files again (flows and targets affect the tests
  that use them); a change to the config file opens the project again.
- **Editing**: 300 ms after the last change, the text is sent to `validate`
  with `content`; only the answer for the newest text is applied. While a tab
  has unsaved changes, its file's problems (in the tab and the problems panel)
  are those of the text being edited. A change on disk reloads a tab without
  unsaved changes; with unsaved changes, a bar offers to reload or keep. The
  app's own save is not mistaken for a change from outside, because a tab
  compares the disk with the text it last read or saved.
- **Unsaved changes**: closing a tab, the project or the window, or opening
  another project, asks (through `IDialogService`) whether to save, discard
  or cancel. Opening the same project again keeps the open tabs and their
  unsaved text, so an automatic reopen never loses work.
- **Settings**: Node path, engine path and recent projects, stored as JSON in
  the user's application data folder under the product's display name.

## The step list

- A test or flow file's tab shows a **step list** beside the text editor: the
  file's sections (`before`, `steps`, `after` for a test; `steps` for a flow)
  with their steps, each with its action, main value (the shorthand value or
  the parameters in short), its `name`, and a mark when the engine reported
  a problem on its lines. Selecting a step shows its line in the text editor.
- The app reads the YAML itself, with YamlDotNet's parser
  ([ADR D0007](adr/0007-yamldotnet-for-the-step-list.md)): `StepFiles/YamlTree`
  gives every node's position, `StepFiles/StepOutline` finds each step's lines
  and its form (bare, shorthand or long, as in the step format).
- **One text, two views.** The step list is read again from the document on
  every change, so typing in the text editor changes it at once. A change made
  in the step list (add, remove, duplicate, move up or down, move to another
  section) is computed on the text by `StepFiles/StepEdits`, which touches only
  the lines of the steps concerned, and applied to the document as one
  replacement of the part that differs: the text editor shows it at once and
  its undo takes it back in one step. Comments and layout elsewhere stay as
  they were.
- **Adding a step**: an action picked from the engine's `listActions` (with a
  search) is inserted after the selected step, or at the end of the steps. It
  is written as the action alone when it needs nothing, with its shorthand and
  an empty value when it has one, else in the long form with its required
  parameters empty; the engine then reports what is still missing.
- A file the step list cannot read (invalid YAML, anchors, sections written
  as `[ … ]`, …) shows a line saying why, and the text editor alone.
- **The step's form.** Below the list, the selected step's form has a field
  per parameter of its action, read from the action's `paramsSchema`
  (`StepFiles/ParamSchema`): text, number (or `${…}`), yes/no, a choice from
  an `enum`, a target picker offering the file's own targets and those of the
  project's `*.targets.yaml` files, and YAML text for anything else (lists,
  objects, an inline target). The protocol does not mark target parameters;
  a parameter whose schema allows an object with `candidates` is taken as
  one. Then the step's `name`, `page` and `timeout`.
- A valid change in a field rewrites the step's lines at once
  (`StepFiles/StepWriter`): the step keeps the form it was written in where it
  can (shorthand stays shorthand, a flow mapping stays one), values not
  changed keep their text, strings are quoted only when they must be, and
  emptying an optional field removes its key. A value that does not fit its
  field says why and writes nothing.
- The engine's problems on the step show next to the field whose key or value
  they point at (by line and column); the others, such as a missing
  parameter, are listed at the top of the form. An action the engine does not
  know keeps its values as YAML.

## The targets editor

- Test, flow and targets files have a **Targets** tab in the side panel (a
  targets file opens on it): the file's targets by name, adding a target
  (with a name that is not taken in the file or the shared targets) and
  removing one, and the selected target's form.
- The form shows `frame` and `within`, each picked from the other targets
  (a target has one or the other, not both), and the **candidates in order of
  reliability**: each with its kind (`role`, `label`, `placeholder`, `text`,
  `testId`, `css`), its value, the accessible name of a `role` candidate,
  `exact` and `nth`. Candidates can be added, removed and moved up or down.
  A candidate with other keys, or an inline frame or within, is kept as
  written.
- It reads targets with the same tree as the step list
  (`StepFiles/TargetsOutline`) and writes only the lines of the target that
  changed (`StepFiles/TargetWriter`), as one replacement of the document, so
  the text editor's undo covers it. Unchanged candidates keep their original
  lines: the comment lines above a candidate and a comment at the end of its
  line stay with it, also when it moves. A target written in the short form
  stays short until it gets a frame or within.

## Files

- **New test, New flow, New targets file** (in the explorer) ask for a folder
  and a name (a test goes to the folder selected in the explorer by default;
  flows to `flows`, targets files to `targets`), add the kind's ending, refuse
  paths outside the project and existing files, write a minimal file the
  engine reads without problems (a test or flow with one `goto: /` step,
  since `steps` needs at least one; a targets file with `targets: {}`), and
  open it. The test list and problems are read again at once.
- **Rename** (in a file's tab) keeps the file in its folder and its kind's
  ending; a tab with unsaved changes is saved or reverted first. **Delete**
  asks first, then deletes the file and closes its tab.

## Runs

- **Starting**: Run all, Run selected (a test, or every test shown in a
  folder of the explorer), Run tag, and Run in a test's tab. Each sends
  `startRun` with the chosen environment and `options.headed` ("Show
  browser"). Running is off, with the reason shown, while the engine is not
  running or reports no browser in `capabilities.browsers`; the reason then
  names the command that installs Chromium for the engine of a checkout.
- **Unsaved files**: the engine runs what is on disk, so a run with unsaved
  tabs first asks to save and run, run without saving, or cancel.
- **One run at a time**: a second start while a run goes on only says so.
  `RunInProgress` and `StepFilesInvalid` refusals are explained; the problems
  of the latter go to the problems panel.
- **The run model**: a `RunViewModel` is built purely from events, keyed by
  `testId` and `stepId`, so a live run and a finished run read back from
  `events.ndjson` (ADR 0015 of the repository) use the same code path. Events
  that arrive before the answer to `startRun` are kept and applied once the
  run id is known. A gap in `seq` is noted rather than guessed over. Steps
  are grouped into before, steps and after and indented under the flow step
  that called them. A step skipped before it started (after a failure) has
  no title or line, so it is listed in its test's messages.
- **Cancel**: `cancelRun`; the run ends when `runFinished` arrives. If the
  engine stops during a run, the run is marked unfinished and keeps what was
  reported. Closing the project cancels a run that is still going.
- **The run tab**: the test instances with their state; the selected test's
  steps; the selected step's details (error code, message, hint, expected and
  actual, locator candidates, warnings and fallbacks, parameters), a button
  that opens the file at the step's line, and its screenshot, or "No
  screenshot was recorded." Screenshots are read from the absolute paths in
  `screenshotReady`; the app never builds artifact paths itself.
- **Page states**: "Open page state" appears once `snapshotReady` arrived (or
  a step result says the snapshot was saved) and sends `openSnapshot`; a
  `SnapshotUnavailable` error shows `data.screenshot` instead. The engine of
  protocol 0.1.0 does not yet take screenshots or save page states, so with
  it every step shows "No screenshot was recorded." and no button.

## Run history

- The bottom panel's **Run history** lists the project's run folders
  (`<project>/.cfe/runs/<runId>/`), newest first by `runId` (which sorts by
  start time), with the start time, environment, result and totals. It is read
  when the project opens and again when a run starts or ends, or on Refresh.
- Only `events.ndjson` is read; it holds protocol events exactly as sent. The
  shapes of `run.json` and `test.json` are not part of the protocol, so the
  app does not use them.
- Opening a run builds the same `RunViewModel` as a live run, by applying the
  events in order; a record that ends without `runFinished` is shown as a run
  that did not finish. Opening the run going on, or one already open, selects
  its tab.
- The engine appends to the file during a run and deletes the oldest folders
  when it starts one (`keepRuns`). The file is read with sharing for writing
  and deleting. A line that is not JSON or is cut off is counted and left out
  (and the run says so); an unknown event is noted by the run view; a folder
  without `events.ndjson` is listed with that reason; a folder that is gone
  when opened is explained and dropped from the list.

## Threading

Engine output is read on background tasks. View models receive events through
a dispatcher service that posts onto the UI thread, so no view model is
touched from two threads. Tests use an immediate dispatcher.

## Errors

Every failure the user can see says what happened and what to do next, in the
style of the engine's messages: "The engine could not be started: Node was not
found on the PATH. Install Node 24, or set its path in Settings." Exceptions
from the engine host are typed (`EngineNotFoundException`,
`EngineExitedException`, `EngineRequestException` with the protocol error's
name and data, `ProtocolViolationException`).
