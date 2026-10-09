# Architecture

> Status: **Proposal, revised after the second review** (2026-10-09). Nothing
> here is implemented yet. Decisions from the reviews are listed in
> [Review decisions](#review-decisions); questions still open are in
> [Open questions](#open-questions).

The product is called **Coffee**. Its command, data folder and npm scope are
separate names, still to be chosen because `coffee` and `.coffee` belong to
CoffeeScript ([ADR 0019](adr/0019-four-product-names.md)). In code all four come
only from `PRODUCT` in `packages/protocol/src/product.ts`. This document uses
the interim values: the `coffee` command, `coffee.config.yaml`, the
`.coffee/` data folder and the `@test-tool` scope.

## Goals

- Tests are YAML step files in the user's Git repository. The files are the
  source of truth; the tools only read and write them.
- One headless engine does all the work (validate, run, later record). Every
  user interface is a separate client that talks to it over one documented,
  versioned protocol.
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
 │                     schema     context   pagestate ── browser        │
 │                                 secrets  results     (Playwright)    │
 └──────────────────────────────────────────────────────────────────────┘
                     reads/writes ▼
        user's Git repository: *.test.yaml, *.flow.yaml, actions/**/*.ts,
        coffee.config.yaml; run output in .coffee/ (git-ignored)
```

| Package / app       | Depends on | Responsibility                                                       |
| ------------------- | ---------- | -------------------------------------------------------------------- |
| `packages/protocol` | nothing    | TypeScript types and JSON Schemas for every request, response, event |
| `packages/engine`   | protocol   | Everything that understands step files or touches a browser          |
| `packages/cli`      | protocol\* | Starts the engine, sends requests, prints events, sets exit code     |
| `apps/desktop`      | protocol†  | Avalonia UI (v0.3.0)                                                 |

\* The CLI also lists the engine package as an install-time dependency, only to
find its executable; ESLint blocks any code import ([ADR 0009](adr/0009-clients-locate-engine.md)).
† The C# app consumes the protocol through the published JSON Schemas.

## Engine modules

Each module is a folder under `packages/engine/src/` with one job.

| Module      | Job                                                                                                      |
| ----------- | -------------------------------------------------------------------------------------------------------- |
| `rpc`       | Newline-delimited JSON-RPC on stdio, version handshake, dispatch, event emission; masks every message    |
| `project`   | Finds `coffee.config.yaml`, environments, saved logins, test, flow and target files                      |
| `stepfile`  | Parses YAML with source positions, validates, normalises shorthand to the long form, resolves references |
| `schema`    | Zod schemas for step files and config; maps validation issues to file, line and column                   |
| `actions`   | `defineAction`, registry, built-in actions, loading and name-checking user actions                       |
| `locators`  | `ctx.locate`: tries a target's candidates in order and records which one matched                         |
| `context`   | Builds `ctx` per test: pages, request, vars, env, secrets, log, locate, signal; interpolation            |
| `secrets`   | Loads declared secrets, keeps the registry of values to mask, masks text and artifacts                   |
| `runner`    | Runs tests: data rows, before → steps → after, flow calls, timeouts, cancellation, `opens`, events       |
| `pagestate` | Internal `PageStateRecorder` interface: screenshot and snapshot per step, open a snapshot on request     |
| `results`   | Writes the run folder (`run.json`, `events.ndjson`, per-step artifacts)                                  |
| `browser`   | Launches the configured browser, fresh context per test, applies saved logins                            |
| `errors`    | Typed error classes with a stable `code`, a tester-readable message, the location and a hint             |

The engine has no knowledge of any UI. Everything a client needs to show is in
protocol events; everything a client can ask for is a protocol request.

### Our own runner, not Playwright Test

The engine drives the Playwright **library** with its own runner
([ADR 0012](adr/0012-own-runner.md)). Playwright Test is built to run
`.spec.ts` files from its own command line in short-lived worker processes; we
need step-level control (per-step screenshots and snapshots, `after` steps that
always run, cancellation between and within steps, locator-fallback reporting)
and a long-lived engine process that streams events to the desktop app.

### Browsers

v0.1.0 supports Chromium only. The browser is still a configuration value
(`defaults.browser` in the config, `options.browser` in `startRun`), validated
against the engine's `capabilities.browsers`. Neither the protocol nor the
public engine API names a browser type; Chromium-specific code (if any) stays
behind the `browser` and `pagestate` modules.

### Page states

After each step the runner calls the internal `PageStateRecorder`, which saves
a screenshot and a snapshot and can open a snapshot later. The snapshot format
is hidden behind this interface, and clients only ever receive a screenshot
path and a `snapshotReady` notice; they open a snapshot through the
`openSnapshot` request. The recommended implementation is Playwright tracing
with one trace chunk per step ([ADR 0007](adr/0007-page-snapshot-format.md)).

## Lifecycle of a run

1. **Handshake.** The client spawns `node <engine>/dist/main.js --stdio`. Its
   first message must be `initialize` with the protocol version it speaks. If
   the versions are incompatible, the engine answers `IncompatibleProtocol`
   with both versions and what to update, then exits with code 3
   ([protocol.md](protocol.md#handshake)).
2. **Project.** `openProject` points the engine at a repository root. The engine
   reads `coffee.config.yaml` and loads user actions. A broken action file, or
   one whose action name has no namespace, is reported as a diagnostic, not a
   crash.
3. **Validation.** `startRun` validates every selected file first. If any file
   has errors, nothing runs and the response lists the diagnostics.
4. **Tests.** For each test, and for each of its data rows as a separate test
   instance:
   1. New browser context per login (fresh cookies, storage, cache). Saved
      logins are applied from the login cache, which is refreshed by running
      the login flow when missing or out of date.
   2. `before` steps, then `steps`. The first failing step stops the test.
   3. `after` steps **always** run, even after a failure or cancellation. A
      failing `after` step is reported but does not stop the other `after`
      steps.
   4. After each step: page state recorded, `screenshotReady` and
      `snapshotReady` sent, then `stepPassed` / `stepFailed`, which name the
      locator candidate each target used.
   5. Contexts are closed.
5. `runFinished` carries the totals. The client may then send `shutdown`.

Tests run one at a time in v0.1.0. Parallel workers can be added later without
changing the protocol, because every event carries its `testId`.

### Cancellation and timeouts

`cancelRun` is a protocol request. It aborts `ctx.signal` for the current step.
Every built-in action honours the signal: waits inside the engine (locating,
`expect.*` retries, `wait.*`) check it on every poll, and Playwright calls are
given at most the step's remaining time. The step is reported as cancelled,
the remaining steps as skipped, and `after` steps still run (with a 30-second
limit).

## Where things are stored

| What                   | Where                                          | In Git?   |
| ---------------------- | ---------------------------------------------- | --------- |
| Tests, flows, targets  | anywhere in the repo, per the config's globs   | yes       |
| User actions           | `actions/**/*.ts` (configurable)               | yes       |
| Project config         | `coffee.config.yaml` at the repo root          | yes       |
| Secrets                | process environment, optionally a local `.env` | **never** |
| Saved login state      | `.coffee/logins/<key>.json` (ADR 0018)         | **never** |
| Compiled user actions  | `.coffee/cache/actions/`                       | **never** |
| Run results, artifacts | `.coffee/runs/<runId>/…`                       | **never** |

The run folder layout is defined in [ADR 0015](adr/0015-results-layout.md).

## Secrets

Secrets are declared by name in the config and read from the process
environment (or a git-ignored `.env`). The raw process environment is never
exposed to step files: `${env.…}` is the environment profile, and the only way
to reach an environment variable is a declared secret through `ctx.secrets` or
`${secrets.…}`.

Masking ([ADR 0014](adr/0014-secret-masking.md)) happens at the points where
data leaves the engine: every protocol message, every file in the run folder,
and every snapshot (rewritten before `snapshotReady`). Screenshots are taken by
the engine with Playwright's `mask` option over elements that show a secret.

## Errors

Every error the engine raises extends `CoffeeError` (named from `PRODUCT` in
code) with:

- `code`: stable, machine-readable (`StepFileInvalid`, `TargetNotFound`,
  `AssertionFailed`, `ActionTimeout`, `UnknownAction`, `FlowNotFound`,
  `ActionNameNotNamespaced`, …),
- `message`: one or two sentences a tester can act on, secrets masked,
- `location`: file, line, column of the step that caused it (when known),
- `hint`: optional next step (for example "Did you mean `expect.text`?").

The protocol carries the same fields, so clients show identical messages.

## Testing strategy

- **Unit tests** (Vitest) for every module that has logic: parsing, schema
  issues to line numbers, shorthand normalisation, interpolation, masking,
  locator ordering, registry and name rules, runner state machine (with a fake
  browser), protocol framing and handshake.
- **Integration tests** (Vitest) start `examples/demo-app`, run real step files
  through the engine in a real Chromium, and assert on the event stream and the
  run folder. Every built-in action has at least one integration test.
- **Protocol tests** spawn the engine as a process, exactly as a client does.
- The exact v0.1.0 acceptance criteria are in
  [milestones/v0.1.0-definition-of-done.md](milestones/v0.1.0-definition-of-done.md).

## Review decisions

Answers from the owner's review of 2026-10-09, folded into the documents:

| #   | Topic                   | Decision                                                                                                           |
| --- | ----------------------- | ------------------------------------------------------------------------------------------------------------------ |
| 1   | Product name            | "Coffee", defined once; later split into four names ([ADR 0019](adr/0019-four-product-names.md))                   |
| 2   | `ctx.env`               | The selected environment profile; raw process environment never exposed; secrets only via `ctx.secrets`            |
| 3   | Data rows               | Whole test only in v0.1.0, each row its own test instance; no `forEach` ([ADR 0013](adr/0013-data-rows.md))        |
| 4   | Browsers                | Chromium only in v0.1.0; browser stays a config field; no Chromium assumption in protocol or public API            |
| 5   | Snapshot format         | Investigated Playwright tracing; recommendation in [ADR 0007](adr/0007-page-snapshot-format.md), awaiting approval |
| 6   | New tabs                | `opens` is a step-level field for any action; unnamed new pages get an automatic name and a warning                |
| 7   | Protocol versioning     | Approved, with a handshake that refuses incompatible clients ([ADR 0011](adr/0011-protocol-versioning.md))         |
| 8   | Action names            | User actions must be namespaced; un-namespaced names are reserved ([ADR 0016](adr/0016-action-names.md))           |
| 9   | Additions               | `description`, `shorthand`, `ctx.locate`, `ctx.signal` approved with conditions (see actions.md)                   |
| 10  | CLI → engine dependency | Accepted for locating the executable only ([ADR 0009](adr/0009-clients-locate-engine.md))                          |
| 11  | Node pin                | Raise to the current 24 LTS patch after the owner confirms the local upgrade; CI uses `.nvmrc`                     |
| 12  | Copyright holder        | starscream000                                                                                                      |

Second review, 2026-10-09:

| #   | Topic                 | Decision                                                                                                                                  |
| --- | --------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| 17  | Names                 | Display name "Coffee" final; command, folder and npm scope split out and chosen by the owner ([ADR 0019](adr/0019-four-product-names.md)) |
| 18  | Frames                | Targets may name the `frame` they are in, nestable; `ctx.locate` resolves it                                                              |
| 19  | Scoping               | Targets may be `within` another target, with interpolation                                                                                |
| 20  | Unset vars in `after` | The step is skipped with "skipped: <name> was never set", not failed                                                                      |
| 21  | Saved logins          | `maxAge` (default 12h), `freshLogin` per test, hashed cache keys ([ADR 0018](adr/0018-saved-logins.md))                                   |
| 22  | Skipping tests        | `skip: "<reason>"`; skipped tests are validated and reported with the reason                                                              |
| 23  | Run settings          | Fixed default viewport, locale and timezone, overridable per environment                                                                  |
| 24  | SDK                   | One SDK copy (the engine's); SDK errors identified by a tag field                                                                         |
| 25  | Ignored `ctx.signal`  | Step failed, its page closed, its `ctx` sealed                                                                                            |
| 26  | Trust model           | Documented in [actions.md](actions.md#trust-model): opening a project runs its code                                                       |
| 27  | Deliberate gaps       | Listed in [step-format.md](step-format.md#not-in-v010)                                                                                    |
| 28  | Release               | v0.1.0 is tagged only after CI passes on all three systems                                                                                |

## Open questions

13. **Built-in names with a dot.** Rule 8 reserves un-namespaced names for
    built-ins, but `expect.text` and `wait.url` contain a dot. Proposed: also
    reserve the `expect` and `wait` namespaces for built-ins. Alternative:
    rename them to `expectText`, `waitForUrl` and so on. See
    [ADR 0016](adr/0016-action-names.md).
14. **Command, folder and npm scope names.** Three options proposed to the
    owner; see [ADR 0019](adr/0019-four-product-names.md).
15. **Snapshot format.** Approve Playwright tracing per step
    ([ADR 0007](adr/0007-page-snapshot-format.md)), or keep MHTML?
16. **Run retention.** Page states take disk space on every run. Should v0.1.0
    keep all runs (users delete `.coffee/runs` themselves) or add a
    `keepRuns` setting?
