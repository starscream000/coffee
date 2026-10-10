# Desktop app

Native desktop client for the product, written in C# with
[Avalonia](https://avaloniaui.net/): a place to browse, check and (as the
engine grows) run and review a project's end-to-end tests.

Status: **in progress** (desktop milestones D1 to D4 and D5's placeholder,
see the [plan](docs/plan.md)). Nothing is released.

What it does today:

- **Opens a project** and shows its tests (a folder tree with search and
  tags), every problem the engine finds (checked again when files change), the
  action catalogue, and the engine's log.
- **Edits tests without YAML**: a step list beside the text of each test and
  flow (add, remove, duplicate, move steps), a form for each step built from
  its action's parameters, a targets editor (candidates in order of
  reliability, frame and within), and new, renamed and deleted files. The text
  editor and these views show the same text at once, and a change rewrites
  only the lines it is about, comments included.
- **Runs tests**: all, a folder, a tag or one test, with the browser shown or
  not; a run view with each step's result, a failure's expected and actual
  values and locator candidates, screenshots once the engine records them,
  and a link from a failed target to the targets editor; cancel; and the
  history of earlier runs.
- **Recording** has its place (Record), which says that it arrives with a
  later engine version.

Saving writes the whole file anew (a temporary file next to it, then moved
over the old one), so a crash never leaves half a file. The new file does not
keep the old one's permissions, and a symbolic link is replaced by a plain
file.

It follows the project folder while it is open: a changed step file (YAML)
re-reads its tab and validates the tests again; a changed config file, or a
changed user-action source (`.ts`, `.mts`, `.cts`, `.js`, `.mjs`, `.cjs`
anywhere under the project, outside dot-folders and `node_modules`), opens
the project again, because the engine reads both only in `openProject`.

It starts the engine as a separate process and talks to it only through the
engine protocol described in [docs/protocol.md](../../docs/protocol.md). It
never references engine code. This folder is a .NET solution and is not part of
the pnpm workspace.

## Walk-through: from a fresh clone to a passing run

This builds the engine and the app, starts the demo web app, creates a test
in the app, runs it with the browser shown, and reads a failure. Commands are
run from the repository's root unless said otherwise.

1. **Install** Git, the [.NET 10 SDK](https://dotnet.microsoft.com/download)
   and [Node 24](https://nodejs.org/) (which brings Corepack, and with it the
   pinned pnpm).
2. **Clone** the repository and go into it:

   ```sh
   git clone https://github.com/starscream000/coffee.git
   cd coffee
   ```

3. **Build the engine and publish the app** into `apps/desktop/dist/app`. The
   script prints the path of the app's executable at the end:

   ```sh
   apps/desktop/scripts/publish.sh            # Linux, macOS
   pwsh apps/desktop/scripts/publish.ps1      # Windows (or any system with PowerShell 7)
   ```

4. **Install the browser** the engine runs tests in (once per machine; the
   full browser, so that a run can show it):

   ```sh
   corepack pnpm --filter @cfe/engine exec playwright install chromium
   ```

5. **Start the demo web app** in a terminal of its own and leave it running.
   It serves the pages the demo project tests, on `http://127.0.0.1:4310`:

   ```sh
   node examples/demo-app/server/server.ts
   ```

6. **Start the app** in another terminal, with the secret the demo project
   declares set (any value of 4 characters or more; only its saved logins use
   it):

   ```sh
   DEMO_PASSWORD=demo-secret apps/desktop/dist/app/Desktop.App                       # Linux, macOS
   $env:DEMO_PASSWORD = 'demo-secret'; apps\desktop\dist\app\Desktop.App.exe     # Windows PowerShell
   ```

   The engine status at the top right turns to "Engine ready". If the run
   buttons stay off, the text below them says why (no browser: go back to
   step 4).

7. **Open the demo project**: Open folder…, then `examples/demo-app`. The
   tests appear on the left; the problems panel says "No problems". (Runs
   write their results into `examples/demo-app/.cfe/`, which Git ignores.)
8. **Create a test**: select the `tests` folder, click New test, name it
   `my-first`, and Create. It opens with one step, `goto: /`. In its step
   list:
   - select `goto` and, in its form below, set **url** to `/todos`;
   - Add step → `fill`; set **target** to `todos.new` (the picker offers the
     shared targets) and **value** to `Buy milk`;
   - Add step → `click`; set **target** to `todos.add`;
   - Add step → `expect.text`; set **target** to `todos.count` and **equals**
     to `1`;
   - Add step → `click`; set **target** to `todos.clearAll`; then Move to
     after, so the list is emptied even when the test fails.

   The text editor on the right shows each change as you make it. Save
   (Ctrl+S, Cmd+S on macOS).

9. **Run it with the browser shown**: tick Show browser on the left, then Run
   in the test's tab. A browser window opens and works through the steps; the
   run's tab shows each step turning green and the run ends "Passed".
10. **Read a failure**: in the `expect.text` step's form, set **equals** to
    `2`, save, and Run again. The run ends "Failed"; the failed step is
    selected and shows the error code, its message, the expected value `"2"`
    and the actual `"1"`. Click the step's file and line to go back to it;
    Revert the change (or set it to `1` again) and save.
11. **Look back**: the bottom panel's Run history lists both runs; click one
    to open it again.

## Layout

| Path                         | Contents                                                                           |
| ---------------------------- | ---------------------------------------------------------------------------------- |
| `src/Desktop.Protocol`       | The engine protocol in C#: message types, framing, version rule, `Product`         |
| `src/Desktop.Engine`         | Finds and starts the engine, handshake, typed requests and events                  |
| `src/Desktop.App`            | The Avalonia app: views, view models, services                                     |
| `tests/*`                    | xUnit tests, one project per source project                                        |
| `docs/`                      | [Architecture](docs/architecture.md), [plan](docs/plan.md), [decisions](docs/adr/) |
| `handoff/`                   | The desktop track's [handoff folder](handoff/README.md)                            |
| `scripts/verify.sh`, `.ps1`  | Every check a pull request must pass                                               |
| `scripts/publish.sh`, `.ps1` | Builds the engine and publishes the app into `dist/app`                            |
| `NuGet.Config`               | Restores from nuget.org only, whatever the machine's own settings                  |

## Getting started

Requirements: the .NET 10 SDK (see [global.json](global.json)), and for a
working engine, Node and pnpm as in the [root README](../../README.md).

```sh
# once, from the repository root: build the engine the app starts
corepack pnpm install
corepack pnpm build

# from apps/desktop
dotnet run --project src/Desktop.App   # start the app (or use scripts/publish.sh, see the walk-through)
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
