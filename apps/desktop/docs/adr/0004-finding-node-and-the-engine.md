# D0004. Find Node and the engine in a fixed order

- Status: Proposed
- Date: 2026-10-09

## Context

Repository ADR 0009 says clients start the engine as
`node <engine>/dist/main.js --stdio`, the CLI finds the engine through its
package dependency, `CFE_ENGINE` (the product's environment prefix plus
`ENGINE`) overrides it, and the desktop app ships a Node runtime and the
engine build. Until installers exist (plan milestone D6), the desktop app runs
from a development checkout.

## Decision

The engine's entry script is the first that exists of:

1. the path in the app's settings;
2. the product's `ENGINE` environment variable;
3. `engine/dist/main.js` next to the app's executable (the bundled engine of
   milestone D6);
4. `packages/engine/dist/main.js` in the nearest folder above the app's
   executable that holds `pnpm-workspace.yaml` (a development checkout).

Node is the path in the settings, else a Node bundled next to the app
(milestone D6), else `node` on the `PATH`.

If nothing is found, the app says which places it looked in and how to fix
it (build the engine with `pnpm build`, or set the path in Settings).

## Alternatives rejected

- **Ask the user for the path on first start**: needless for developers and,
  after D6, for everyone.
- **Read the CLI's installed package**: the desktop app does not depend on
  npm packages.

## Consequences

A developer runs `pnpm build` once and starts the app from the checkout with
no setup. The search order is shown in the engine log, so a wrong engine is
easy to spot.

## Revisit when

Installers ship (D6), or the engine is distributed on its own.
