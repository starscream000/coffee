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
  │ shutdown ─────────────────────────────▶ │
  │ ◀──────────────────────── result null   │  (process exits with 0)
```

## Requests

### `initialize`

```jsonc
// params
{ "protocolVersion": "0.1.0", "client": { "name": "@cfe/cli", "version": "0.1.0" } }
// result
{
  "protocolVersion": "0.1.0",
  "engine": { "name": "@cfe/engine", "version": "0.1.0" },
  "capabilities": { "browsers": ["chromium"] }
}
```

`capabilities.browsers` lists the browser names this engine can run. Clients
must not assume any particular browser; they offer what is listed. An engine
whose browser is not installed reports `[]`; `startRun` then fails with
invalid params and a message that gives the command that installs it.

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
replaces it. Fails with `ProjectInvalid` if no config file is found.

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
{ "runId": "20261009-054902-1a2b", "resultsDir": "C:/work/shop-tests/.cfe/runs/20261009-054902-1a2b" }
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

## Events

Events are JSON-RPC notifications. Every event's params include `runId` and
`seq`, a number that increases by one per event within a run, so a client can
detect gaps. Times are ISO 8601 UTC strings; durations are milliseconds.

| Event             | Extra fields                                                                                                                  |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `runStarted`      | `env`, `browser`, `settings`: `{ viewport, locale, timezone }`, `startedAt`, `tests`: `{ testId, file, name, row?, skip? }[]` |
| `testStarted`     | `testId`, `startedAt`                                                                                                         |
| `stepStarted`     | `testId`, `stepId`, `parentStepId?`, `section` (`before`, `steps`, `after`), `action`, `params`, `page`, `title`, `location`  |
| `stepPassed`      | `testId`, `stepId`, `durationMs`, `locators`: `LocatorUse[]`, `snapshot`: `SnapshotStatus`                                    |
| `stepFailed`      | `testId`, `stepId`, `durationMs`, `error`: `ErrorInfo`, `locators`: `LocatorUse[]`, `snapshot`: `SnapshotStatus`              |
| `stepSkipped`     | `testId`, `stepId`, `reason` (`previousFailure`, `cancelled`, `variableNotSet`), `message`, `variable?`                       |
| `screenshotReady` | `testId`, `stepId`, `page`, `path`, `width`, `height`                                                                         |
| `snapshotReady`   | `testId`, `stepId`, `page`                                                                                                    |
| `pageOpened`      | `testId`, `stepId`, `page`, `automatic` (`true` when no step named it)                                                        |
| `log`             | `level` (`debug`, `info`, `warn`, `error`), `message`, `code?`, `testId?`, `stepId?`, `location?`, `data?`                    |
| `testSkipped`     | `testId`, `reason`: the test's `skip` text (sent instead of `testStarted` … `testFinished`)                                   |
| `testFinished`    | `testId`, `status` (`passed`, `failed`, `cancelled`), `durationMs`                                                            |
| `runFinished`     | `status`, `durationMs`, `totals`: `{ passed, failed, cancelled, skipped }`                                                    |

- `stepStarted.params` is the step's **canonical long form** with variables
  still uninterpolated (`${secrets.…}` is never resolved in events).
- `locators` lists every target the step resolved, in order (empty when the
  step used none). A `candidateIndex` greater than 0 means the preferred
  candidates no longer match; clients can show this as a target to refresh.
- An unnamed new page produces `pageOpened` with `automatic: true` and a `log`
  event with level `warn`, code `UnnamedPage` and the step's `location`.
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
{"jsonrpc":"2.0","method":"stepFailed","params":{"runId":"20261009-054902-1a2b","seq":17,
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

| Code     | Name                   | When                                                   |
| -------- | ---------------------- | ------------------------------------------------------ |
| `-32001` | `NotInitialized`       | Any request before a successful `initialize`           |
| `-32002` | `IncompatibleProtocol` | `initialize` with an incompatible version (then exit)  |
| `-32003` | `ProjectNotOpen`       | A project request before `openProject`                 |
| `-32004` | `ProjectInvalid`       | No or unreadable config file                           |
| `-32005` | `StepFilesInvalid`     | `startRun` with validation errors (`data.diagnostics`) |
| `-32006` | `RunInProgress`        | `startRun` while a run is active                       |
| `-32007` | `RunNotFound`          | `cancelRun` / `openSnapshot` with an unknown `runId`   |
| `-32008` | `SnapshotNotFound`     | `openSnapshot` for a step without a snapshot           |
| `-32009` | `MessageTooLarge`      | A message over 4 MiB (see [Transport](#transport))     |
| `-32010` | `SnapshotUnavailable`  | Snapshot exists but cannot be shown; `data.screenshot` |

Error responses carry `error.data.name` (the name above) so clients can switch on
names instead of numbers.

A failing **test** is not a protocol error: it is reported through events.

## Engine exit codes

| Code | Meaning                                                                                                                |
| ---- | ---------------------------------------------------------------------------------------------------------------------- |
| 0    | Normal exit after `shutdown` or stdin closed                                                                           |
| 1    | Unexpected internal error (details on stderr), or started without `--stdio` (a message on stderr says how to start it) |
| 3    | Refused the client during the handshake                                                                                |
