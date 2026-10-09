# 0003. Pin Node 24 LTS and TypeScript 6.0

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

The brief asks to pin the Node version. TypeScript 7 (the native port) is
released, but typescript-eslint 8.71 supports only TypeScript below 6.1.

## Decision

- Node 24 LTS, exact version in `.nvmrc`, used by CI through
  `node-version-file`; `engines` `>=24.11.0 <25`, `engine-strict=true`. The pin
  is 24.11.0 today and will be raised to the current 24 LTS patch (24.21.0 at
  the time of writing) once the owner confirms the local upgrade.
- ESM only (`"type": "module"`, `module: NodeNext`).
- TypeScript 6.0.x with `strict`, `noUncheckedIndexedAccess`,
  `verbatimModuleSyntax` and explicit `types: ["node"]` (TypeScript 6 no longer
  includes every installed `@types` package by default).
- eslint-plugin-jsdoc is held at 63.x because 64 and later require Node 24.15.

## Alternatives rejected

- TypeScript 7: type-aware linting (required by our standards) would not work.
- Dropping type-aware lint rules to allow TypeScript 7: weakens "no `any`"
  enforcement.
- Node 22: older LTS line, ends sooner, no benefit.

## Consequences

Raising the Node patch is a one-line change plus a lockfile check, done in its
own `chore/` branch.

## Revisit when

- typescript-eslint releases a version supporting TypeScript 7 (move to 7), or
- the owner confirms the local Node upgrade (raise `.nvmrc` to the current 24
  LTS patch and lift the eslint-plugin-jsdoc hold), or
- Node 24 reaches end of maintenance (April 2028).
