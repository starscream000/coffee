# Engine protocol

> Status: **Proposal** (Milestone 0). Protocol version `0.1.0` once
> implemented. This document is a **public contract**: every client (CLI,
> desktop app, a future VS Code extension, the server) relies on it.

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
- When stdin closes, the engine cancels any run (still running `after` steps,
  with a 30-second limit), closes browsers and exits.

## Versioning

- The protocol has its own semantic version, separate from package versions,
  exported as `PROTOCOL_VERSION` from `@test-tool/protocol`.
- **Compatible changes** (minor version): new optional fields, new methods, new
  events, new error codes, new enum values in fields documented as open.
- **Breaking changes** (major version): removing or renaming anything, changing a
  type, making an optional field required.
- Clients **must ignore** unknown fields and unknown events. The engine ignores
  unknown fields in request parameters.
- `initialize` fails with `IncompatibleProtocol` if the major versions differ.
- **(Open 7)** While the version is `0.x`, a minor bump may break, and client and
  engine must match on major _and_ minor. Version `1.0.0` is proposed for the
  desktop app release (v0.3.0). See [ADR 0011](adr/0011-protocol-versioning.md).

Machine-readable definitions: `@test-tool/protocol` exports the TypeScript types
and generates a JSON Schema per message under `packages/protocol/schema/`, which
the C# client uses for code generation and contract tests.

## Session

```
client                                   engine
  │ initialize ───────────────────────────▶ │
  │ ◀─────────────────── result (versions)  │
  │ openProject ──────────────────────────▶ │
  │ ◀──────────────── result (diagnostics)  │
  │ startRun ─────────────────────────────▶ │
  │ ◀──────────────────── result { runId }  │
  │ ◀── runStarted, testStarted, stepStarted, screenshotReady,
  │     snapshotReady, stepPassed / stepFailed, …, testFinished, runFinished
  │ shutdown ─────────────────────────────▶ │
  │ ◀──────────────────────── result null   │  (process exits)
```

## Requests

All requests except `initialize` fail with `NotInitialized` before
`initialize` succeeds.

### `initialize`

```jsonc
// params
{ "protocolVersion": "0.1.0", "client": { "name": "testtool-cli", "version": "0.1.0" } }
// result
{
  "protocolVersion": "0.1.0",
  "engine": { "name": "@test-tool/engine", "version": "0.1.0" },
  "capabilities": { "browsers": ["chromium"], "snapshots": true }
}
```

### `shutdown`

No params. Cancels any run, closes browsers, responds `null`, then exits.

### `openProject`

```jsonc
// params
{ "root": "C:/work/shop-tests" }
// result
{
  "root": "C:/work/shop-tests",
  "configFile": "C:/work/shop-tests/testtool.config.yaml",
  "environments": ["local", "staging"],
  "logins": ["customer", "admin"],
  "diagnostics": [ /* Diagnostic[]: config and user-action problems */ ]
}
```

Opening a project loads user actions. Only one project is open per engine
process; opening another replaces it. Fails with `ProjectInvalid` if no
config file is found.

### `listTests`

```jsonc
// params
{ "tags": ["smoke"] }                    // optional filter
// result
{ "tests": [ { "file": "tests/checkout/guest-checkout.test.yaml",
               "name": "Guest checks out ${row.product}", "tags": ["smoke"], "rows": 2 } ] }
```

### `listActions`

```jsonc
// result
{
  "actions": [
    {
      "name": "fill",
      "description": "Clears a field and types a value.",
      "shorthand": null,
      "paramsSchema": {/* JSON Schema */},
      "source": { "kind": "builtin" }, // or { "kind": "file", "file": "actions/shop/add-to-cart.ts" }
    },
  ],
}
```

### `validate`

Validates files on disk, or an unsaved editor buffer passed as `content`.

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
  "env": "local",
  "options": { "headed": false, "browser": "chromium", "refreshLogins": false }
}
// result (returned before the run starts; progress arrives as events)
{ "runId": "2026-10-09T05-49-02-1a2b" }
```

All selected files are validated first. If any has an error, the request fails
with `StepFilesInvalid` and `error.data.diagnostics`; nothing runs. One run at a
time per engine; a second `startRun` fails with `RunInProgress`.

### `cancelRun`

`{ "runId": "…" }` → `null`. The current step is aborted, remaining steps are
skipped, `after` steps still run, then `testFinished` (status `cancelled`) and
`runFinished` follow.

### `openSnapshot`

`{ "runId": "…", "testId": "…", "stepId": "…" }` → `null`. Opens the saved page
snapshot of that step in a test-browser window. Fails with `SnapshotNotFound`.

## Events

Events are JSON-RPC notifications. Every event's params include `runId` and
`seq`, a number that increases by one per event within a run, so a client can
detect gaps. Times are ISO 8601 UTC strings; durations are milliseconds.

| Event             | Extra fields                                                                                               |
| ----------------- | ---------------------------------------------------------------------------------------------------------- |
| `runStarted`      | `env`, `startedAt`, `tests`: `{ testId, file, name, row? }[]`                                              |
| `testStarted`     | `testId`, `startedAt`                                                                                      |
| `stepStarted`     | `testId`, `stepId`, `parentStepId?`, `section` (`before`, `steps`, `after`), `action`, `title`, `location` |
| `stepPassed`      | `testId`, `stepId`, `durationMs`, `locator?`: `{ target, candidateIndex }`                                 |
| `stepFailed`      | `testId`, `stepId`, `durationMs`, `error`: `ErrorInfo`                                                     |
| `stepSkipped`     | `testId`, `stepId`, `reason` (`previousFailure`, `cancelled`)                                              |
| `screenshotReady` | `testId`, `stepId`, `path`, `width`, `height`                                                              |
| `snapshotReady`   | `testId`, `stepId`, `path`                                                                                 |
| `log`             | `level` (`debug`, `info`, `warn`, `error`), `message`, `testId?`, `stepId?`                                |
| `testFinished`    | `testId`, `status` (`passed`, `failed`, `cancelled`), `durationMs`                                         |
| `runFinished`     | `status`, `durationMs`, `totals`: `{ passed, failed, cancelled }`                                          |

Example line on stdout:

```json
{
  "jsonrpc": "2.0",
  "method": "stepFailed",
  "params": {
    "runId": "2026-10-09T05-49-02-1a2b",
    "seq": 17,
    "testId": "tests/checkout/guest-checkout.test.yaml#1",
    "stepId": "steps.3",
    "durationMs": 10012,
    "error": {
      "code": "AssertionFailed",
      "message": "Text of \"cart.count\" is \"0\", expected \"1\".",
      "expected": "1",
      "actual": "0",
      "location": { "file": "tests/checkout/guest-checkout.test.yaml", "line": 31, "column": 5 }
    }
  }
}
```

`locator.candidateIndex` greater than 0 means the preferred candidates no longer
match; clients can show this as a warning that the target needs refreshing.

### Identifiers

- `testId`: the test file path plus `#` and the data-row index
  (`tests/a.test.yaml#0`). Clients should treat it as opaque.
- `stepId`: a path of `section.index` segments joined by `/`, for example
  `steps.3`, `after.0`, or `steps.4/steps.1` for the second step of the flow
  called by the fifth step. Stable across runs of the same file content.
- Paths in events are absolute file-system paths for artifacts and paths
  relative to the project root for step files.

## Shared types

```ts
interface Location {
  file: string;
  line: number;
  column: number;
} // 1-based

interface Diagnostic extends Location {
  endLine?: number;
  endColumn?: number;
  severity: 'error' | 'warning';
  code: string; // e.g. "UnknownAction", "MissingParameter"
  message: string;
  hint?: string;
}

interface ErrorInfo {
  code: string; // e.g. "TargetNotFound", "AssertionFailed", "ActionTimeout"
  message: string; // secrets already masked
  hint?: string;
  location?: Location;
  expected?: unknown;
  actual?: unknown;
  candidates?: { candidate: Record<string, unknown>; matches: number }[];
}
```

## Error codes

Standard JSON-RPC codes (`-32700` parse error, `-32600` invalid request,
`-32601` method not found, `-32602` invalid params, `-32603` internal error)
plus:

| Code     | Name                   | When                                                   |
| -------- | ---------------------- | ------------------------------------------------------ |
| `-32001` | `NotInitialized`       | Any request before `initialize`                        |
| `-32002` | `IncompatibleProtocol` | `initialize` with a different major version            |
| `-32003` | `ProjectNotOpen`       | A project request before `openProject`                 |
| `-32004` | `ProjectInvalid`       | No or unreadable config file                           |
| `-32005` | `StepFilesInvalid`     | `startRun` with validation errors (`data.diagnostics`) |
| `-32006` | `RunInProgress`        | `startRun` while a run is active                       |
| `-32007` | `RunNotFound`          | `cancelRun` / `openSnapshot` with an unknown `runId`   |
| `-32008` | `SnapshotNotFound`     | `openSnapshot` for a step without a snapshot           |

Error responses carry `error.data.name` (the name above) so clients can switch on
names instead of numbers.

A failing **test** is not a protocol error: it is reported through events.
