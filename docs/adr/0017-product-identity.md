# 0017. Define the product identity in one constant

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

The final product name is not chosen yet. The name shows up in the command,
the config file name, the data folder, environment variables and messages. The
rename must be a single change.

## Decision

- `packages/protocol/src/product.ts` defines `PRODUCT_NAME` ("Coffee",
  placeholder) and `PRODUCT_ID` ("coffee"). Everything else is derived:
  `PRODUCT.command`, `PRODUCT.configFile`, `PRODUCT.dataDir`,
  `PRODUCT.envPrefix`.
- Code never writes these names literally; it imports `PRODUCT` (the protocol
  package depends on nothing, so every package and client can).
- Files that cannot import it (`.gitignore`, the CLI's `bin` entry in
  `package.json`) are checked by `product.test.ts`, which fails and names the
  file when they disagree.
- Documentation uses the derived names and states that they are placeholders.

## Alternatives rejected

- **A JSON file read at build time**: needs a code generation step for the
  values that must be literal (`bin`, `.gitignore`) anyway.
- **Search and replace when the name is known**: the name would be spread over
  dozens of places, and a missed one is found only by users.

## Consequences

Renaming means changing two constants and the files the test points at, then
updating the docs. The npm scope (`@test-tool/*`) is user-visible through
`@test-tool/engine/sdk` but cannot come from a constant (open question 14).

## Revisit when

The owner supplies the final product name (rename, then decide whether to keep
this indirection).
