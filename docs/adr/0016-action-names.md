# 0016. Namespace user actions; reserve plain names for built-ins

- Status: Accepted (owner, 2026-10-09), except the reserved namespaces (open
  question 13)
- Date: 2026-10-09

## Context

Built-in and user actions share one registry and one syntax in step files. A
user action named like a future built-in would break when that built-in ships.

## Decision

- An action name matches `^[a-z][a-zA-Z0-9]*(\.[a-z][a-zA-Z0-9]*)?$`.
- **User actions must be namespaced**: `<namespace>.<name>`, for example
  `auth.fillOtp`.
- **Names without a dot are reserved for built-ins**, now and in future.
- Proposed (open question 13): the namespaces of built-ins, `expect` and `wait`,
  are reserved as well.
- A violation fails **at load time**, before any test runs, with a diagnostic
  that points at the action file and suggests a name:
  `Action "fillOtp" needs a namespace, for example "auth.fillOtp". Names without
a dot are reserved for built-in actions.` (code `ActionNameNotNamespaced`).
  Duplicate names fail with `ActionNameTaken`, listing both files.

## Alternatives rejected

- **Only forbid exact clashes with today's built-ins** (the first proposal): a
  later built-in could still break a user's tests.
- **A fixed prefix for all user actions (`custom.`, `x.`)**: wastes the
  namespace on noise instead of meaning (`auth.`, `shop.`).
- **Renaming built-ins to drop the dot (`expectText`)**: the alternative for
  question 13; reads less well in YAML and loses the grouping in completion
  lists.

## Consequences

Users can always tell built-ins from their own actions at a glance, and new
built-ins can never collide with user actions.

## Revisit when

The project adds a third kind of action source (for example shared action
packages), which may need a two-level namespace.
