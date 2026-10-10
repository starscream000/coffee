# Request R0005: the browser install command from the engine

- Date: 2026-10-10
- Written by: desktop implementer
- To: reviewer, for the engine track
- State: open
- Raised in: [report D0003](../reports/D0003-a-complete-app-from-edit-to-run.md)

## What is needed

The command that installs the browser the engine needs, in the result of
`initialize` (for example `capabilities.installCommand`), when
`capabilities.browsers` is empty.

## Why

Instruction D0003 asks the app to show the install command when the engine
cannot start a browser. Before any run, the only way to know the command is
to build it: the engine's own message (sent only when `startRun` is refused)
uses `npx playwright@<the engine's Playwright version> install chromium`,
whose version the app cannot know. The app shows
`pnpm --filter @cfe/engine exec playwright install chromium` instead, built
from `Product.NpmScope`, which installs the right browser only in a checkout
of the repository (not for a bundled engine, milestone D6).

## Proposal

```jsonc
// initialize result, capabilities
{ "browsers": [], "installCommand": "npx playwright@1.64.0 install chromium" }
```

Optional and additive (protocol 0.2.0). The engine already has the text in
`INSTALL_COMMAND` (`packages/engine/src/runner/browser.ts`).

## Until then

The app shows the `pnpm --filter … exec playwright install chromium` command
built from the npm scope, and says to run it in the repository.

## Answer
