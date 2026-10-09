# 0020. Define the protocol once, as Zod schemas

- Status: Accepted (reviewer for the owner, 2026-10-09)
- Date: 2026-10-09

## Context

The protocol is a public contract with three representations: TypeScript types
for the engine and the CLI, JSON Schema files for the C# desktop app, and
validation of incoming request parameters in the engine. The v0.1.0 plan said
JSON Schemas are generated but not from what. If the three are written
separately they will drift apart.

## Decision

- Every protocol message is a Zod schema in `@cfe/protocol`: the JSON-RPC
  envelopes, every request's parameters and result, every event, and the
  shared types (`Location`, `Diagnostic`, `LocatorUse`, `SnapshotStatus`,
  `ErrorInfo`, …).
- The TypeScript types are inferred from the schemas (`z.infer`) and exported
  under the names `docs/protocol.md` uses.
- The JSON Schema files in `packages/protocol/schema/` are generated from the
  same schemas with Zod's `toJSONSchema` by a script, and committed. A unit
  test fails when the committed files differ from what the script generates.
- The engine validates incoming request parameters with the same schemas.
- Object schemas accept unknown fields, because the protocol says clients and
  the engine ignore fields they do not know.
- `zod` is the protocol package's one npm dependency. The dependency rule
  "`protocol` depends on nothing" means it depends on no other workspace
  package.

## Alternatives rejected

- **Hand-written TypeScript types and hand-written JSON Schemas**: two sources
  that drift, with nothing to catch it.
- **TypeScript types as the source, JSON Schemas generated from them**
  (`ts-json-schema-generator` and similar): needs a compiler-based generator,
  and the engine would still need a third, runtime validator.
- **JSON Schemas as the source, types generated with `json-schema-to-typescript`**:
  the engine would need Ajv to validate, a second schema language next to the
  Zod used for step files (ADR 0006).
- **No runtime dependency in the protocol package**: then the engine would
  re-declare every schema to validate parameters.

## Consequences

One source for types, schemas and validation. Zod becomes part of the protocol
package's public surface (clients that import it get Zod too), so a Zod major
upgrade is a protocol-package change and must keep the generated JSON Schemas
identical or bump `PROTOCOL_VERSION`.

## Revisit when

Zod's JSON Schema output cannot express something the protocol needs, or the
C# client needs schema features `toJSONSchema` does not produce.
