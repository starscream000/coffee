# 0011. Version the protocol separately, with a handshake at connect

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

The protocol is a public contract between independently released clients and
the engine. It will change a lot until the desktop app (v0.3.0) has exercised
it.

## Decision

- The protocol has its own semantic version (`PROTOCOL_VERSION`).
- While it is `0.x`, client and engine must match on major and minor. At the
  desktop app release it becomes `1.0.0`; from then on only a major bump may
  break, and clients must ignore unknown fields and events.
- **Handshake at connect**: the client's first request must be `initialize`
  with its protocol version. An incompatible client gets `IncompatibleProtocol`
  with both versions and which side to update, and the engine exits with code 3
  ([protocol.md](../protocol.md#handshake)).
- Every protocol change updates `docs/protocol.md`, the JSON Schemas and
  `CHANGELOG.md` in the same branch.

## Alternatives rejected

- **Protocol version = engine package version**: every engine release would
  look like a protocol change.
- **Strict semver from the start (1.0.0 now)**: either we freeze a protocol no
  real UI has used yet, or we bump the major version constantly.
- **Best-effort compatibility without a handshake**: failures would show up as
  confusing errors mid-run instead of a clear message at connect.

## Consequences

Freedom to fix the protocol early, strict guarantees later. Contract tests in CI
check that the JSON Schemas match the TypeScript types and that the handshake
refuses an incompatible version.

## Revisit when

The desktop app is released (move to `1.0.0` and supersede the `0.x` rule), or
two clients need to talk to engines of different minor versions before then.
