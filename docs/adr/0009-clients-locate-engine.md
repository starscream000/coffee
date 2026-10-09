# 0009. Clients locate the engine by path and start it with Node

- Status: Proposed
- Date: 2026-10-09

## Context

Clients must start the engine as a separate process and never import it.

## Decision

- The CLI lists `@test-tool/engine` as a dependency only so that it is installed
  alongside. It finds the entry point with
  `import.meta.resolve('@test-tool/engine/main')` and spawns it with the same
  Node executable (`process.execPath`). ESLint forbids importing engine code.
- `--engine <path>` and the `TESTTOOL_ENGINE` environment variable override the
  location, for development and for the server.
- The desktop app ships a Node runtime and the engine build, and starts it the
  same way.

## Consequences

The dependency rule ("clients depend on protocol") holds for code; the package
dependency exists only for installation. The engine's `main` entry point becomes
part of the public contract.
