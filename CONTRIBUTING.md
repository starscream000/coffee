# Contributing

The binding rules for this repository are in [CLAUDE.md](CLAUDE.md). This page
summarises the day-to-day workflow.

## Setup

1. Install the Node.js version in [`.nvmrc`](.nvmrc).
2. Run `corepack enable` once (or prefix pnpm commands with `corepack`).
3. Run `pnpm install`. The lockfile is committed; do not delete it.

## Everyday commands

| Command          | What it does                                      |
| ---------------- | ------------------------------------------------- |
| `pnpm build`     | Compile all packages                              |
| `pnpm lint`      | ESLint (type-aware, strict)                       |
| `pnpm format`    | Format with Prettier (`format:check` only checks) |
| `pnpm typecheck` | Type check sources and tests                      |
| `pnpm test`      | Build, then run all Vitest tests                  |
| `pnpm verify`    | Everything above; must pass before any merge      |

## Branches and commits

- `main` is always stable. Never commit to it directly; never rewrite its history.
  (The reviewer's handoff files are the one exception; see
  [handoff/README.md](handoff/README.md).)
- One branch per unit of work: `feat/…`, `fix/…`, `docs/…`, `chore/…`, `test/…`.
- [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`,
  `docs:`, `refactor:`, `test:`, `chore:`. Add a body that explains _why_ when
  it is not obvious.
- Before opening a pull request, run `pnpm verify`. Pull requests are merged by
  the reviewer as merge commits, never squashed or rebased.
- Update [CHANGELOG.md](CHANGELOG.md) under "Unreleased" in the same branch as
  the change.

## Code standards

- Strict TypeScript, no `any`.
- Every file starts with a short comment saying what it is for.
- Every exported function, type, class and constant has a TSDoc comment:
  purpose, parameters, return value, errors thrown, and an example where useful.
  ESLint enforces that the comment exists; reviewers check its content.
- Errors are typed and tell a tester what to do next.
- Package dependency rule: `protocol` depends on nothing; `engine` depends on
  `protocol`; clients depend on `protocol` only and start the engine as a
  separate process. ESLint enforces this.
- Unit tests sit next to the code as `*.test.ts`.

## Decisions

Significant technical choices are recorded as ADRs in [docs/adr/](docs/adr/).
Copy [the template](docs/adr/template.md), give it the next number, and add it
to the [ADR index](docs/adr/README.md).

## Secrets

Never commit secrets, saved logins or run output. the product data folder (`.coffee/`) and `.env*`
files are ignored by Git for this reason.
