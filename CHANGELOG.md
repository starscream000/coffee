# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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
