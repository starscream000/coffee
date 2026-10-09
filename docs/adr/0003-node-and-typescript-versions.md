# 0003. Pin Node 24 LTS and TypeScript 6.0

- Status: Accepted
- Date: 2026-10-09

## Context

The brief asks to pin the Node version. TypeScript 7 (the native port) is
released, but typescript-eslint 8.71 supports only TypeScript below 6.1.

## Decision

- Node 24 LTS, exact version in `.nvmrc` (currently 24.11.0, the version on the
  development machine), `engines` `>=24.11.0 <25`, `engine-strict=true`.
- ESM only (`"type": "module"`, `module: NodeNext`).
- TypeScript 6.0.x with `strict`, `noUncheckedIndexedAccess`,
  `verbatimModuleSyntax` and explicit `types: ["node"]` (TypeScript 6 no longer
  includes every installed `@types` package by default).
- eslint-plugin-jsdoc is held at 63.x because 64 and later require Node 24.15.

## Consequences

Move to TypeScript 7 when typescript-eslint supports it, in its own `chore/`
branch. Raising the Node patch version is a one-line change plus a lockfile
check.
