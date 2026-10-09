# 0006. Use Zod for schemas, and `yaml` for parsing

- Status: Proposed
- Date: 2026-10-09

## Context

Step files must be validated with file and line in every message. Action
parameters need one definition that gives validation, TypeScript types for
`run`, and a JSON Schema for clients' editing forms.

## Decision

- Parse with the `yaml` package's document API, which keeps source ranges,
  comments and formatting (also needed later to edit files without noisy diffs).
- Describe step files, config and action `params` with Zod 4. Map each Zod
  issue path back to its YAML node to get line and column.
- Produce JSON Schema with Zod's built-in `toJSONSchema` for `listActions` and
  for editor completion.
- Re-export `z` from `@test-tool/engine/sdk` so user actions use the same API.

## Consequences

One schema language for built-in and user actions. Zod becomes part of the
public SDK, so a future Zod major upgrade is a breaking change for users.
Alternative considered: TypeBox with Ajv (JSON Schema native, but less familiar
to testers writing actions).
