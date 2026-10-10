# Engine protocol

> Status: **Accepted design, revised for instruction 0001** (2026-10-09). Protocol
> version `0.1.0` once implemented. This document is a **public contract**:
> every client (CLI, desktop app, a future VS Code extension, the server)
> relies on it.

## Transport

- The client starts the engine as a child process:
  `node <engine>/dist/main.js --stdio`.
- Messages are [JSON-RPC 2.0](https://www.jsonrpc.org/specification), UTF-8,
  **one JSON object per line** (newline-delimited, `\n`). JSON escapes
  newlines inside strings, so a message never spans lines
  ([ADR 0005](adr/0005-json-rpc-over-stdio.md)).
- **stdout** carries protocol messages only. **stderr** carries free-form
  diagnostic logging for developers; clients may show or ignore it.
- Client → engine: requests (with `id`). Engine → client: responses and
  notifications (events, without `id`). The engine sends no requests in `0.x`.
  The protocol defines no notifications from the client; the engine ignores
  any message without an `id`.
- Every message the engine writes passes through secret masking first
  ([ADR 0014](adr/0014-secret-masking.md)).
- **No file contents.** Messages never carry the contents of screenshots,
  snapshots, results or other artifacts; they carry absolute paths, and clients
  read the files. (`validate` with `content` carries an unsaved step file,
  which is input, not an artifact.)
- **Maximum message size: 4 MiB** of UTF-8 per line, in both directions
  (`MAX_MESSAGE_BYTES` in `@cfe/protocol`, [ADR 0005](adr/0005-json-rpc-over-stdio.md)):
  - The engine never sends a longer line. It first truncates long string fields
    (`message`, `expected`, `actual`, log text) to 64 KiB each, marked
    `… [truncated N characters]`. The 64 KiB limit counts **bytes of UTF-8**,
    marker included; `N` counts the removed **characters (Unicode code
    points)**. A string is never cut inside a character, and secrets are masked
    before anything is truncated. If the message is still too large, a response
    becomes the error `MessageTooLarge` and an event is replaced by a `log`
    event (level `error`, code `MessageTooLarge`) naming the event type, test
    and step. The run continues.
  - A line from the client over the limit is discarded unparsed; the engine
    answers `MessageTooLarge` with `id: null` and keeps the session open.
  - A client that receives a line over the limit discards it, reports an engine
    error to its user and may end the session; it must not crash.
- When stdin closes, the engine cancels any run (still running `after` steps,
  with a 30-second limit), closes browsers and exits.

## Versioning

([ADR 0011](adr/0011-protocol-versioning.md), approved)

- The protocol has its own semantic version, separate from package versions,
  exported as `PROTOCOL_VERSION` from `@cfe/protocol`.
- **Compatible changes**: new optional fields, new methods, new events, new error
  codes, new values in enums documented as open.
- **Breaking changes**: removing or renaming anything, changing a type, making
  an optional field required.
- Clients **must ignore** unknown fields and unknown events. The engine ignores
  unknown fields in request parameters.
- **Compatibility rule.** From `1.0.0`: client and engine are compatible when
  their major versions are equal. While the version is `0.x`: major **and**
  minor must be equal, because a `0.x` minor bump may break.
- `1.0.0` is planned for the desktop app release (v0.3.0).
- While the version is `0.x`, **compatible additions raise the patch number**
  ([ADR 0011](adr/0011-protocol-versioning.md), R11). The current version is
  `0.1.1`. It adds to `0.1.0`: recording (`startRecording`, `stopRecording`,
  `verifyRecording` and their events), `createProject`,
  `capabilities.installCommand`, the optional `section`, `action`, `title` and
  `location` of `stepSkipped`, and the error names `RecordingInProgress`,
  `RecordingNotFound`, `FileExists` and `FolderNotEmpty`.

Machine-readable definitions: every message is defined once, as a Zod schema in
`@cfe/protocol` ([ADR 0020](adr/0020-protocol-as-zod-schemas.md)). The
TypeScript types are inferred from those schemas, the engine validates request
parameters with them, and a script generates one JSON Schema file per message
into `packages/protocol/schema/`, which the C# client uses for code generation
and contract tests. The generated files are committed, and a test fails if they
differ from what the script produces.

## Handshake

The version handshake happens at connect, before anything else:

1. The engine writes nothing to stdout until it receives a message.
2. The client's first request must be `initialize`. Any other request gets
   `NotInitialized`.
3. If the client's `protocolVersion` is compatible, the engine answers with its
   own version and capabilities. The session is open.
4. If it is not compatible, the engine answers with `IncompatibleProtocol`,
   then exits with code 3. The error says which side to update:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32002,
    "message": "This client speaks protocol 0.2.0 but the engine speaks 0.1.0. Update the engine to a version that supports protocol 0.2.",
    "data": {
      "name": "IncompatibleProtocol",
      "clientProtocolVersion": "0.2.0",
      "engineProtocolVersion": "0.1.0",
      "engineVersion": "0.1.0"
    }
  }
}
```

5. A second `initialize` in the same session fails with `-32600` invalid request.

## Session

```
client                                   engine
  │ initialize ───────────────────────────▶ │  (handshake)
  │ ◀─────────────────── result (versions)  │
  │ openProject ──────────────────────────▶ │
  │ ◀──────────────── result (diagnostics)  │
  │ startRun ─────────────────────────────▶ │
  │ ◀──────────────────── result { runId }  │
  │ ◀── runStarted, testStarted, stepStarted, screenshotReady,
  │     snapshotReady, stepPassed / stepFailed, …, testFinished, runFinished
  │ cancelRun (optional) ─────────────────▶ │
  │ startRecording ───────────────────────▶ │  (a browser opens)
  │ ◀────────────── result { recordingId }  │
  │ ◀── recordingStarted, stepRecorded, stepChanged, recordingNotice, …
  │ stopRecording ────────────────────────▶ │
  │ ◀──── recordingStopped, result { file } │
  │ verifyRecording ──────────────────────▶ │
  │ ◀── result { runId }, run events …, recordingVerified
  │ shutdown ─────────────────────────────▶ │
  │ ◀──────────────────────── result null   │  (process exits with 0)
```

## Requests

### `initialize`

```jsonc
// params
{ "protocolVersion": "0.1.1", "client": { "name": "@cfe/cli", "version": "0.1.0" } }
// result
{
  "protocolVersion": "0.1.1",
  "engine": { "name": "@cfe/engine", "version": "0.1.0" },
  "capabilities": { "browsers": ["chromium"] }
}
```

`capabilities.browsers` lists the browser names this engine can run. Clients
must not assume any particular browser; they offer what is listed. An engine
whose browser is not installed reports `[]`; `startRun` then fails with
invalid params and a message that gives the command that installs it.
`capabilities.installCommand` (since `0.1.1`) is that command, present only
while `browsers` is empty, so a client can show it before any run:
`{ "browsers": [], "installCommand": "npx playwright@1.64.0 install chromium" }`.

### `shutdown`

No params. Cancels any run, closes browsers, responds `null`, then exits.

### `openProject`

```jsonc
// params
{ "root": "C:/work/shop-tests" }
// result
{
  "root": "C:/work/shop-tests",
  "configFile": "C:/work/shop-tests/cfe.config.yaml",
  "environments": ["local", "staging"],
  "defaultEnvironment": "local",
  "logins": ["customer", "admin"],
  "diagnostics": [ /* Diagnostic[]: config and user-action problems */ ]
}
```

`defaultEnvironment` is the config's `defaults.environment`, or the first
environment when the config names none; it is absent only when the config has
no environments. Opening a project loads user actions; naming and loading
problems arrive as diagnostics. Only one project is open per engine process; opening another
replaces it. Fails with `ProjectInvalid` if no config file is found, and with
`RunInProgress` while a run is going: a run keeps its project until it ends.

### `createProject`

```jsonc
// params
{
  "root": "C:/work/shop-tests",          // empty, or not there yet
  "name": "Shop tests",
  "baseUrl": "http://localhost:5173",
  "environment": "local"                  // optional: the first environment's name, default "local"
}
// result: the same as openProject's, for the new project
{
  "root": "C:/work/shop-tests",
  "configFile": "C:/work/shop-tests/cfe.config.yaml",
  "environments": ["local"],
  "defaultEnvironment": "local",
  "logins": [],
  "diagnostics": []
}
```

Creates a project in a folder that is empty or does not exist yet, then opens
it as `openProject` does. It writes a minimal valid config (the project's name
in its first comment line, the default globs, one environment), creates the
folders the config's globs name (`tests/`, `flows/`, `targets/`, `actions/`),
and writes a `.gitignore` that leaves out the data folder and `.env`
([step-format.md](step-format.md#a-new-project)). Fails with `FolderNotEmpty`,
whose message names what is in the folder; with `-32602` invalid params for an
empty name, a root that is a file, or a base URL or environment name the
config would not accept; and, like `openProject`, with `RunInProgress` or
`RecordingInProgress`.

### `listTests`

```jsonc
// params
{ "tags": ["smoke"] }                    // optional filter
// result
{ "tests": [ { "file": "tests/checkout/guest-checkout.test.yaml",
               "name": "Guest checks out ${row.product}", "tags": ["smoke"], "rows": 2 } ] }
```

### `listActions`

No params. Lists the built-in actions and the open project's user actions
(only the built-ins when no project is open). `paramsSchema` is the JSON Schema
of the action's canonical long form; it is `{}` for a user action whose schema
cannot be converted.

```jsonc
// result
{
  "actions": [
    {
      "name": "fill",
      "description": "Clears a field and types a value.",
      "shorthand": null,
      "paramsSchema": {/* JSON Schema of the canonical long form */},
      "source": { "kind": "builtin" }, // or { "kind": "file", "file": "actions/shop/add-to-cart.ts" }
    },
  ],
}
```

### `validate`

Validates files on disk, or an unsaved editor buffer passed as `content`. One
of the two is required; without either, the request fails with invalid params
("it needs "files" … or "content" …"). A path that is missing, a folder or
outside the project is reported as a diagnostic (`FileNotFound`, `NotAFile`,
`FileOutsideProject`), not as an error.

```jsonc
// params
{ "files": ["tests/checkout/guest-checkout.test.yaml"] }
// or
{ "content": { "file": "tests/new.test.yaml", "text": "version: 1\nname: …" } }
// result
{ "diagnostics": [ /* Diagnostic[] */ ] }
```

### `startRun`

```jsonc
// params
{
  "files": ["tests/checkout/guest-checkout.test.yaml"],   // or omit to use "tags"
  "tags": ["smoke"],
  "env": "local",                                         // optional: config default
  "options": { "headed": false, "browser": "chromium", "refreshLogins": false }
}
// result (returned before the run starts; progress arrives as events)
{ "runId": "20261009-054902-123-1a2b", "resultsDir": "C:/work/shop-tests/.cfe/runs/20261009-054902-123-1a2b" }
```

All selected files are validated first. If any has an error, the request fails
with `StepFilesInvalid` and `error.data.diagnostics`; nothing runs. One run at a
time per engine; a second `startRun` fails with `RunInProgress`. An
`options.browser` not in `capabilities.browsers` fails with `-32602` invalid
params.

### `cancelRun`

`{ "runId": "…" }` → `null`, returned as soon as cancellation has started. The
current step's `ctx.signal` is aborted, and that step is reported as
`stepFailed` with error code `Cancelled`. Remaining steps are skipped, `after`
steps still run, then `testFinished` (status `cancelled`) and `runFinished`
follow. Tests not yet started are reported as cancelled.

### `openSnapshot`

`{ "runId": "…", "testId": "…", "stepId": "…" }` → `null`. Opens the saved page
state of that step in a test-browser window. Fails with `SnapshotNotFound` when
the step has no snapshot (for example with `snapshots: off`), and with
`SnapshotUnavailable` when the snapshot exists but cannot be shown (for example
the viewer failed to start); that error's `data.screenshot` holds the step's
screenshot path so the client can show it instead. The snapshot format is an
engine detail and not part of the protocol.

### `startRecording`

```jsonc
// params
{
  "file": "tests/add-todo.test.yaml",   // relative to the project root; must not exist
  "startUrl": "/todos",                  // optional: a path under the environment's baseUrl, or a full URL; default "/"
  "environment": "local",                // optional: config default
  "login": "customer",                   // optional: a saved login for the main page
  "name": "Add a to-do"                  // optional: made from the file name
}
// result (once the recording browser is open; the recording arrives as events)
{ "recordingId": "rec-20261010-101500-123-1a2b", "file": "tests/add-todo.test.yaml" }
```

Opens a **visible** browser with the environment's context settings (and the
saved login's state), goes to the start URL and records what the person does
there, as [recording.md](recording.md) describes. The engine writes the file
after every change; it is a valid test file at every moment.

- One recording at a time, and none while a run is going: refused with
  `RecordingInProgress` or `RunInProgress`. `startRun` and `openProject` are
  refused with `RecordingInProgress` while a recording is going on.
- Refused with `FileExists` when the file exists (recording into an existing
  test is not available yet), and with `-32602` invalid params for a file name
  that does not end in `.test.yaml`, an unknown environment or login, or a
  browser that cannot start (the message gives the install command).
- The recording ends with `stopRecording`, when the person closes the browser,
  or when the engine ends: on `shutdown`, when the client closes its
  connection, or for any other reason. The file keeps what was recorded.

### `stopRecording`

`{ "recordingId": "…" }` → `{ "file": "tests/add-todo.test.yaml", "steps": 3 }`.
Writes the last pending step, closes the recording browser, sends
`recordingStopped`, then answers. For a recording that has already ended it
answers the same without doing anything. Fails with `RecordingNotFound` for an
unknown id.

### `verifyRecording`

`{ "recordingId": "…" }` → `{ "runId": "…", "resultsDir": "…" }`, as
`startRun`. Runs the recorded file with the normal runner, in a fresh browser
context; its events are the usual run events, followed by
`recordingVerified`. A recording counts as runnable only after a verify that
passed. Fails with `RecordingInProgress` while it is still recording (stop it
first), with `RecordingNotFound` for an unknown id, and with the errors of
`startRun`.

## Events

Events are JSON-RPC notifications. Every run event's params include `runId`
and `seq`, a number that increases by one per event within a run, so a client
can detect gaps. Recording events carry `recordingId` instead, and arrive in
order. Times are ISO 8601 UTC strings; durations are milliseconds.

| Event             | Extra fields                                                                                                                                          |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `runStarted`      | `env`, `browser`, `settings`: `{ viewport, locale, timezone }`, `startedAt`, `tests`: `{ testId, file, name, row?, skip? }[]`                         |
| `testStarted`     | `testId`, `startedAt`                                                                                                                                 |
| `stepStarted`     | `testId`, `stepId`, `parentStepId?`, `section` (`before`, `steps`, `after`), `action`, `params`, `page`, `title`, `location`                          |
| `stepPassed`      | `testId`, `stepId`, `durationMs`, `locators`: `LocatorUse[]`, `snapshot`: `SnapshotStatus`                                                            |
| `stepFailed`      | `testId`, `stepId`, `durationMs`, `error`: `ErrorInfo`, `locators`: `LocatorUse[]`, `snapshot`: `SnapshotStatus`                                      |
| `stepSkipped`     | `testId`, `stepId`, `reason` (`previousFailure`, `cancelled`, `variableNotSet`), `message`, `variable?`, `section?`, `action?`, `title?`, `location?` |
| `screenshotReady` | `testId`, `stepId`, `page`, `path`, `width`, `height`                                                                                                 |
| `snapshotReady`   | `testId`, `stepId`, `page`                                                                                                                            |
| `pageOpened`      | `testId`, `stepId`, `page`, `automatic` (`true` when no step named it)                                                                                |
| `log`             | `level` (`debug`, `info`, `warn`, `error`), `message`, `code?`, `testId?`, `stepId?`, `location?`, `data?`                                            |
| `testSkipped`     | `testId`, `reason`: the test's `skip` text (sent instead of `testStarted` … `testFinished`)                                                           |
| `testFinished`    | `testId`, `status` (`passed`, `failed`, `cancelled`), `durationMs`                                                                                    |
| `runFinished`     | `status`, `durationMs`, `totals`: `{ passed, failed, cancelled, skipped }`                                                                            |

Recording events:

| Event               | Fields                                                                                                                                                                                          |
| ------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `recordingStarted`  | `recordingId`, `file`, `startUrl`                                                                                                                                                               |
| `stepRecorded`      | `recordingId`, `index`, `step`: `{ action, params, page?, opens? }` (as written, canonical long form), `targets` (the targets the step made, as the file holds them), `review?`                 |
| `stepChanged`       | `recordingId`, `index`, `step`, `review?`: a `fill` whose text changed, or a step that got `opens`                                                                                              |
| `recordingNotice`   | `recordingId`, `kind` (`drag`, `fileChooser`, `contextMenu`, `doubleClick`, `shortcut`, `key`, `history`, `contentEditable`, `background`, `unmapped`, `writeFailed`), `message`, `page`, `url` |
| `recordingStopped`  | `recordingId`, `file`, `reason` (`stopped`, `browserClosed`), `steps`                                                                                                                           |
| `recordingVerified` | `recordingId`, `runId`, `status` (`passed`, `failed`): sent after the verify run's `runFinished`                                                                                                |

- `stepRecorded.review` is the reason the step is marked for review, the same
  text as the `# review:` comment above it in the file.
- `index` is the step's place in the file's `steps` (0 is the first `goto`).
- `recordingNotice.kind` is an open enum: clients show unknown kinds as they
  show the others.
- Recording events are masked like every other message; a recorded `fill`
  never carries a typed password in the first place
  ([recording.md](recording.md#secrets)).

- `stepStarted.params` is the step's **canonical long form** with variables
  still uninterpolated (`${secrets.…}` is never resolved in events).
- `locators` lists every target the step resolved, in order (empty when the
  step used none). A `candidateIndex` greater than 0 means the preferred
  candidates no longer match; clients can show this as a target to refresh.
- An unnamed new page produces `pageOpened` with `automatic: true` and a `log`
  event with level `warn`, code `UnnamedPage` and the step's `location`.
- A step skipped **without a `stepStarted`** (after a failure, after a
  cancellation, or an `after` step whose variable was never set) carries its
  `section`, `action`, `title` and `location` in `stepSkipped`, so a client can
  show it in its place.
- An `after` step that uses a variable that was never set produces
  `stepSkipped` with reason `variableNotSet`, `variable` (the name) and
  `message` "skipped: orderNumber was never set". It is not a failure.
- A test with `skip` produces one `testSkipped` per data row and counts in
  `totals.skipped`.
- `log.data` is an optional object of facts that belong to the message's
  `code`, for clients that act on them; its fields are documented per code.
- Whenever a target is found by a candidate other than its first, at any level
  (frame, `within`, element), the engine sends a `log` event with level
  `warn`, code `LocatorFallback` and the step's `location`, with
  `data: { target, candidateIndex }`: the target's name (absent for an inline
  target) and the index of the candidate used.
- `runFinished.status` is `cancelled` when the run was cancelled, otherwise
  `failed` when any test instance failed, otherwise `passed` (skipped tests do
  not make a run fail).
- `snapshot` in a step result says what happened to the page snapshot. A
  snapshot problem never changes the step's own status.
- Other `warn` codes in 0.1.0: `StrayActionCode` (a user action kept running
  after its step ended), `PageReplaced` (a closed page was replaced for an
  `after` step), `SdkVersionMismatch`, `RunCleanupFailed` (an old run folder
  could not be deleted), `LoginCleanupFailed` (an old saved login
  could not be deleted).

Example line on stdout (shown wrapped):

<!-- prettier-ignore -->
```json
{"jsonrpc":"2.0","method":"stepFailed","params":{"runId":"20261009-054902-123-1a2b","seq":17,
"testId":"tests/checkout/guest-checkout.test.yaml#1","stepId":"steps.3","durationMs":10012,
"error":{"code":"AssertionFailed","message":"Text of \"cart.count\" is \"0\", expected \"1\".",
"expected":"1","actual":"0","location":{"file":"tests/checkout/guest-checkout.test.yaml","line":31,"column":5}},
"locators":[{"param":"target","target":"cart.count","candidateIndex":0,"candidate":{"testId":"cart-count"}}],
"snapshot":{"state":"saved"}}}
```

### Identifiers

- `testId`: the test file path plus `#` and the data-row index
  (`tests/a.test.yaml#0`; tests without data use `#0`). Clients should treat it
  as opaque.
- `stepId`: a path of `section.index` segments joined by `/`, for example
  `steps.3`, `after.0`, or `steps.4/steps.1` for the second step of the flow
  called by the fifth step. Stable across runs of the same file content.
- `page`: the page name (`main`, a declared name, an `opens` name, or an
  automatic name such as `tab-2`).
- Artifact paths in events are absolute file-system paths; step-file paths are
  relative to the project root.

## Shared types

```ts
interface Location {
  file: string;
  line: number; // 1-based
  column: number; // 1-based
}

interface Diagnostic extends Location {
  endLine?: number;
  endColumn?: number;
  severity: 'error' | 'warning';
  code: string; // e.g. "UnknownAction", "MissingParameter", "ActionNameNotNamespaced"
  message: string;
  hint?: string;
}

interface LocatorUse {
  param: string; // parameter name, e.g. "target", "from", "to"; "frame" / "within" when nested
  target?: string; // target name; absent for inline targets
  candidateIndex: number | null; // null when no candidate matched
  candidate: Record<string, unknown> | null; // after interpolation, secrets masked
  frame?: LocatorUse; // how the target's frame was found
  within?: LocatorUse; // how the target's containing element was found
}

type SnapshotStatus =
  | { state: 'saved' } // snapshotReady was sent
  | { state: 'skipped' } // snapshots: off, or onFailure and the step passed
  | { state: 'failed'; reason: string }; // recording or masking failed; screenshot only

interface ErrorInfo {
  code: string; // e.g. "TargetNotFound", "InvalidSelector", "AssertionFailed", "ActionTimeout", "Cancelled"
  message: string; // secrets already masked
  hint?: string;
  location?: Location;
  expected?: unknown;
  actual?: unknown;
  candidates?: { candidate: Record<string, unknown>; matches: number }[];
}
```

### Step error codes

`ErrorInfo.code` in `stepFailed`, in 0.1.0. The list is open: clients show the
message of a code they do not know.

| Code                | When                                                                                       |
| ------------------- | ------------------------------------------------------------------------------------------ |
| `TargetNotFound`    | No candidate matched exactly one element before the step's time ran out; with `candidates` |
| `InvalidSelector`   | Playwright rejected a candidate's selector                                                 |
| `AssertionFailed`   | An `expect.*` action or an `AssertionError`; with `expected` and `actual`                  |
| `ActionTimeout`     | The step did not finish within its timeout                                                 |
| `Cancelled`         | The run was cancelled during the step                                                      |
| `ActionError`       | The action threw: an `ActionError`, or anything that is not an SDK error                   |
| `PageClosed`        | The step's page was closed while the step ran                                              |
| `VariableNotSet`    | A `${vars.…}` the step uses was never set (in `after` steps the step is skipped instead)   |
| `PathNotFound`      | A `${…}` path does not exist                                                               |
| `InvalidParameters` | The step's parameters are invalid once their `${…}` values are filled in                   |
| `NotImplemented`    | The step needs something this engine version cannot run yet                                |

For `TargetNotFound`, `candidates` lists the candidates of the level where the
search stopped (the element itself, or the frame or `within` target it is in),
each with its last match count; the message names every level.

## Error codes

Standard JSON-RPC codes (`-32700` parse error, `-32600` invalid request,
`-32601` method not found, `-32602` invalid params, `-32603` internal error)
plus:

| Code     | Name                   | When                                                                                 |
| -------- | ---------------------- | ------------------------------------------------------------------------------------ |
| `-32001` | `NotInitialized`       | Any request before a successful `initialize`                                         |
| `-32002` | `IncompatibleProtocol` | `initialize` with an incompatible version (then exit)                                |
| `-32003` | `ProjectNotOpen`       | A project request before `openProject`                                               |
| `-32004` | `ProjectInvalid`       | No or unreadable config file                                                         |
| `-32005` | `StepFilesInvalid`     | `startRun` with validation errors (`data.diagnostics`)                               |
| `-32006` | `RunInProgress`        | `startRun`, `openProject`, `createProject` or `startRecording` while a run is active |
| `-32007` | `RunNotFound`          | `cancelRun` / `openSnapshot` with an unknown `runId`                                 |
| `-32008` | `SnapshotNotFound`     | `openSnapshot` for a step without a snapshot                                         |
| `-32009` | `MessageTooLarge`      | A message over 4 MiB (see [Transport](#transport))                                   |
| `-32010` | `SnapshotUnavailable`  | Snapshot exists but cannot be shown; `data.screenshot`                               |
| `-32011` | `RecordingInProgress`  | `startRecording`, `startRun`, `openProject` or `verifyRecording` during a recording  |
| `-32012` | `RecordingNotFound`    | `stopRecording` / `verifyRecording` with an unknown `recordingId`                    |
| `-32013` | `FileExists`           | `startRecording` with a file that exists                                             |
| `-32014` | `FolderNotEmpty`       | `createProject` in a folder that is not empty; the message names what is in it       |

Error responses carry `error.data.name` (the name above) so clients can switch on
names instead of numbers.

A failing **test** is not a protocol error: it is reported through events.

## Engine exit codes

| Code | Meaning                                                                                                                |
| ---- | ---------------------------------------------------------------------------------------------------------------------- |
| 0    | Normal exit after `shutdown` or stdin closed                                                                           |
| 1    | Unexpected internal error (details on stderr), or started without `--stdio` (a message on stderr says how to start it) |
| 3    | Refused the client during the handshake                                                                                |
