# 0007. Record page snapshots with Playwright tracing, one chunk per step

- Status: Proposed (revised after a time-boxed investigation, 2026-10-09)
- Date: 2026-10-09

## Context

The engine saves a page snapshot after each step, to be opened later on
request. The first proposal was MHTML, which works only in Chromium. The owner
asked for a short investigation of Playwright's own tracing first: it works
across browsers, includes network and console data, and its viewer runs in a
browser. Two questions: can each step be wrapped in a trace group named by its
step ID, and can a single step be opened directly?

## Investigation

Spike with Playwright 1.64.0 and Chromium on Windows: a five-step login page
(including a password field and a text field filled with a fake secret, and a
fetch to a JSON API), time-boxed to one hour. The spike code was throwaway and
is not in the repository.

| Question                          | Finding                                                                                                                                                                                                                                                                                                                                |
| --------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Trace group per step              | **Yes.** `context.tracing.group(stepId, { location })` / `groupEnd()` are public API. The group's title is the step ID and its location can be the **YAML file and line**, so the viewer shows step-file locations.                                                                                                                    |
| Open a single step directly       | **Not by URL**: the viewer reads only `?trace=<url>` and has no parameter to select an action. **But yes by chunk**: `tracing.startChunk()` / `stopChunk({ path })` around each step writes one zip per step that contains only that step.                                                                                             |
| Is a step's zip self-contained    | **Yes.** Each chunk repeats the context options and the resources it needs. Sizes in the spike were 3–9 KB per step. Larger pages repeat their CSS and images in every chunk.                                                                                                                                                          |
| Viewer without the Playwright CLI | **Yes, but not public API.** The viewer is a static web app inside `playwright-core/lib/vite/traceViewer` (not in the package's `exports`). Serving that folder from the engine and opening `/?trace=<step zip>` in a browser window worked offline.                                                                                   |
| Network and console data          | Included: network entries with URLs and bodies, console and action logs, before/after DOM snapshots, action parameters.                                                                                                                                                                                                                |
| Secrets                           | A secret appears in **action parameters** (`fill` value), **DOM snapshots** (input values, **including password fields**), **log lines**, **network URLs**, and in **screencast frames** (images). Rewriting the text files in the zip and dropping screencast frames produced a trace the viewer still opened, with `•••` everywhere. |
| Cross-browser                     | Tracing is supported by Playwright for all browsers; only Chromium was tested (v0.1.0 is Chromium only).                                                                                                                                                                                                                               |

## Decision

- Hide the format behind an internal interface in the `pagestate` module:

  ```ts
  interface PageStateRecorder {
    beginStep(step: StepRef): Promise<void>;
    endStep(step: StepRef): Promise<PageState>; // screenshot path + snapshot handle
    open(snapshot: SnapshotHandle): Promise<void>;
  }
  ```

  Clients see only `screenshotReady` (a PNG path) and `snapshotReady`, and open
  snapshots through `openSnapshot`. The format is not in the protocol.

- **Recommended implementation: Playwright tracing**, started per browser
  context with `snapshots: true`, `screenshots: false`; one chunk per step,
  wrapped in a group titled with the step ID at the step's YAML location. Each
  chunk is masked ([ADR 0014](0014-secret-masking.md)) before `snapshotReady`.
- `openSnapshot` serves the viewer's static files from the installed
  `playwright-core` on a local port and opens `/?trace=<step zip>` in a
  test-browser window.
- Screenshots are **not** taken from the trace; the engine takes its own after
  each step, so it can mask secrets with Playwright's `mask` option.
- Playwright is pinned to an exact version, and an integration test opens a
  snapshot through the viewer, so an upgrade that moves the viewer fails CI.

## Alternatives rejected

- **MHTML via the Chrome DevTools Protocol**: Chromium only, no network or
  console data, and opening it means loading a page from a file, which runs
  differently from the original. Still possible later behind the same
  interface.
- **One trace per test and a viewer deep link**: the viewer has no parameter to
  select a step, so "open this step" would land on the start of the test.
- **Spawning `playwright show-trace`**: that is the Playwright CLI, which the
  brief rules out; it also needs `playwright` (not just `playwright-core`) at
  run time.
- **The hosted viewer (trace.playwright.dev)**: needs internet access and sends
  the user to an outside site, even though processing is local.
- **Our own DOM serializer**: much more work and less faithful.

## Consequences

- Snapshots show before and after states, network and console, and work for any
  browser Playwright supports, which suits v0.2+ when more browsers come.
- We depend on an internal file layout of `playwright-core`; the exact pin and
  the integration test contain that risk.
- Disk use grows with page size because each step chunk repeats its resources.
- The snapshot is shown inside the trace viewer (in a browser window), not as a
  live page loaded in the test browser.

## Revisit when

- a Playwright upgrade moves or removes `lib/vite/traceViewer`, or the viewer
  stops accepting `?trace=`, or
- Playwright adds a public API for serving the trace viewer (adopt it), or
- a typical run folder exceeds 200 MB for 50 steps, or
- a secret is found unmasked in a trace despite ADR 0014.
