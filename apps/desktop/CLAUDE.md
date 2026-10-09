# CLAUDE.md (desktop app)

Standing instructions for every session working on the desktop app. The
repository's [CLAUDE.md](../../CLAUDE.md) applies too; this file adds to it and,
for work in this folder, takes precedence where the two differ. If anything here
conflicts with a request, ask the owner instead of guessing.

## Scope

- Change files **only under `apps/desktop/`**. Read anything in the repository;
  edit nothing outside this folder, not even the root `handoff/`,
  `CHANGELOG.md`, `.github/` or `.prettierignore`.
- Anything needed outside this folder is a request in
  [handoff/requests/](handoff/requests/) (see [handoff/README.md](handoff/README.md)).

## How to work

At the start of every session:

1. `git switch main` and `git pull --ff-only`.
2. Read [handoff/STATUS.md](handoff/STATUS.md). Carry out the open desktop
   instructions in the order listed, and nothing else. If none is open, do
   nothing and say so.
3. Work on the branches the instruction names (`desktop/<type>/<name>`).
4. Write the report in `handoff/reports/` on the last branch, run
   `scripts/verify.sh` and the root `pnpm verify`, push, open the pull
   requests, and stop. Never merge into `main` or push to it.

## Engineering standards (C#)

- Nullable reference types on, warnings are errors, analyzers at
  `latest-recommended`, code style enforced (`dotnet format`). No `dynamic`,
  no `#pragma warning disable` without a comment saying why (list each one in
  the report).
- Every file starts with a short comment saying what it is for. Every public
  type and member has an XML doc comment: purpose, parameters, return value,
  exceptions, and an example where useful.
- MVVM: views only bind; view models hold the logic; services wrap the
  operating system. View models are tested without a window.
- Small focused types. Typed exceptions with messages that tell the user what
  to do next.
- The dependency rule: `Desktop.Protocol` → nothing; `Desktop.Engine` →
  `Desktop.Protocol`; `Desktop.App` → both. Never reference engine code; talk
  to the engine only through the protocol.
- Never write the product's names literally in code: use `Product` in
  `Desktop.Protocol` (checked against `packages/protocol/src/product.ts`).
- The protocol types must match `packages/protocol/schema/`; the contract tests
  say where they do not ([ADR D0003](docs/adr/0003-hand-written-protocol-types.md)).
- Exact package versions in `Directory.Packages.props`; commit the lock files.
  Add a package only in the branch that first needs it.
- No AI features. Do not add features the owner has not asked for.
- `"license"`: none. Copyright holder: starscream000.

## Git

- Branches `desktop/<type>/<name>` with `feat`, `fix`, `docs`, `chore`, `test`.
- Small Conventional Commits with a body explaining why when it is not obvious.
- Update [CHANGELOG.md](CHANGELOG.md) in the same branch as the change.
- Never rewrite pushed history; never force-push.

## Commands

```
dotnet build Desktop.slnx          build (warnings are errors)
dotnet test Desktop.slnx           all tests
dotnet format Desktop.slnx         apply code style
scripts/verify.sh                  every check before a pull request
```
