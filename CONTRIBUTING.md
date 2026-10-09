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
- One branch per unit of work: `feat/…`, `fix/…`, `docs/…`, `chore/…`, `test/…`.
- [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`,
  `docs:`, `refactor:`, `test:`, `chore:`. Add a body that explains _why_ when
  it is not obvious.
- Before merging, run `pnpm verify`. Merge with `git merge --no-ff`.
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

## Secrets

Never commit secrets, saved logins or run output. `.testtool/` and `.env*`
files are ignored by Git for this reason.
