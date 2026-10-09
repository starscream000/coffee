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
the engine (no RPC library), unit tested for split and merged chunks, very long
lines and invalid JSON.

## Alternatives rejected

- `Content-Length` framing: more code in every client and harder to read
  when debugging; its advantage (binary-safe bodies) does not apply, because we
  never send binary data over the protocol (artifacts are files).
- `vscode-jsonrpc` in the engine: ties the engine to LSP framing and adds a
  dependency for very little code.
- gRPC, WebSockets or HTTP: the brief fixed stdio.

## Consequences

Trivial to implement in C# (`StreamReader.ReadLineAsync`; StreamJsonRpc also
supports this framing) and in any future client. Any stray `console.log` in the
engine (or in a user action) would corrupt the stream, so the engine redirects
`console.*` to stderr at start-up and a test checks it.

## Revisit when

A client needs to receive binary content over the protocol (for example the
server streaming artifacts to a remote client), or a message exceeds 16 MB.
