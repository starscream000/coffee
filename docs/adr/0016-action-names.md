# 0016. Namespace user actions; reserve plain names for built-ins

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

Built-in and user actions share one registry and one syntax in step files. A
user action named like a future built-in would break when that built-in ships.

## Decision

- An action name matches `^[a-z][a-zA-Z0-9]*(\.[a-z][a-zA-Z0-9]*)?$`.
- **User actions must be namespaced**: `<namespace>.<name>`, for example
  `auth.fillOtp`.
- **Names without a dot are reserved for built-ins**, now and in future.
- **Reserved namespaces** (owner decision, 2026-10-09): `expect`, `wait`, `api`
  and the product's command name (`PRODUCT.command`, so `cfe.`).
  Built-in names keep their dots (`expect.text`, `wait.url`). The list lives
  in **one place** in the engine, a single exported constant in the `actions`
  module:

  ```ts
  export const RESERVED_NAMESPACES: readonly string[] = ['expect', 'wait', 'api', PRODUCT.command];
  ```

  Built-ins and the name check both read it; nothing else lists namespaces.

- A user action in a reserved namespace fails at load time with
  `ActionNamespaceReserved`, for example: `Action "expect.priceFormat" uses
the namespace "expect", which is reserved for built-in actions. Use your own
namespace, for example "shop.expectPriceFormat".`
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
- **Renaming built-ins to drop the dot (`expectText`)**: reads less well in
  YAML and loses the grouping in completion lists.
- **Reserving only today's built-in namespaces (`expect`, `wait`)**: `api`
  is a likely namespace for future HTTP built-ins, and the product's own name
  is the natural place for product-specific built-ins.

## Consequences

Users can always tell built-ins from their own actions at a glance, and new
built-ins can never collide with user actions.

## Revisit when

The project adds a third kind of action source (for example shared action
packages), which may need a two-level namespace.
