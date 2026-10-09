# 0006. Use Zod for schemas, and `yaml` for parsing

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

Step files must be validated with file and line in every message. Action
parameters need one definition that gives validation, TypeScript types for
`run`, and a JSON Schema for clients' editing forms.

## Decision

Validation runs in three passes, all reporting `Diagnostic`s with file, line and
column:

1. **Parse** with the `yaml` package's document API (`parseDocument` with a
   `LineCounter`), which keeps a source range for every node, plus comments and
   formatting. YAML syntax errors become diagnostics directly.
2. **Structure**: Zod 4 schemas describe test, flow, targets and config files.
   For each step the action key is looked up in the registry; the bare and
   shorthand forms are normalised to the canonical long form; the action's own
   Zod `params` schema validates it. Each Zod issue path is mapped back to its
   YAML node to get line and column.
3. **References**: target names, flow paths, flow parameters, flow cycles,
   page names (`page` and `opens`), `${…}` namespaces, declared secrets and
   environment values are checked across files.

Zod's built-in `toJSONSchema` produces the JSON Schemas for `listActions` and
for editor completion. `z` is re-exported from `@cfe/engine/sdk` so user
actions use the same API.

## Alternatives rejected

- **JSON Schema + Ajv**: language-neutral, but gives no TypeScript types for
  `run` without a second tool, and its error paths are harder to turn into
  tester-friendly messages.
- **TypeBox + Ajv**: JSON Schema native with types, but less familiar to testers
  writing actions, and one more concept in the public SDK.
- **js-yaml**: no source positions for nodes and no comment preservation.

## Consequences

One schema language for built-in and user actions. Zod becomes part of the
public SDK, so a future Zod major upgrade is a breaking change for users.

## Revisit when

Zod releases a new major version, or a client in another language (C#) needs to
validate step files itself without the engine.
