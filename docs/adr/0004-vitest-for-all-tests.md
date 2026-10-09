# 0004. Use Vitest for unit and integration tests

- Status: Accepted
- Date: 2026-10-09

## Context

The brief requires Vitest for unit tests and integration tests that run real
step files in a real browser.

## Decision

Use Vitest for both. Integration tests drive the engine (in-process for engine
tests, as a child process for protocol and CLI tests) against
`examples/demo-app`, in a separate Vitest project with longer timeouts.

## Alternatives rejected

- Playwright Test for the browser tests: a second runner and config, and our
  integration tests check the engine's output (events, run folder), not a page.

## Consequences

One runner, one config, one report. Unit tests stay fast because browser tests
are a separate project (`pnpm test:integration`).

## Revisit when

The integration suite takes longer than 10 minutes on any CI runner, or
Vitest's process isolation causes flaky browser tests.
