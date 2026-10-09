# Instruction D0001: desktop foundation

- Date: 2026-10-09
- Written by: **desktop implementer, transcribing the owner's brief from chat**
  (see [Bootstrapping](../README.md#bootstrapping)). The reviewer may replace
  this file with a corrected instruction under a new number.
- Based on `main` at: `901a988`
- Replaces: none
- Follows review: none

## Goal

Start the Avalonia desktop app: a test-suite management client for the engine.
At the end of this instruction the app opens a project through the real engine
and shows its tests, problems and actions, and the desktop track has its own
documents, plan and handoff folder.

## Owner decisions

The owner's brief, 2026-10-09, in the owner's words where it matters:

1. "Start working on the Avalonia based desktop application." It should be "a
   nice e2e test-suite-management solution" for the engine, which records and
   runs Playwright-based tests through YAML step files and will start its own
   sandboxed Chromium (headed on a tester's machine, headless on the server).
2. "Follow proper standards of coding, follow git backed development,
   committing and branching aptly", in the way the engine implementer works.
3. Work only inside `apps/desktop/`: "never come out of that". Read and refer
   to everything else, but edit nothing outside.
4. Follow the same handoff method, inside `apps/desktop/`, with "proper docs
   and reporting for inter-agent operability". Anything needed from outside is
   asked for through the handoff channel.

## Branches

Stacked; each based on the one above.

| Branch                         | Based on                       | What it holds |
| ------------------------------ | ------------------------------ | ------------- |
| `desktop/chore/scaffold`       | `main`                         | Tasks 1 to 4  |
| `desktop/feat/protocol-client` | `desktop/chore/scaffold`       | Tasks 5 to 8  |
| `desktop/feat/workspace-shell` | `desktop/feat/protocol-client` | Tasks 9 to 13 |

## Tasks

### Part A: scaffold (`desktop/chore/scaffold`)

1. A .NET solution under `apps/desktop/`: projects for the protocol, the engine
   host and the app, a test project for each, shared build settings, exact
   package versions, lock files, code style enforced by the build.
2. The product's names in C#, checked against `packages/protocol/src/product.ts`.
3. The desktop documents: README, architecture, milestone plan, decision
   records, changelog, standing instructions for agents.
4. This handoff folder, with the requests the track already needs.

### Part B: protocol client (`desktop/feat/protocol-client`)

5. C# types for every message of `docs/protocol.md`, tested against the
   committed JSON Schemas and the protocol examples.
6. Newline-delimited JSON-RPC framing with the 4 MiB limit, and the version
   rule.
7. The engine host: find Node and the engine, start the process, the handshake,
   typed requests, events, errors, stderr, shutdown and crash handling.
8. Tests against a fake engine in memory, and against the real engine when it
   is built.

### Part C: workspace shell (`desktop/feat/workspace-shell`)

9. Main window: start page with recent projects, open a project folder.
10. Test explorer: the project's tests as a tree, with search and tags.
11. Problems: the diagnostics of `openProject` and `validate`, with file and
    line; re-validate on demand and when a file changes on disk.
12. Step file view (read-only, with line numbers and problems marked) and the
    action catalogue from `listActions`.
13. Engine status and engine log (stderr); settings for the Node and engine
    paths.

## Done when

- [ ] The desktop checks (`scripts/verify.sh`) pass on every branch.
- [ ] The root `pnpm verify` passes on every branch.
- [ ] No file outside `apps/desktop/` changes.
- [ ] `reports/D0001-desktop-foundation.md` exists on the last branch.

## Out of scope

Running tests (the engine's runner does not exist yet), the recorder, editing
step files, packaging and installers, CI changes (asked for as a request).

## Report back

What the app can do now, what it cannot do yet and why, and every place where
the engine's documents were unclear for a client.
