# 0005. Frame JSON-RPC as newline-delimited JSON on stdio

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

The brief fixes JSON-RPC over stdin/stdout. Two framings are common:
`Content-Length` headers (as in the Language Server Protocol) and one JSON
message per line. The review asked for a rule on file contents and a maximum
message size.

## Decision

- One JSON-RPC 2.0 message per line, UTF-8, `\n` separated. stdout carries only
  protocol messages; logs go to stderr. A small hand-written reader and writer
  in the engine (no RPC library), unit tested for split and merged chunks, very
  long lines and invalid JSON.
- **Protocol messages never carry file contents.** Screenshots, snapshots, run
  results and other artifacts are referenced by absolute path; clients read the
  files themselves. The one input that carries text is `validate` with
  `content`, which holds an unsaved step file, not an artifact.
- **Maximum message size: 4 MiB** (4,194,304 bytes of UTF-8, not counting the
  newline), in both directions. The constant `MAX_MESSAGE_BYTES` lives in the
  protocol package.
- **When the engine would exceed it** (for example a huge `actual` value in an
  assertion or a very long log line):
  1. it truncates the longest string fields of that message (`message`,
     `expected`, `actual`, log text) to 64 KiB each, marked with
     `… [truncated N characters]`;
  2. if the message is still too large, it does not send it. For a response it
     sends the error `MessageTooLarge` (`-32009`) instead; for an event it sends
     a `log` event with level `error` and code `MessageTooLarge` naming the
     event type, test and step. The run continues.
- **When a client sends a line over the limit**, the engine reads and discards
  the rest of that line without parsing it, answers with `MessageTooLarge`
  (`-32009`, `id: null` because the id could not be read), and keeps the
  session open.
- **When a client receives a line over the limit** (which a correct engine
  never sends), it must discard the line, report an engine error to its user,
  and may end the session. It must not crash or try to parse the line.

## Alternatives rejected

- `Content-Length` framing: more code in every client and harder to read when
  debugging; its advantage (binary-safe bodies) does not apply, because we
  never send binary data or file contents over the protocol.
- `vscode-jsonrpc` in the engine: ties the engine to LSP framing and adds a
  dependency for very little code.
- gRPC, WebSockets or HTTP: the brief fixed stdio.
- No size limit: a single runaway value could make a client allocate without
  bound or stall the stream.
- Sending screenshots inline (base64): bloats every event by hundreds of
  kilobytes and duplicates files that already exist on disk.

## Consequences

Trivial to implement in C# (`StreamReader.ReadLineAsync`; StreamJsonRpc also
supports this framing) and in any future client. Any stray `console.log` in the
engine (or in a user action) would corrupt the stream, so the engine redirects
`console.*` to stderr at start-up and a test checks it. Clients and engine must
share a file system, which holds for the CLI and the desktop app.

## Revisit when

A client needs artifacts from an engine on another machine (for example the
server streaming results to a remote client), or real step files make
`validate` messages approach 4 MiB.
