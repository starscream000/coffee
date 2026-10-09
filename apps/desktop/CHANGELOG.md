# Changelog (desktop app)

All notable changes to the desktop app are documented in this file. The
repository's [changelog](../../CHANGELOG.md) covers everything else.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- The workspace shell: a start page with recent projects and the engine's
  state; opening a project folder through the engine; the test explorer,
  tabs for step files and the action catalogue, the problems panel and the
  engine log; re-validation when YAML files change and reopening when the
  config changes; restarting the engine (and reopening the project) after a
  crash; a settings panel for the Node and engine paths. Headless UI tests
  render every screen, and one test opens `examples/demo-app` through the
  real engine.
- App services and view models, tested without a window: the engine service
  (start, stop, log, requests) with its status and a bounded engine log; the
  test explorer (folder tree, search, tags, problem counts, and a fallback
  for engines that cannot list tests yet); the problems panel; the read-only
  step file view with problem lines marked; the action catalogue with the
  parameters read from each action's schema; the settings panel and the
  settings store; reading and watching project files.
- The engine protocol in C# (`Desktop.Protocol`): every request, result,
  event and shared type of protocol 0.1.0, the version rule, the error table,
  newline framing with the 4 MiB limit, and tolerance for unknown fields,
  events and enum values. Contract tests compare every type with the JSON
  Schema files and read and write every protocol example (ADR D0003).
- The engine host (`Desktop.Engine`): finds Node and the engine (ADR D0004),
  starts it, runs the handshake, sends typed requests, raises events, reports
  unusable output without failing, and fails waiting requests with the exit
  code and the end of stderr when the engine stops. Tested against an engine
  in memory and against the real engine of the checkout.
- The desktop solution: `Desktop.Protocol`, `Desktop.Engine` and the Avalonia
  app `Desktop.App`, each with an xUnit test project; .NET 10, Avalonia 12,
  exact package versions with lock files, strict build settings and code style;
  `scripts/verify.sh` and `scripts/verify.ps1`.
- `Product` in C#, checked against `packages/protocol/src/product.ts`.
- The desktop documents: architecture, plan, decision records D0001 to D0005,
  standing instructions, and the desktop handoff folder with requests R0001 to
  R0003.
