# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- Review 0004 masking and secrets findings: protocol messages are masked field
  by field with one rules table next to the schemas, so masking keeps every
  message valid JSON that matches its schema and never changes keys or
  identifiers; console output and internal error logs on stderr are masked;
  `${…}` paths reach own properties only; an empty environment variable counts
  as unset, so the `.env` value is used.
- Review 0004 loader findings: user actions that import CommonJS packages
  using Node built-ins load; the load-error hint fits the cause; a changed
  imported file is picked up by the next `openProject` in the same engine; stale
  bundles are removed from the action cache.
- Review 0003: no console method can write to stdout; the engine answers every
  received request before exiting when stdin closes; `validate` on a folder is
  a diagnostic; an unclosed `${` is reported; regular expressions are checked
  when validating; clearer messages for a numeric duration, an empty
  `validate` and a target written straight after an action; shared targets are
  re-read on every `validate`; environment values may not be named `name` or
  `baseUrl`.

### Added

- `ctx.locate` in the engine: resolves a target (by name, file-local then
  shared, or inline) to the Playwright locator of the first candidate that
  matches exactly one element, with `frame` and `within` chains, `${…}` in
  candidates, the `fallbackGrace` period, polling every 50 ms until the step's
  deadline, at-once cancellation, and `TargetNotFound` listing every candidate's
  last match count. Each call is reported as a `LocatorUse` and each fallback as
  a `LocatorFallback` warning, through an interface the runner will implement.
- Playwright 1.64.0 in the engine. The SDK's `Page`, `APIRequestContext` and
  `Locator` are Playwright's own types, and user actions that import
  `playwright` get the engine's copy.
- `pnpm test:integration` runs the browser tests (`*.integration.test.ts`) in
  headless Chromium; `pnpm verify` stays free of any browser. CI has a new
  `integration` job on Linux, Windows and macOS with the browser download
  cached.

- Variables, run-time interpolation and environment profiles in the engine;
  secrets loaded from the process environment and `.env`, rejected under 4
  characters, and reported by `validate` where a test uses one without a value.
  Every protocol message and every line on stderr is masked, before any
  truncation.
- User actions: `@cfe/engine/sdk` (`defineAction`, `target()`, `z`,
  `ActionError`, `AssertionError` and the type of `ctx`); one registry for
  built-in and user actions with the naming rules of ADR 0016; loading with
  esbuild into `.cfe/cache/actions/` against the engine's single SDK copy;
  user actions in `openProject`, `validate` and the new `listActions`.
- Protocol: an optional `data` object on the `log` event; `LocatorFallback`
  carries `{ target, candidateIndex }` there. A compatible change; the protocol
  stays 0.1.0.
- Review 0003 rulings written into the specification: the shared targets file
  layout, `call` paths relative to the project root, `${env.X}` and its reserved
  names, `row` not in flows, page checks in flows, default globs, where each
  diagnostic points, and the protocol details of rulings 7 to 13.
- `openProject` and `validate` requests: the engine finds a project's files
  from its config, and validation adds the cross-file checks (targets and their
  cycles, flows with their parameters and cycles, pages, logins, data files,
  `${…}` namespaces, declared secrets and environment values). The demo app
  has its config and the invalid fixtures for checks F4, F5 and F6.
- Step-file schemas in the engine: tests, flows, shared targets and the config,
  the specs of all 25 built-in actions, and per-file validation that reports
  every problem with file, line and column (with "did you mean" hints) and
  brings each step into its canonical long form.
- Engine over stdio: `node packages/engine/dist/main.js --stdio` speaks the
  protocol line by line, with the version handshake (refusing an incompatible
  client with exit code 3), the 4 MiB message limit and truncation, standard
  JSON-RPC errors, `shutdown`, and `console` output redirected to stderr.
- Protocol 0.1.0 in `@cfe/protocol`: every request, response, event and shared
  type as a Zod schema with inferred types; JSON Schema files generated into
  `packages/protocol/schema/` (`pnpm generate:schemas`); `MAX_MESSAGE_BYTES`,
  the error-code table and the version-compatibility rule.
- pnpm workspace monorepo with `protocol`, `engine` and `cli` packages.
- TypeScript (strict), ESLint, Prettier and Vitest configuration.
- CI workflow running lint, format check, type check and tests on Linux,
  Windows and macOS.
- `CLAUDE.md`, `README.md` and `CONTRIBUTING.md`.
- Product identity defined once in `packages/protocol/src/product.ts`: display
  name "Coffee", command `cfe`, data folder `.cfe` and npm scope `@cfe`.
- Design proposals: architecture, step file format, actions and engine
  protocol, with architecture decision records in `docs/adr/`, and the
  v0.1.0 definition of done.
- Handoff protocol in `handoff/`: numbered instructions, reports and reviews
  that pass work between the reviewer and the implementer.
- Design additions from the reviews: target frames and `within`, `skip` with
  a reason, saved-login `maxAge` and `freshLogin`, fixed viewport, locale and
  timezone, the `snapshots`, `fallbackGrace` and `keepRuns` settings, a 4 MiB
  protocol message limit, and a "Not in v0.1.0" list of deliberate gaps.
- ADRs 0018 (saved-login cache) and 0019 (four product names), and the v0.1.0
  plan in `docs/milestones/v0.1.0-plan.md`.

### Changed

- Git rules: pull requests are merged into `main` only by the reviewer, on the
  owner's word.
- ADRs 0005 to 0017 accepted. Page snapshots use Playwright tracing (one chunk
  per step, measured at about 20 ms and 7 KB per step on a small page), user
  actions use the engine's single SDK copy, built-in names keep their dots with
  `expect`, `wait`, `api` and the command name reserved as namespaces.
- Review 0001 fixes: sensitive response headers keep their real values in
  variables and are registered as secrets for the rest of the run; old saved
  logins are deleted at the start of a run; the login cache key is described
  the same way everywhere; the plan splits the runner into two branches.
- Final product names applied: command `cfe`, data folder `.cfe`, config file
  `cfe.config.yaml`, npm scope `@cfe` (`@cfe/protocol`, `@cfe/engine`,
  `@cfe/cli`); the repository's root package is `coffee`.
- Milestone 1 approvals: the v0.1.0 plan and definition of done are approved,
  ADR 0018 is accepted, and ADRs 0020 (the protocol is defined once, as Zod
  schemas) and 0021 (actions are described by an `ActionSpec`, separate from
  their code) record the reviewer's decisions.
