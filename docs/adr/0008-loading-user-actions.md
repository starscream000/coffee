# 0008. Compile user actions with esbuild

- Status: Proposed
- Date: 2026-10-09

## Context

User actions are TypeScript files in the user's repository, loaded when a
project is opened. Node 24 can strip types natively, but only for "erasable"
syntax (no enums, no parameter properties), and it requires explicit `.ts`
extensions in imports.

## Decision

1. Find files with the config's `actions` globs (default `actions/**/*.ts`).
2. Bundle each file with esbuild into ESM in `.coffee/cache/actions/`, with a
   source map. Imports of the user's own files are bundled in; packages are
   resolved from the user's `node_modules`; `@test-tool/engine` and
   `playwright` stay external so the engine provides them.
3. `import()` the output. The default export must be one action or an array of
   actions; the engine checks each object's shape (it does not rely on class
   identity, so the user's copy of the SDK may be a different version).
4. Check names ([ADR 0016](0016-action-names.md)) and that `shorthand` names an
   existing parameter.
5. Every failure (compile error, import error, bad export, bad name, duplicate)
   becomes a diagnostic with file and line; the source map turns runtime stack
   traces into the user's file and line.

The cache is keyed by file content hash, so unchanged files are not rebuilt.

## Alternatives rejected

- **Node's built-in type stripping**: rejects common TypeScript syntax (enums,
  parameter properties) with confusing errors and forces `.ts` import
  extensions on users.
- **tsx or jiti (on-the-fly loaders)**: hook into Node's module loader for the
  whole engine process, which makes errors and caching harder to control.
- **`tsc`**: slow, and type errors in a user's file would block running tests
  that do not use that action.
- **Requiring users to pre-compile actions to JavaScript**: one more build step
  for testers.

## Consequences

All TypeScript syntax works and errors are clear. esbuild is a native
dependency with prebuilt binaries for all three CI platforms. The engine does
not type check user actions; users run `tsc` in their own repository for that.

## Revisit when

Node's type stripping supports the full TypeScript syntax and resolves
extensionless imports, or esbuild stops publishing binaries for a platform we
support.
