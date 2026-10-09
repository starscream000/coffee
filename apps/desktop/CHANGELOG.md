# Changelog (desktop app)

All notable changes to the desktop app are documented in this file. The
repository's [changelog](../../CHANGELOG.md) covers everything else.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- The desktop solution: `Desktop.Protocol`, `Desktop.Engine` and the Avalonia
  app `Desktop.App`, each with an xUnit test project; .NET 10, Avalonia 12,
  exact package versions with lock files, strict build settings and code style;
  `scripts/verify.sh` and `scripts/verify.ps1`.
- `Product` in C#, checked against `packages/protocol/src/product.ts`.
- The desktop documents: architecture, plan, decision records D0001 to D0005,
  standing instructions, and the desktop handoff folder with requests R0001 to
  R0003.
