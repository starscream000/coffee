# 0019. Keep four independent product names

- Status: Accepted (owner, 2026-10-09); final values applied on 2026-10-09
- Date: 2026-10-09
- Amends: [0017](0017-product-identity.md)

## Context

The display name "Coffee" is final. ADR 0017 derived the command and the data
folder from it, giving `coffee` and `.coffee`. But `coffee` is CoffeeScript's
command and `.coffee` its file extension: installing both tools would clash, and a
`.coffee` folder looks like a CoffeeScript file to people and to tools that
match on the extension.

## Decision

`PRODUCT` in `packages/protocol/src/product.ts` holds four independent values.
The final values were chosen on 2026-10-09: the owner chose the command `cfe`,
and the reviewer chose the folder and scope to match.

| Field         | Purpose                                                     | Final value | Interim value until 2026-10-09 |
| ------------- | ----------------------------------------------------------- | ----------- | ------------------------------ |
| `displayName` | What people read                                            | `Coffee`    | `Coffee`                       |
| `command`     | CLI program name; config file and env prefix derive from it | `cfe`       | `coffee`                       |
| `dataDir`     | Git-ignored folder in the user's repository                 | `.cfe`      | `.coffee`                      |
| `npmScope`    | Scope of all packages, including the user SDK               | `@cfe`      | `@test-tool`                   |

Derived from `command`: the config file `cfe.config.yaml` and the environment
variable prefix `CFE_`. The packages are `@cfe/protocol`, `@cfe/engine` and
`@cfe/cli`; the root package of the repository is `coffee`.

Code never writes any of these literally. `product.test.ts` checks the CLI's
`bin` entry, `.gitignore` and the scope of every workspace package.

**Warning for documentation and instructions.** The unscoped npm package `cfe`
exists and belongs to someone else. Always name the scoped package
(`@cfe/cli`). Never suggest `npx cfe` or `npm install cfe`: either would fetch
and run a stranger's package.

## Alternatives rejected

- **Keep deriving everything from one identifier** (ADR 0017): forces a
  command or folder name that clashes as soon as the display name is not a
  safe identifier.
- **Keep `coffee` and accept the clash**: users with CoffeeScript installed
  would run the wrong program.
- **`coffeeqa`, `coffeetest` or `coffee-e2e`** (proposed in report 0001): free
  on npm, but longer to type; the owner chose `cfe`.

## Consequences

Import specifiers (`@cfe/protocol`) cannot read a constant, so a scope rename
touches every import; it is a mechanical search and replace, checked by the
type checker. The short command is easy to type, at the cost of the unscoped
npm name belonging to someone else.

## Revisit when

The product adds another user-visible name (for example a desktop app bundle
identifier), which then joins `PRODUCT`, or the owner of the unscoped `cfe`
package causes confusion for users (for example in search results or support
questions).
