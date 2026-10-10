# Desktop app

Native desktop client for the product, written in C# with
[Avalonia](https://avaloniaui.net/): a place to browse, check and (as the
engine grows) run and review a project's end-to-end tests.

Status: **in progress** (desktop milestone D1, see the [plan](docs/plan.md)).
Nothing is released.

What it does today: starts the engine of the checkout, opens a project
folder, and shows its tests (as a folder tree with search and tags), every
problem the engine finds (re-checked when files change), the step files
in an editor (undo, redo, save, revert, problem lines marked, and the
engine's problems for the text being typed), the action catalogue with each action's
parameters, and the engine's log.

Saving writes the whole file anew (a temporary file next to it, then moved
over the old one), so a crash never leaves half a file. The new file does not
keep the old one's permissions, and a symbolic link is replaced by a plain
file.

It follows the project folder while it is open: a changed step file (YAML)
re-reads its tab and validates the tests again; a changed config file, or a
changed user-action source (`.ts`, `.mts`, `.cts`, `.js`, `.mjs`, `.cjs`
anywhere under the project, outside dot-folders and `node_modules`), opens
the project again, because the engine reads both only in `openProject`. It cannot run tests yet, because the
engine's runner does not exist yet (desktop milestone D2).

It starts the engine as a separate process and talks to it only through the
engine protocol described in [docs/protocol.md](../../docs/protocol.md). It
never references engine code. This folder is a .NET solution and is not part of
the pnpm workspace.

## Layout

| Path                        | Contents                                                                           |
| --------------------------- | ---------------------------------------------------------------------------------- |
| `src/Desktop.Protocol`      | The engine protocol in C#: message types, framing, version rule, `Product`         |
| `src/Desktop.Engine`        | Finds and starts the engine, handshake, typed requests and events                  |
| `src/Desktop.App`           | The Avalonia app: views, view models, services                                     |
| `tests/*`                   | xUnit tests, one project per source project                                        |
| `docs/`                     | [Architecture](docs/architecture.md), [plan](docs/plan.md), [decisions](docs/adr/) |
| `handoff/`                  | The desktop track's [handoff folder](handoff/README.md)                            |
| `scripts/verify.sh`, `.ps1` | Every check a pull request must pass                                               |

## Getting started

Requirements: the .NET 10 SDK (see [global.json](global.json)), and for a
working engine, Node and pnpm as in the [root README](../../README.md).

```sh
# once, from the repository root: build the engine the app starts
corepack pnpm install
corepack pnpm build

# from apps/desktop
dotnet run --project src/Desktop.App   # start the app
scripts/verify.sh                      # format, build, tests, Prettier
```

The app finds the engine of the checkout by itself
([ADR D0004](docs/adr/0004-finding-node-and-the-engine.md)); Settings can
point it at another Node or engine.

## Commands

```
dotnet build Desktop.slnx                         build everything (warnings are errors)
dotnet test Desktop.slnx                          all tests
dotnet format Desktop.slnx                        apply code style (--verify-no-changes to check)
scripts/verify.sh                                 every check, as before a pull request
```

After adding or upgrading a NuGet package, run
`corepack pnpm exec prettier --write apps/desktop` from the repository root so
the lock files match Prettier.

## Copyright

Copyright © 2026 starscream000. All rights reserved.
