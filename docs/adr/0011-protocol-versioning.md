# 0011. Version the protocol separately, 0.x until the desktop app

- Status: Proposed
- Date: 2026-10-09

## Context

The protocol is a public contract between independently released clients and
the engine. It will change a lot until the desktop app (v0.3.0) has exercised
it.

## Decision

The protocol has its own semantic version (`PROTOCOL_VERSION`). While it is
`0.x`, client and engine must match on major and minor. At the desktop app
release it becomes `1.0.0`; from then on only a major bump may break, and
clients must ignore unknown fields and events. Every protocol change updates
`docs/protocol.md`, the JSON Schemas and `CHANGELOG.md` in the same branch.

## Consequences

Freedom to fix the protocol early, strict guarantees later. Contract tests in CI
check that the JSON Schemas match the TypeScript types.
