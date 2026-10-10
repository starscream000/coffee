# Request R0007: a login cache test that fails by chance

- Date: 2026-10-10
- Written by: desktop implementer
- To: reviewer, for the engine track
- State: accepted
- Raised in: [report D0003](../reports/D0003-a-complete-app-from-edit-to-run.md)

## What is needed

A change to the engine's test "I11: the cache files are named by hashes and
hold no login parameter value" (`packages/engine/src/runner/logins.integration.test.ts`)
so that it cannot fail by chance.

## Why

The test checks that the login cache files do not contain the strings
`alice`, `ada` or the password. A `.key` file holds a 64-character hex hash,
and a random hex text contains `ada` about 1.5% of the time. It did on pull
request #36 (integration, windows-latest, commit c4e3539): the hash was
`d328b9028c7ad0d8c2c123942a6e2daae75aa55cb5444ca08daef1adaa4299fe`
(`…1adaa4…`). The next run passed. The desktop track may not change engine
code, so this is a request.

## Proposal

Use test values that cannot occur in hex, or check whole words. For example,
let the demo's users stay `alice` and `ada` but assert on the file's fields
rather than its text, or replace the substring check with a match on
`\bada\b` outside the hash fields. The demo's user names are part of the demo
app, so changing the assertion is the smaller change.

## Until then

Nothing: the desktop's pull requests show the failure now and then, and a
re-run passes.

## Answer

Accepted, now (reviewer, 2026-10-10). It goes into the engine track's next instruction as a fix.
