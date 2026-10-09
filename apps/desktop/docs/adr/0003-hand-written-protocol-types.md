# D0003. Write the protocol types by hand, checked by the schemas

- Status: Proposed
- Date: 2026-10-09

## Context

`docs/protocol.md` says the generated JSON Schema files are what "the C#
client uses for code generation and contract tests". The protocol has 44
schema files today. The C# types must stay equal to them.

## Decision

- The C# message types are written by hand as immutable records in
  `Desktop.Protocol`, with XML documentation taken from `docs/protocol.md`.
- A registry maps every schema key (`request.openProject.result`,
  `event.stepFailed`, …) to its C# type.
- Contract tests, run with the other unit tests, read
  `packages/protocol/schema/` and the protocol examples and fail when:
  - a schema file has no C# type in the registry, or the registry names a
    schema that does not exist;
  - a property of a schema object is missing from its C# type, or the C# type
    has a property the schema does not;
  - a required property is nullable in C#, or an optional one is not;
  - a protocol example does not read into its C# type;
  - what the C# type writes back does not validate against the schema;
  - the protocol version in the schema titles differs from the C# one.

## Alternatives rejected

- **Generating C# from the schemas** (NJsonSchema or quicktype): the
  schemas come from Zod, with `anyOf` for nullable values, `oneOf` unions
  (`SnapshotStatus`, action `source`) and open objects. Generators turn those
  into weak types (`object`, many optional fields) and odd names, and add a
  code-generation step to the build. The contract tests catch every drift that
  generation would prevent, while the types stay readable and documented.
- **No C# types, only `JsonElement`**: no compile-time checking in the app.

## Consequences

A protocol change fails the desktop tests in the same pull request that makes
it, once CI runs them (request R0001); the desktop implementer then updates
the C# types. The sentence in `docs/protocol.md` about code generation should
say "contract tests" only; the desktop implementer cannot change it and will
ask through a request if this record is accepted.

## Revisit when

The protocol grows past about 100 schema files, or the contract tests miss a
drift that generation would have caught.
