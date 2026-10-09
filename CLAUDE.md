# CLAUDE.md

Standing instructions for every working session in this repository. Read this
file in full before making changes. If anything here conflicts with a request,
ask the owner instead of guessing.

## What we are building

Coffee (placeholder name; the owner will supply the final one) is a desktop tool for recording, editing and running
end-to-end web tests. It has three parts:

1. **Engine**: a headless TypeScript program on Node.js that uses the
   Playwright library directly (not the Playwright CLI). It records, validates
   and runs tests.
2. **Clients of the engine**: first a command-line runner, later a native
   desktop app written in C# with Avalonia, and possibly a VS Code extension
   after that.
3. **Server** (later): runs the same engine in containers for scheduled
   regression runs.

## Decisions already made (do not change without asking the owner)

- Tests are YAML "step files" stored in the user's Git repository. They are the
  source of truth. An "export to plain Playwright code" command will exist later.
- Every step calls an "action". Built-in and user-written actions use one
  mechanism: `defineAction({ name, params, run(ctx, params) })`. User actions are
  TypeScript files in the user's repository, loaded by the engine at start.
- Built-in actions: goto, back, reload, click, fill, select, check, hover, press,
  upload, drag, wait (element / URL / response), expect (visible / text / value /
  URL / count / response), set variable, extract, API call, mock response,
  call another flow.
- No if/else and no free loops in step files. Only "call a flow" and
  "repeat for each data row".
- Each target stores several locator candidates in order of reliability
  (role and name, test ID, CSS last).
- `ctx` holds: `page`, `request`, `vars`, `env`, `secrets`, `log`. Rules: fresh
  browser context per test, named saved logins, named pages for multi-tab or
  multi-user flows, secrets masked everywhere, "after" steps always run.
- The engine must not know about any UI. All clients talk to it through one
  versioned, documented message protocol (JSON-RPC over stdin/stdout), with the
  engine pushing events such as `stepStarted`, `stepPassed`, `stepFailed`,
  `screenshotReady`. Treat this protocol as a public contract.
- **No AI features. Do not add any.**
- Past page states: the engine saves a screenshot and a page snapshot per step.
  Clients show the screenshot; the snapshot is opened in the test browser on
  request.

Further decisions are recorded as ADRs in `docs/adr/`. An ADR with status
"Accepted" is binding; "Proposed" ADRs need owner approval before code relies
on them.

## Repository layout

A pnpm workspace monorepo:

```
packages/protocol   message and event types shared by engine and clients
packages/engine     core: step file parsing, validation, action registry,
                    context, runner, recorder (later)
packages/cli        headless command-line client
apps/desktop        Avalonia app (later milestone; README only for now)
examples/demo-app   a tiny local web app used by integration tests
docs/               architecture, step format, actions, protocol, ADRs
```

**Dependency rule:** `protocol` depends on nothing; `engine` depends on
`protocol`; clients depend on `protocol` and start the engine as a separate
process. Clients never import engine code. ESLint enforces this.

## Engineering standards

- TypeScript in strict mode. No `any`. ESLint and Prettier configured and
  enforced.
- Every file starts with a short comment saying what it is for. Every exported
  function, type and class has a TSDoc comment: purpose, parameters, return
  value, errors thrown, and a short example where useful.
- Small focused modules. Typed errors with clear messages a tester can act on.
- Unit tests with Vitest for all logic. Integration tests that run real step
  files against `examples/demo-app` in a real browser.
- Validate step files against a schema and report errors with file and line.
- Pin the Node version (`.nvmrc`). Commit the lockfile. Never commit secrets.
- Do not add a licence file. The project is "All rights reserved" for now
  (`"license": "UNLICENSED"` in every package.json). Copyright holder:
  starscream000.
- Never write the product name literally in code. Use `PRODUCT` from
  `packages/protocol/src/product.ts`, the single place to rename the product.
  Its test checks the files that cannot import it.
- Do not add features the owner has not asked for.

## Git rules

- `main` is always stable: it builds, lints and passes all tests. Never commit
  directly to `main` and never force-push or rewrite its history.
- One branch per unit of work: `feat/...`, `fix/...`, `docs/...`, `chore/...`,
  `test/...`.
- Small commits using Conventional Commits (`feat:`, `fix:`, `docs:`,
  `refactor:`, `test:`, `chore:`), each with a body explaining why when it is
  not obvious.
- Before merging a branch: run lint, type check and all tests
  (`pnpm verify`). Merge only if everything passes. Use a merge commit
  (`git merge --no-ff`) so each feature stays visible.
- If a GitHub remote exists, push branches and open pull requests with a clear
  description. Ask the owner before creating a remote or pushing for the first
  time.
- Releases use Semantic Versioning with annotated tags (`v0.1.0`). Keep
  `CHANGELOG.md` in "Keep a Changelog" format, updated in the same branch as the
  change.
- CI runs lint, type check and tests on Windows, macOS and Linux.

## How to work

- Before each milestone, give the owner a short plan and wait for the go-ahead.
- If anything is unclear or seems wrong, ask instead of guessing.
- At the end of each working session, report what was merged, what is in
  progress, and what you need from the owner.

### Milestones

- **Milestone 0**: scaffold, tooling, CI, docs as proposals, ADRs.
- **Milestone 1 (v0.1.0)**: step file parser and validator, action registry with
  the built-in actions, context and variables, runner, event stream over the
  protocol, and a CLI that runs a step file against the demo app and prints
  results.
- Later: recorder (v0.2.0), Avalonia desktop app (v0.3.0), server and
  scheduling (v0.4.0).

## Commands

pnpm is provided through Corepack (version pinned in `package.json`
`packageManager`). If `pnpm` is not on the PATH, prefix commands with
`corepack`, for example `corepack pnpm install`.

```
pnpm install          install dependencies (lockfile is committed)
pnpm build            compile all packages (tsc -b)
pnpm lint             ESLint
pnpm format:check     Prettier check (pnpm format to fix)
pnpm typecheck        type check all packages
pnpm test             Vitest, all packages
pnpm verify           type check, lint, format check, tests; must pass before any merge
                      (lint needs a build, so a fresh clone runs typecheck first)
```
