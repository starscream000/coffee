# Test Tool

> Working name. Status: **pre-release (Milestone 0)**. Nothing runs tests yet.

Test Tool records, edits and runs end-to-end web tests. Tests are readable YAML
**step files** stored in your own Git repository; a headless engine built on
[Playwright](https://playwright.dev) runs them and streams results to a client
(a command-line runner first, a desktop app later).

```yaml
# tests/checkout/guest-checkout.test.yaml (proposed format)
name: Guest can check out
steps:
  - goto: /products
  - click: product.addToCart
  - expect.text: { target: cart.count, equals: '1' }
```

## Repository layout

| Path                | Contents                                                       |
| ------------------- | -------------------------------------------------------------- |
| `packages/protocol` | Message and event types shared by the engine and clients       |
| `packages/engine`   | Step file parsing, validation, actions, runner                 |
| `packages/cli`      | Command-line client (`testtool`)                               |
| `apps/desktop`      | Avalonia desktop app (later milestone)                         |
| `examples/demo-app` | Small local web app used by integration tests (Milestone 1)    |
| `docs/`             | Architecture, step format, actions, protocol, decision records |

## Getting started

Requirements: Node.js (version in [`.nvmrc`](.nvmrc)) and Corepack (ships with
Node 24).

```sh
corepack enable        # or prefix pnpm commands with "corepack"
pnpm install
pnpm verify            # lint, format check, type check, tests
```

## Documentation

- [Contributing](CONTRIBUTING.md) and [Changelog](CHANGELOG.md)

## Copyright

Copyright © 2026. All rights reserved. This is not open-source software; no
licence is granted.
