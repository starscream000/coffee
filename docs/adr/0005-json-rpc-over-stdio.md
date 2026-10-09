# 0005. Frame JSON-RPC as newline-delimited JSON on stdio

- Status: Proposed
- Date: 2026-10-09

## Context

The brief fixes JSON-RPC over stdin/stdout. Two framings are common:
`Content-Length` headers (as in the Language Server Protocol) and one JSON
message per line.

## Decision

One JSON-RPC 2.0 message per line, UTF-8, `\n` separated. stdout carries only
protocol messages; logs go to stderr. A small hand-written reader and writer in
the engine (no RPC library), unit tested for split and merged chunks.

## Consequences

Trivial to implement in C# (`StreamReader.ReadLineAsync`; StreamJsonRpc also
supports this framing) and in any future client, and easy to debug by reading
the stream. Any stray `console.log` in the engine would corrupt the stream, so
the engine redirects `console.*` to stderr at start-up and a test checks it.
