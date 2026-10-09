# 0012. Run tests with our own runner on the Playwright library

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

The brief says the engine uses the Playwright library directly, not the
Playwright CLI. Playwright Test (`@playwright/test`) is Playwright's own test
runner. We must decide whether step files run through it or through our own
runner.

## Decision

The engine has its own runner (`runner` module) that uses the `playwright`
library (`chromium.launch`, `browser.newContext`, `Page`, `Locator`). It
implements exactly what the step format needs:

- data rows as separate test instances; `before` → `steps` → `after`, with
  `after` always running;
- per-step page state, events in real time, locator reporting, `opens`;
- step timeouts and cancellation through `ctx.signal`;
- saved logins via `storageState`;
- sequential execution in v0.1.0.

## Alternatives rejected

- **Generate `.spec.ts` files and run Playwright Test**: needs the Playwright
  CLI (ruled out), loses the mapping from errors to YAML lines unless we
  rebuild it, and starts a fresh process tree per run, which suits CI but not a
  desktop app that runs one test again and again.
- **Use Playwright Test's internals programmatically**: not public API; breaks
  without notice.
- **Write steps as Playwright Test fixtures and reporters**: the reporter API
  gives results per `test.step`, but not control over when screenshots,
  snapshots, cancellation and `after` steps happen, and events would arrive
  through Playwright's reporter model instead of our protocol.

## Consequences

We own features Playwright Test would give us for free: timeouts, retries,
parallel workers, reporters. v0.1.0 needs only timeouts; retries and workers
are not requested. The future "export to Playwright code" command generates
Playwright Test files, but running never depends on it.

## Revisit when

- the owner asks for retries, sharding or parallel workers and implementing
  them in our runner would take more than a milestone, or
- parallel runs and JUnit-style report output are required before the server
  milestone (v0.4.0), or
- Playwright Test gains a public programmatic API that supports per-step hooks
  and cancellation.
