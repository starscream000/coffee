# Changelog (desktop app)

All notable changes to the desktop app are documented in this file. The
repository's [changelog](../../CHANGELOG.md) covers everything else.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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
