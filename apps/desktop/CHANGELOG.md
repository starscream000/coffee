# Changelog (desktop app)

All notable changes to the desktop app are documented in this file. The
repository's [changelog](../../CHANGELOG.md) covers everything else.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- Review D0002, findings 1 to 5: opening the same project again moves the
  open tabs themselves to the new workspace at the moment of the swap, so
  text typed meanwhile, undo history, caret, scroll position and the
  "changed on disk" state are all kept; after "keep my version" a tab stays
  unsaved while its text differs from the disk; if asking about unsaved
  changes fails, the window stays open; the README says that saving replaces
  the file. A tab's caret and scroll position also survive switching tabs.

### Changed

- The test explorer shows only what the engine's `listTests` reports (names,
  tags, row counts); the search for `*.test.yaml` files and its notice are
  gone. ADRs D0001, D0003, D0004, D0005 and the plan are accepted.

### Added

- A form for each step (instruction D0003, task 15): fields from the action's
  parameter schema (text, number, yes/no, a choice, a target picker with the
  file's and the shared targets, YAML text for the rest) and the step's name,
  page and timeout; the engine's problems next to their field; a valid change
  rewrites only the step's lines and keeps the form it was written in. A test
  built this way runs and passes in a real browser (tested).

- A step list beside the text of test and flow files (instruction D0003,
  tasks 14 and 16): the sections and their steps with action, main value,
  name and problem marks; selecting a step shows its line; add a step from
  the engine's actions (with search), remove, duplicate, move up and down,
  and move to another section. The step list and the text editor show the
  same text at once, the editor's undo covers both, and a file the list
  cannot read says why and shows the text only.

- Reading test and flow files as sections of steps, and changing them on
  the text (instruction D0003, task 14, first part): each step's lines and
  form (bare, shorthand, long), and adding, removing, duplicating and moving
  steps so that only the lines of the steps concerned change, comments and
  Windows line breaks included. A file that cannot be read as steps says why.
  Reads YAML with YamlDotNet 18.1.0 (ADR D0007, proposed). The step list that
  uses it follows in the next branch.

- Run history (instruction D0003, tasks 12 and 13): the bottom panel lists
  the project's earlier runs, newest first, with start time, environment,
  result and totals, read only from each run's `events.ndjson`; opening one
  shows it in the same run view as a live run, and a record that ends early
  shows as a run that did not finish. Damaged or half-written lines, unknown
  events, folders without events and folders the engine deletes meanwhile do
  not break the list.

- Running tests (instruction D0003, tasks 8, 10 and 11): Run all, Run
  selected (a test or a folder), Run tag and Run in a test's tab, with the
  environment and "Show browser"; running is off, with the reason and the
  command that installs Chromium, while the engine cannot start a browser; a
  question about unsaved files before a run; one run at a time; Cancel; a run
  tab with the tests, the steps by section, a failed step's details, its
  screenshot or "No screenshot was recorded.", "Open page state", and a
  button that opens the step's file at its line. If the engine stops during
  a run, what it reported is kept. Tested with scripted runs of a fake
  engine, on the headless platform, and with a real engine and browser
  against the demo server in a temporary copy of the demo project.

- The run model (instruction D0003, task 9): a run as the app shows it,
  built only from the engine's events, so that a live run and a run read back
  from its folder use the same code: test instances with their state, data
  row and duration; steps grouped into before, steps and after, nested under
  the flow step that called them, with title, page, duration, parameters,
  warnings, locators used and fallbacks; a failure's code, message, hint,
  expected and actual values and locator candidates; skip reasons; the
  screenshot and page state of each step; a gap in the events is noted, and a
  run whose events stop is marked unfinished with what it reported kept.

- Unsaved changes are never lost by accident (instruction D0002, task 14):
  closing a tab, the project or the window, or opening another project, asks
  whether to save, discard or cancel; Save all saves every changed tab; and
  opening the same project again (after a config or action change, or an
  engine restart) keeps the open tabs and their unsaved text.
- Step files are editable (instruction D0002, tasks 9 to 13): an editor
  (Avalonia.AvaloniaEdit 12.0.0, ADR D0006) with line numbers, undo and
  redo and problem lines marked; Save (Ctrl+S, Cmd+S) and Revert, keeping the
  file's line endings and replacing the file in one step; the engine's
  problems for the text being typed, 300 ms after the last change, in the tab
  and the problems panel; a bar to reload or keep when the file changes on
  disk during editing; a question before saving over such a file; a notice
  when the file was deleted on disk.

### Fixed

- Review D0001, findings 3 to 7: starting and stopping the engine run one at
  a time in the order asked, and nothing starts once the app is shutting
  down; an event handler that throws no longer stops the connection from
  reading, and any other end of reading fails the waiting requests; no
  command or background task loses an exception (the notice bar or the
  status line says what failed, the engine log has the detail); one refresh
  at a time after file changes, with the newest answer winning; a changed
  user-action source reopens the project.
- Review D0001, findings 1 and 2: the engine locator tests describe both the
  Windows rules and the others with paths that mean the same on every system,
  and the real-engine app test starts the engine once instead of twice.

### Added

- The workspace shell: a start page with recent projects and the engine's
  state; opening a project folder through the engine; the test explorer,
  tabs for step files and the action catalogue, the problems panel and the
  engine log; re-validation when YAML files change and reopening when the
  config changes; restarting the engine (and reopening the project) after a
  crash; a settings panel for the Node and engine paths. Headless UI tests
  render every screen, and one test opens `examples/demo-app` through the
  real engine.
- App services and view models, tested without a window: the engine service
  (start, stop, log, requests) with its status and a bounded engine log; the
  test explorer (folder tree, search, tags, problem counts, and a fallback
  for engines that cannot list tests yet); the problems panel; the read-only
  step file view with problem lines marked; the action catalogue with the
  parameters read from each action's schema; the settings panel and the
  settings store; reading and watching project files.
- The engine protocol in C# (`Desktop.Protocol`): every request, result,
  event and shared type of protocol 0.1.0, the version rule, the error table,
  newline framing with the 4 MiB limit, and tolerance for unknown fields,
  events and enum values. Contract tests compare every type with the JSON
  Schema files and read and write every protocol example (ADR D0003).
- The engine host (`Desktop.Engine`): finds Node and the engine (ADR D0004),
  starts it, runs the handshake, sends typed requests, raises events, reports
  unusable output without failing, and fails waiting requests with the exit
  code and the end of stderr when the engine stops. Tested against an engine
  in memory and against the real engine of the checkout.
- The desktop solution: `Desktop.Protocol`, `Desktop.Engine` and the Avalonia
  app `Desktop.App`, each with an xUnit test project; .NET 10, Avalonia 12,
  exact package versions with lock files, strict build settings and code style;
  `scripts/verify.sh` and `scripts/verify.ps1`.
- `Product` in C#, checked against `packages/protocol/src/product.ts`.
- The desktop documents: architecture, plan, decision records D0001 to D0005,
  standing instructions, and the desktop handoff folder with requests R0001 to
  R0003.
