# D0001. Build on .NET 10 LTS and Avalonia 12

- Status: Accepted (owner, 2026-10-10)
- Date: 2026-10-09

## Context

The owner chose C# and Avalonia for the desktop app. The .NET and Avalonia
versions are open. The app must run on Windows, macOS and Linux and will ship
as one installer per system (plan milestone D6).

## Decision

- **.NET 10**, the current long-term-support release (supported until
  November 2028). `global.json` pins SDK `10.0.100` with `rollForward:
latestFeature`, so any 10.0 SDK feature band builds it.
- **Avalonia 12.1.3** with the Fluent theme and the Inter font, the current
  stable release.
- Every NuGet package is pinned to an exact version in
  `Directory.Packages.props` (central package management), and every project
  commits its `packages.lock.json`; CI restores in locked mode.
- Build strictness, the C# counterpart of the repository's strict TypeScript:
  nullable reference types on, warnings are errors, the .NET analyzers at
  `latest-recommended`, code style enforced in the build and by
  `dotnet format --verify-no-changes`, XML documentation required on every
  public type and member.

## Alternatives rejected

- **.NET 8 LTS**: supported only until November 2026, a month after this
  decision.
- **.NET 9 (standard-term support)**: shorter support than 10.
- **Avalonia 11**: still maintained, but 12 is the current line; starting on
  the older line means a migration before v0.3.0.

## Consequences

Contributors need the .NET 10 SDK besides Node 24. Lock files are JSON, so
Prettier checks them through the root `pnpm verify`: after a package change,
run `pnpm exec prettier --write apps/desktop` once (NuGet keeps the formatted
file as long as its content does not change).

## Revisit when

.NET 12 (the next LTS) ships, or an Avalonia release we need requires a newer
.NET.
