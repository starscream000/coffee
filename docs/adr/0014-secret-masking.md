# 0014. Mask secrets at every exit point, by value

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

The brief requires secrets to be masked everywhere. The tracing investigation
([ADR 0007](0007-page-snapshot-format.md)) showed where a secret ends up when a
test uses it: action parameters, log lines, DOM snapshots (input values,
including password fields), network URLs and bodies, and screenshots or
screencast frames.

## Decision

**Sources.** Secrets are declared by name in the config. Values come from the
process environment variable of the same name or a git-ignored `.env` file. The
raw process environment is not otherwise visible to step files or actions.
Values shorter than 4 characters are rejected, with a message naming the
secret.

**Registry.** At run start the `secrets` module loads every declared secret into
a registry, together with the encoded forms the value can take in output: as
is, JSON-escaped, URL-encoded (`encodeURIComponent` and form encoding), and
HTML-escaped. Values reach actions only through `ctx.secrets.get` or
`${secrets.…}`; values derived from a secret (for example a variable set to
`Bearer ${secrets.TOKEN}`) are masked because they contain the secret.

**Masking text.** One function, `mask(text)`, replaces every registered form
with `•••`. It is applied at the exit points, not at each call site:

| Exit point       | How                                                                                                                          |
| ---------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Protocol         | The JSON-RPC writer masks each message field by field (see below), before truncation, then writes it to stdout               |
| Logs             | `ctx.log` goes through the writer; the console replacement and the engine's own error logging pass every line to `mask`      |
| Run folder files | `run.json` and `events.ndjson` hold protocol messages and are masked like them; other text files go through a masking writer |
| Variable values  | Events carry step params uninterpolated (`${secrets.X}` stays as written), so values are not even sent                       |

**Masking protocol messages.** Masking must never damage a message: after
masking, a message is still valid JSON, still validates against its protocol
schema, and keeps every identifier. So `mask` is never applied to the
serialised text or to object keys; it is applied to string values, field by
field, following one table next to the protocol schemas
(`packages/protocol/src/masking.ts`):

- **Kept as they are:** the envelope (`jsonrpc`, `id`, `method`), error codes
  and error names, and the fields the protocol defines as identifiers or fixed
  values: `runId`, `testId`, `stepId`, `parentStepId`, `seq`, `section`,
  `action`, `page`, `status`, `level`, `severity`, `code`, `state`, every file
  and folder path, environment names, login names, numbers and booleans. Also
  an action's `paramsSchema` in `listActions`: it comes from action code in the
  repository, like an identifier, and a masked word (a secret `string` or
  `password`) would break the schema clients build their forms from.
- **Masked:** every string inside a field that carries free text or user data:
  `message`, `hint`, `expected`, `actual`, a step's `params`, `candidate`,
  `data`, `title`, `reason` where it is free text, log text, and any field the
  table does not name, so a new field is masked until someone decides
  otherwise. A test fails when a protocol schema has a field the table does not
  name.
- **Walked:** containers (`result`, `error`, an event's `params`,
  `diagnostics`, `locators`, …), whose fields are looked up in the table in
  turn. A `log` event's `data` is walked too: the identifiers the table knows
  (the `target` and `candidateIndex` of a `LocatorFallback` warning) are kept,
  and every field it does not know is masked.

The reason identifiers can be kept: they come from files in the repository
(step files, action files, the config) and from the engine itself, and the
repository never holds secret values; those live in the environment or in the
git-ignored `.env` file. Masking happens before long strings are truncated, so
truncation cannot cut a secret in half and leave a recognisable piece.

Stderr is an exit point too. The console replacement that keeps stdout for the
protocol ([ADR 0005](0005-json-rpc-over-stdio.md)) writes through `mask`, and so
does the engine's own logging of internal errors.

**Masking traces (snapshots).** Tracing runs with `screenshots: false` (no
screencast images, which cannot be masked). When a step's chunk is written, the
engine rewrites every text entry in the zip (`trace.trace`, `trace.network`,
text resources) through `mask`, removes binary resources whose text form
contains a secret, and only then sends `snapshotReady`. If rewriting fails, the
chunk is deleted and the step reports `snapshot: "failed"` rather than keeping
an unmasked file ([ADR 0007](0007-page-snapshot-format.md)).

**Sensitive headers, not only declared secrets.** Declared secrets are not the
only sensitive data a trace holds. By default, the values of the request and
response headers `Cookie`, `Set-Cookie` and `Authorization` (matched without
regard to case) are replaced with `•••` in every trace chunk, whether or not
they contain a declared secret. The header names stay visible so the trace
still shows that a session or token was sent.

**Header values in variables.** Variables keep the real value of a header, so a
test can read a token from a response and send it in a later request. When
`api`, `wait.response` or `expect.response` stores or reads a `Cookie`,
`Set-Cookie` or `Authorization` header, the engine registers its value (for
`Cookie` and `Set-Cookie`, the value of each cookie) as a secret for the rest
of the run, with the same encoded forms as a declared secret. From then on it is
masked wherever the engine writes it out: protocol messages, logs, error
messages, the run folder and traces. Values shorter than 4 characters are not
registered, as with declared secrets, and the engine logs a `warn` naming the
header (never the value).

**Saved logins.** Saved login state (cookies and storage) is written only to
the git-ignored data folder (`.cfe/logins/`, [ADR 0018](0018-saved-logins.md)),
never into step files, results or events; the protocol has no message that
carries it. Its file names are hashes and its metadata holds no parameter
values.

**Masking screenshots.** The engine takes each step's screenshot itself with
Playwright's `mask` option, covering:

- `input` and `textarea` elements whose current value contains a secret (found
  with one `page.evaluate` per screenshot), and
- elements whose visible text contains a secret.

Password fields are already shown as dots by the browser.

**Proof.** An acceptance check (A9 in the definition of done) runs a test that
logs in, fills, sends and displays a secret, then searches every byte of the
protocol stream, every file in the run folder (zips unpacked) and every file in
`.cfe/logins/` for each registered form of the secret, and searches the
run folder for the session cookie's value; it fails on any match.

## Alternatives rejected

- **Masking at each call site** (each action masks its own output): one
  forgotten call leaks a secret.
- **Never recording values typed into fields**: masks too much and still leaks
  through network data and the DOM.
- **Blurring whole screenshots**: makes them useless.
- **Turning tracing off for tests that use secrets**: most real tests log in,
  so snapshots would be missing where they are most useful.

## Consequences

Known limits, documented for users: a secret drawn into a canvas or image, or
shown only in part (such as the last four digits), cannot be detected; and a
secret the application transforms (for example hashes or base64-encodes it)
appears in its transformed form. Masking costs one string scan per masked
field and per artifact.

What field-by-field masking gives up: a secret that equals an identifier, or
part of a file path, is not masked in that identifier or path (for example a
secret `checkout` in `tests/checkout/guest.test.yaml`). It is still masked in
every message, log line and error text. The alternative, masking the
serialised text, broke messages whenever a secret equalled a key or a piece of
JSON syntax such as `jsonrpc` or `2.0","` (review 0004, finding 1).

## Revisit when

A secret is found unmasked in any output, or masking adds more than 10% to the
run time of the demo-app suite.
