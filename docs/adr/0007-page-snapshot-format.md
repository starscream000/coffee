# 0007. Save page snapshots as MHTML

- Status: Proposed
- Date: 2026-10-09

## Context

The engine saves a page snapshot after each step, to be opened in the test
browser later. It must keep the DOM, styles and images of that moment without
running the page's scripts again.

## Decision

Capture MHTML with the Chrome DevTools Protocol (`Page.captureSnapshot`) and
open it in a Chromium window on `openSnapshot`. Current form values are written
into the DOM before capture, and secret values are replaced before the file is
saved.

## Consequences

Faithful, self-contained single files that Chromium opens natively. Works only
with Chromium-based browsers (open question 4). Alternatives considered: our own
DOM serializer (cross-browser, but much more work and less faithful) and
Playwright trace files (internal format, opened in the trace viewer rather than
the test browser). Snapshots can be large, so a retention setting for
`.testtool/runs` will be needed.
