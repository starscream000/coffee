# 0021. Describe actions with an ActionSpec, separate from their code

- Status: Accepted (reviewer for the owner, 2026-10-09)
- Date: 2026-10-09

## Context

The step-file validator must know every built-in action's name, shorthand and
parameter schema, to normalise shorthand and to check parameters. The v0.1.0
plan delivers the validator in branch 3 but the actions' code in branches 8 and
11 to 13. `defineAction({ name, params, run })` (actions.md) bundles the
description and the code together.

## Decision

- An **`ActionSpec`** holds what can be known without running anything: `name`,
  `description`, `shorthand` (optional) and the `params` Zod schema.
- A **runnable action** is an `ActionSpec` plus `run(ctx, params)`.
- The engine keeps the specs of all built-in actions in one list, available
  from the start; each built-in's `run` is added in the branch that implements
  it. The validator only ever needs specs.
- `defineAction` (plan branch 4) takes the same fields as `ActionSpec` plus
  `run`, and returns a runnable action. User actions are therefore described
  by the same `ActionSpec` shape as built-ins, and `listActions` reports specs.

## Alternatives rejected

- **Validate parameters only when the action runs**: the validator could not
  report parameter errors with file and line before a run, which ADR 0006
  requires.
- **Define every built-in completely (with `run`) before the validator**:
  delays the validator until the runner and the browser exist, against the
  plan's order.
- **A separate parameter list maintained by hand for the validator**: two
  descriptions of each action that drift.

## Consequences

The validator and `listActions` work before any action can run. A built-in
whose spec exists but whose `run` does not yet exist validates but cannot run;
until v0.1.0 is complete, the runner reports such an action as not implemented.

## Revisit when

An action's parameters depend on something only known at run time, which an
`ActionSpec` cannot express.
