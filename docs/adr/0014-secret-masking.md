# 0014. Mask secrets at every exit point, by value

- Status: Proposed
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

| Exit point       | How                                                                                                    |
| ---------------- | ------------------------------------------------------------------------------------------------------ |
| Protocol         | The JSON-RPC writer masks every serialised message before writing it to stdout                         |
| Logs             | `ctx.log`, engine stderr logging and error messages go through the same writer or `mask`               |
| Run folder files | `run.json`, `events.ndjson` and every text file are written through a masking file writer              |
| Variable values  | Events carry step params uninterpolated (`${secrets.X}` stays as written), so values are not even sent |

**Masking traces (snapshots).** Tracing runs with `screenshots: false` (no
screencast images, which cannot be masked). When a step's chunk is written, the
engine rewrites every text entry in the zip (`trace.trace`, `trace.network`,
text resources) through `mask`, removes binary resources whose text form
contains a secret, and only then sends `snapshotReady`. If rewriting fails, the
chunk is deleted and the step reports "snapshot unavailable" rather than keeping
an unmasked file.

**Masking screenshots.** The engine takes each step's screenshot itself with
Playwright's `mask` option, covering:

- `input` and `textarea` elements whose current value contains a secret (found
  with one `page.evaluate` per screenshot), and
- elements whose visible text contains a secret.

Password fields are already shown as dots by the browser.

**Proof.** An integration test runs a test that fills, sends and displays a
secret, then searches every byte of the protocol stream and every file in the
run folder (unzipped) for each registered form of the secret, and fails on any
match.

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
appears in its transformed form. Masking costs one string scan per message and
per artifact.

## Revisit when

A secret is found unmasked in any output, or masking adds more than 10% to the
run time of the demo-app suite.
