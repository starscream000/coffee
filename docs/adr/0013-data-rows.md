# 0013. Data rows repeat the whole test; no flow-level `forEach` yet

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

The brief allows "repeat for each data row" as one of only two reuse
mechanisms. It could apply to a whole test or to a flow call inside a test.

## Decision

- `data` is allowed on tests only (inline rows, or a CSV or YAML file). The
  whole test runs once per row.
- Each row is its own test instance: own `testId` (`<file>#<row>`), own fresh
  browser contexts, own `before` and `after`, reported separately.
- `${row.…}` is available in tests with `data`; elsewhere it is a validation
  error.
- **Possible later addition:** `forEach` on `call`, repeating a flow per row
  inside one test. Not part of v0.1.0.

To keep that addition open, the format avoids anything that would block it:
`call` always takes a mapping in its long form (so a `forEach` key can be added
compatibly), the name `forEach` is not used for anything else, and the `row`
namespace has one meaning ("the current data row") that would also fit inside a
repeated flow.

## Alternatives rejected

- **Both levels in v0.1.0**: more to build and test, and a loop inside a test
  makes failures harder to read (which iteration failed?) without a clear
  request for it.
- **Flow-level only**: one failing row would fail the whole test and hide the
  other rows' results.

## Consequences

Results are easy to read: one line per row. Tests that need several rows inside
one session (for example adding five products to one cart) must, for now, call
a flow several times explicitly.

## Revisit when

The owner asks for `forEach`, or real test suites show the same flow call
repeated more than five times in a row within one test.
