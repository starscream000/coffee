# 0009. Clients locate the engine by path and start it with Node

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

Clients must start the engine as a separate process and never import it. The
CLI still has to find the engine's entry point on disk.

## Decision

- The CLI lists `@test-tool/engine` as a package dependency **only so that it
  is installed alongside**. It finds the entry point with
  `import.meta.resolve('@test-tool/engine/main')` and spawns it with the same
  Node executable (`process.execPath`).
- The ESLint rule that blocks importing engine code from clients stays in place
  and covers type-only imports too.
- `--engine <path>` and the `COFFEE_ENGINE` environment variable (prefix from
  `PRODUCT.envPrefix`) override the location, for development and for the
  server.
- The desktop app ships a Node runtime and the engine build, and starts it the
  same way.

## Alternatives rejected

- **No package dependency, path configured by the user**: every CLI user would
  have to install and point at the engine by hand.
- **Bundling the engine into the CLI package**: duplicates the engine and blurs
  the process boundary.
- **Importing the engine in-process from the CLI**: breaks the rule that every
  client uses the protocol, and the CLI would stop testing the protocol.

## Consequences

The dependency rule ("clients depend on protocol") holds for code; the package
dependency exists only for installation. The engine's `main` export becomes
part of the public contract.

## Revisit when

The engine is distributed separately from the CLI (for example a standalone
binary or a container image), so the CLI must discover it another way.
