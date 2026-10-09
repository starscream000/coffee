# 0002. Use a pnpm workspace with tsc project references

- Status: Accepted
- Date: 2026-10-09

## Context

The brief asks for a pnpm workspace with `protocol`, `engine` and `cli`
packages and a strict dependency rule between them.

## Decision

- pnpm 10.34.6, pinned in the root `packageManager` field and supplied by
  Corepack. pnpm 12 is the newest, but the Corepack bundled with Node 24.11
  cannot start it.
- Each package compiles with `tsc` to `dist/`. The root `tsconfig.json` lists
  them as project references, so `tsc -b` builds in dependency order.
- `tsconfig.check.json` type checks sources and tests together without
  emitting; type-aware ESLint uses the same file.
- ESLint `no-restricted-imports` enforces the dependency rule.
- Root scripts call tools directly instead of nested `pnpm` calls, so they work
  when pnpm is only available as `corepack pnpm`.

## Consequences

Packages resolve each other through their built `dist`, so on a fresh clone the
type check (which builds) must run before lint and tests. `pnpm verify` and CI
do this. No bundler is needed for the engine or CLI.
