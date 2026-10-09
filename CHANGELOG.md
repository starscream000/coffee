# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- pnpm workspace monorepo with `protocol`, `engine` and `cli` packages.
- TypeScript (strict), ESLint, Prettier and Vitest configuration.
- CI workflow running lint, format check, type check and tests on Linux,
  Windows and macOS.
- `CLAUDE.md`, `README.md` and `CONTRIBUTING.md`.
- Placeholder product name "Coffee" (command `coffee`), defined once in
  `packages/protocol/src/product.ts`.
- Design proposals: architecture, step file format, actions and engine
  protocol, with architecture decision records in `docs/adr/`.
