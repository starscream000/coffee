# 0004. Use Vitest for unit and integration tests

- Status: Accepted
- Date: 2026-10-09

## Context

The brief requires Vitest for unit tests and integration tests that run real
step files in a real browser. Playwright Test would be an alternative runner for
the browser tests.

## Decision

Use Vitest for both. Integration tests drive the engine (in-process for engine
tests, as a child process for protocol tests) against `examples/demo-app`. The
engine already uses the Playwright library directly, so Playwright Test adds
nothing.

## Consequences

One runner, one config, one report. Browser tests need longer timeouts and run
in a separate Vitest project so unit tests stay fast.
