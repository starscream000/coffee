# Protocol examples

Every example message printed in `docs/protocol.md`, as plain JSON, used by
`src/examples.test.ts`. The file name is the key of the schema the example must
validate against (see `protocolSchemas()`), optionally followed by `--variant`.
Error responses are also checked against their `error-data.<name>` schema, and
notifications against the `event.<method>` schema.

`docs/protocol.md` shows the examples as JSONC with comments and `…`
placeholders; here comments are removed and placeholders filled with
realistic values. When an example in `docs/protocol.md` changes, change its
file here.
