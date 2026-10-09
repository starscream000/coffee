# 0008. Compile user actions with esbuild

- Status: Proposed
- Date: 2026-10-09

## Context

User actions are TypeScript files in the user's repository, loaded when a
project is opened. Node 24 can strip types natively, but only for "erasable"
syntax (no enums, no parameter properties), and it requires explicit `.ts`
extensions in imports.

## Decision

Bundle each action file with esbuild into an ESM file in
`.testtool/cache/actions/`, then `import()` it. Packages are resolved from the
user's `node_modules`; `@test-tool/engine` and `playwright` are kept external.
Compile errors become diagnostics with file and line.

## Consequences

All TypeScript syntax works and errors are clear. esbuild is a native
dependency, with prebuilt binaries for all three CI platforms. The engine does
not type check user actions; users run `tsc` in their own repository for that.
