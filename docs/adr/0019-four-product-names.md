# 0019. Keep four independent product names

- Status: Accepted (owner, 2026-10-09); the final values are pending
- Date: 2026-10-09
- Supersedes: [0017](0017-product-identity.md)

## Context

The display name "Coffee" is final. ADR 0017 derived the command and the data
folder from it, giving `coffee` and `.coffee`. But `coffee` is CoffeeScript's
command and `.coffee` its file extension: installing both tools would clash, and a
`.coffee` folder looks like a CoffeeScript file to people and to tools that
match on the extension.

## Decision

`PRODUCT` in `packages/protocol/src/product.ts` holds four independent values:

| Field         | Purpose                                                     | Value now              |
| ------------- | ----------------------------------------------------------- | ---------------------- |
| `displayName` | What people read                                            | `Coffee` (final)       |
| `command`     | CLI program name; config file and env prefix derive from it | `coffee` (interim)     |
| `dataDir`     | Git-ignored folder in the user's repository                 | `.coffee` (interim)    |
| `npmScope`    | Scope of all packages, including the user SDK               | `@test-tool` (interim) |

`configFile` (`<command>.config.yaml`) and `envPrefix` (`<COMMAND>_`) are
derived from `command`. Code never writes any of these literally.
`product.test.ts` checks the CLI's `bin` entry, `.gitignore` and the scope of
every workspace package.

The owner chooses the final command, folder and scope from three proposed
options. The rename is then one change to `product.ts`, the files the test
points at, the import specifiers that use the scope, and the docs.

## Alternatives rejected

- **Keep deriving everything from one identifier** (ADR 0017): forces a
  command or folder name that clashes as soon as the display name is not a
  safe identifier.
- **Keep `coffee` and accept the clash**: users with CoffeeScript installed
  would run the wrong program.

## Consequences

Import specifiers (`@test-tool/protocol`) cannot read a constant, so a scope
rename still touches every import; it is a mechanical search and replace,
checked by the type checker.

## Revisit when

The owner picks the final values (apply them), or the product adds another
user-visible name (for example a desktop app bundle identifier), which then
joins `PRODUCT`.
