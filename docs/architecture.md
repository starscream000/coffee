# Architecture

> Status: **Proposal** (Milestone 0). Nothing here is implemented yet. Open
> questions are marked **(Open)** and collected at the end.

## Goals

- Tests are YAML step files in the user's Git repository. The files are the
  source of truth; the tools only read and write them.
- One headless engine does all the work (validate, run, later record). Every
  user interface is a separate client that talks to it over one documented
  protocol.
- Clear, actionable failures: every error names the file, the line and what to
  do next.

Non-goals: AI features, conditional logic in step files, a hosted service
(the server in v0.4.0 runs the same engine in containers).

## Components

```
 ┌────────────────────┐   ┌──────────────────────┐   ┌──────────────────┐
 │ CLI (TypeScript)   │   │ Desktop (C#/Avalonia)│   │ Server (later)   │
 └─────────┬──────────┘   └──────────┬───────────┘   └────────┬─────────┘
           │ spawn + JSON-RPC 2.0 over stdin/stdout (newline-delimited)
           ▼                         ▼                        ▼
 ┌──────────────────────────────────────────────────────────────────────┐
 │ Engine process (Node.js)                                             │
 │  rpc ── project ── stepfile ── runner ── actions ── locators         │
 │                        │          │         │                        │
 │                     schema     context   artifacts ── browser        │
 │                                                       (Playwright)   │
 └──────────────────────────────────────────────────────────────────────┘
                     reads/writes ▼
        user's Git repository: *.test.yaml, *.flow.yaml, actions/*.ts,
        testtool.config.yaml; run output in .testtool/ (git-ignored)
```

| Package / app       | Depends on | Responsibility                                                       |
| ------------------- | ---------- | -------------------------------------------------------------------- |
| `packages/protocol` | nothing    | TypeScript types and JSON Schemas for every request, response, event |
| `packages/engine`   | protocol   | Everything that understands step files or touches a browser          |
| `packages/cli`      | protocol   | Starts the engine, sends requests, prints events, sets exit code     |
| `apps/desktop`      | protocol\* | Avalonia UI (v0.3.0)                                                 |

\* The C# app consumes the protocol through the published JSON Schemas, not the
TypeScript package.

Clients never import engine code. A client finds the engine entry point and
runs it with Node (see [ADR 0009](adr/0009-clients-locate-engine.md)).

## Engine modules

Each module is a folder under `packages/engine/src/` with one job.

| Module      | Job                                                                                                    |
| ----------- | ------------------------------------------------------------------------------------------------------ |
| `rpc`       | Reads and writes newline-delimited JSON-RPC on stdio, dispatches requests, emits events                |
| `project`   | Finds `testtool.config.yaml`, environments, saved logins, test and flow files                          |
| `stepfile`  | Parses YAML with source positions; validates against the schema; resolves flow and target references   |
| `schema`    | Zod schemas for step files and config; maps validation issues to file, line and column                 |
| `actions`   | `defineAction`, the action registry, the built-in actions, loading user actions from the repository    |
| `locators`  | Turns a target's ordered candidates into a Playwright locator; reports which candidate matched         |
| `context`   | Builds `ctx` per test: pages, request, vars, env, secrets, log; variable interpolation; secret masking |
| `runner`    | Runs tests: before → steps → after, data rows, flow calls, timeouts, cancellation, result events       |
| `artifacts` | Saves the screenshot and page snapshot per step; opens a snapshot in the test browser on request       |
| `browser`   | Launches the browser, creates a fresh context per test, applies saved logins                           |
| `errors`    | Typed error classes with a stable `code`, a tester-readable message and the source location            |

The engine has no knowledge of any UI. Everything a client needs to show is in
protocol events; everything a client can ask for is a protocol request.

## Lifecycle of a run

1. The client spawns `node <engine>/dist/main.js --stdio` and sends `initialize`
   with the protocol version it speaks. The engine replies with its own version
   and capabilities, or an error if the major versions differ.
2. `openProject` points the engine at a repository root. The engine reads
   `testtool.config.yaml` and loads the user's action files (a broken action
   file is reported as a diagnostic, not a crash).
3. `startRun` with a list of test files, an environment name and options.
   The engine validates every file first. If any file has errors, the run does
   not start and the response lists the diagnostics.
4. For each test (and each data row, if the test has data):
   1. New browser context (fresh cookies, storage, cache). Saved logins named by
      the test's pages are applied from the login cache, refreshing it by
      running the login flow when missing or expired.
   2. `before` steps, then `steps`. The first failing step stops the test.
   3. `after` steps **always** run, even after a failure or cancellation. A
      failing `after` step is reported but does not stop the other `after`
      steps.
   4. After each step: a screenshot and a page snapshot are saved and announced
      with `screenshotReady` / `snapshotReady`.
   5. The context is closed.
5. `runFinished` carries the totals. The client may then send `shutdown`.

Tests run one at a time in v0.1.0. Parallel workers can be added later without
changing the protocol, because every event carries its `testId`.

## Where things are stored

| What                     | Where                                                    | In Git?   |
| ------------------------ | -------------------------------------------------------- | --------- |
| Tests, flows, targets    | anywhere in the repo, `*.test.yaml`, `*.flow.yaml`       | yes       |
| User actions             | `actions/**/*.ts` (configurable)                         | yes       |
| Project config           | `testtool.config.yaml` at the repo root                  | yes       |
| Secrets                  | process environment variables, optionally a local `.env` | **never** |
| Saved login state        | `.testtool/logins/<env>/<login>.json`                    | **never** |
| Run results, screenshots | `.testtool/runs/<runId>/…`                               | **never** |

## Secrets

Secrets are read only from the process environment (and a local `.env` file
that is git-ignored), declared by name in the config. The engine collects every
secret value it has read and replaces it with `•••` in every log line, event,
error message, variable dump and saved snapshot. Screenshots cannot be masked
pixel by pixel; password fields are already masked by the browser, but a secret
typed into a plain text field is visible in its screenshot. This is documented
as a known limitation.

## Errors

Every error the engine raises extends `TestToolError` with:

- `code`: stable, machine-readable (`StepFileInvalid`, `TargetNotFound`,
  `AssertionFailed`, `ActionTimeout`, `UnknownAction`, `FlowNotFound`, …),
- `message`: one or two sentences a tester can act on,
- `location`: file, line, column of the step that caused it (when known),
- `hint`: optional next step (for example "Run the recorder to refresh this
  target" or "Did you mean `expect.text`?").

The protocol carries the same fields, so clients show identical messages.

## Testing strategy

- **Unit tests** (Vitest) for every module that has logic: parsing, schema
  issues to line numbers, interpolation, masking, locator ordering, registry,
  runner state machine (with a fake browser), protocol framing.
- **Integration tests** (Vitest) start `examples/demo-app` on a free port, run
  real step files through the engine in a real Chromium, and assert on the
  event stream. Every built-in action has at least one integration test.
- **Protocol tests** spawn the engine as a process, exactly as a client does.

## Open questions

Collected from all proposal documents; numbered for easy reference.

1. **Product name.** "Test Tool" is used as a working name. The npm scope
   (`@test-tool/*`), the CLI command (`testtool`), the config file name and the
   `.testtool/` folder all derive from it. What is the real name?
2. **Meaning of `ctx.env`.** Proposed: the selected _environment profile_ from
   the config (base URL and non-secret values for `local`, `staging`, …), not
   the raw process environment. See [step-format.md](step-format.md#environments).
3. **Scope of "repeat for each data row".** Proposed: a test may declare `data`
   and runs once per row, each row a separate test result with a fresh browser
   context; and a `call` step may also repeat a flow per row (`forEach`).
   Do you want both, or only one?
4. **Browsers in v0.1.0.** Proposed: Chromium only (Playwright's bundled
   build, plus optionally installed Chrome or Edge). Firefox and WebKit later.
5. **Page snapshot format.** Proposed: MHTML captured with the Chrome DevTools
   Protocol, which Chromium opens natively ([ADR 0007](adr/0007-page-snapshot-format.md)).
   Depends on question 4.
6. **New tabs and pop-ups.** A click that opens a new tab needs a way to name
   that tab. Proposed: an optional `opens: <pageName>` parameter on `click`
   (and `press`). This is a small addition to the built-in action list.
7. **Protocol version before v1.** Proposed: the protocol stays at `0.x` (breaking
   changes allowed with a minor bump) until the desktop app ships in v0.3.0,
   then becomes `1.0.0` with strict rules.
8. **Naming of user actions.** Proposed: user actions may not reuse a built-in
   name; we recommend (but do not require) a project prefix such as `shop.login`.
9. **Additions to `defineAction` and `ctx`.** The brief lists
   `{ name, params, run }` and `ctx` with `page, request, vars, env, secrets,
log`. Proposed additions: `description` and `shorthand` on actions;
   `ctx.locate(target)` (needed so built-in and user actions resolve targets the
   same way, and so `expect.visible: false` can work) and `ctx.signal` (for
   timeouts and cancellation). See [actions.md](actions.md).
10. **CLI package dependency on the engine.** To find and start the engine, the
    CLI would list `@test-tool/engine` as a package dependency without importing
    any of its code ([ADR 0009](adr/0009-clients-locate-engine.md)). Is that
    acceptable under the dependency rule?
11. **Node patch version.** `.nvmrc` pins 24.11.0 because it is installed here.
    The current Node 24 LTS is 24.21.0 (security fixes). May I raise the pin
    once you have upgraded locally?
12. **Copyright holder.** The README says "Copyright © 2026. All rights
    reserved." with no name. Whose name should it carry?
