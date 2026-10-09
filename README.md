# Coffee

> Placeholder name, defined once in
> [`packages/protocol/src/product.ts`](packages/protocol/src/product.ts). Status: **pre-release (Milestone 0)**. Nothing runs tests yet.

Coffee records, edits and runs end-to-end web tests. Tests are readable YAML
**step files** stored in your own Git repository; a headless engine built on
[Playwright](https://playwright.dev) runs them and streams results to a client
(a command-line runner first, a desktop app later).

```yaml
# tests/checkout/guest-checkout.test.yaml (proposed format, see docs/step-format.md)
name: Guest can check out
steps:
  - goto: /products
  - click: product.addToCart
  - expect.text: { target: cart.count, equals: '1' }
```

## Repository layout

| Path                | Contents                                                         |
| ------------------- | ---------------------------------------------------------------- |
| `packages/protocol` | Message and event types shared by the engine and clients         |
| `packages/engine`   | Step file parsing, validation, actions, runner                   |
| `packages/cli`      | Command-line client (`cfe`, package `@cfe/cli`)                  |
| `apps/desktop`      | Avalonia desktop app (in progress, see `apps/desktop/README.md`) |
| `examples/demo-app` | Small local web app used by integration tests (Milestone 1)      |
| `docs/`             | Architecture, step format, actions, protocol, decision records   |

## Getting started

Requirements: Node.js (version in [`.nvmrc`](.nvmrc)) and Corepack (ships with
Node 24).

```sh
corepack enable        # or prefix pnpm commands with "corepack"
pnpm install
pnpm verify            # lint, format check, type check, tests
```

## Documentation

The design documents below are **proposals** awaiting approval.

- [Architecture](docs/architecture.md) (includes the open questions)
- [Step file format](docs/step-format.md)
- [Actions](docs/actions.md)
- [Engine protocol](docs/protocol.md)
- [Architecture decision records](docs/adr/)
- [v0.1.0 definition of done](docs/milestones/v0.1.0-definition-of-done.md) and [plan](docs/milestones/v0.1.0-plan.md)
- [Contributing](CONTRIBUTING.md) and [Changelog](CHANGELOG.md)

## Copyright

Copyright © 2026 starscream000. All rights reserved. This is not open-source software; no
licence is granted.
