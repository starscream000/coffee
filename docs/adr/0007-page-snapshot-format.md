# 0007. Record page snapshots with Playwright tracing, one chunk per step

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

The engine saves a page snapshot after each step, to be opened later on
request. The first proposal was MHTML, which works only in Chromium. The owner
asked for a short investigation of Playwright's own tracing first: it works
across browsers, includes network and console data, and its viewer runs in a
browser. Two questions: can each step be wrapped in a trace group named by its
step ID, and can a single step be opened directly? After the investigation the
owner chose tracing, on condition that its cost is measured, that snapshots can
be turned down or off, and that a snapshot failure never fails a test.

## Investigation

Spike with Playwright 1.64.0 and Chromium on Windows: a five-step login page
(including a password field and a text field filled with a fake secret, and a
fetch to a JSON API), time-boxed to one hour. The spike code was throwaway and
is not in the repository.

| Question                          | Finding                                                                                                                                                                                                                                                                                                                                |
| --------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Trace group per step              | **Yes.** `context.tracing.group(stepId, { location })` / `groupEnd()` are public API. The group's title is the step ID and its location can be the **YAML file and line**, so the viewer shows step-file locations.                                                                                                                    |
| Open a single step directly       | **Not by URL**: the viewer reads only `?trace=<url>` and has no parameter to select an action. **But yes by chunk**: `tracing.startChunk()` / `stopChunk({ path })` around each step writes one zip per step that contains only that step.                                                                                             |
| Is a step's zip self-contained    | **Yes.** Each chunk repeats the context options and the resources it needs. Larger pages repeat their CSS and images in every chunk.                                                                                                                                                                                                   |
| Viewer without the Playwright CLI | **Yes, but not public API.** The viewer is a static web app inside `playwright-core/lib/vite/traceViewer` (not in the package's `exports`). Serving that folder from the engine and opening `/?trace=<step zip>` in a browser window worked offline.                                                                                   |
| Network and console data          | Included: network entries with URLs and bodies, console and action logs, before/after DOM snapshots, action parameters.                                                                                                                                                                                                                |
| Secrets                           | A secret appears in **action parameters** (`fill` value), **DOM snapshots** (input values, **including password fields**), **log lines**, **network URLs**, and in **screencast frames** (images). Rewriting the text files in the zip and dropping screencast frames produced a trace the viewer still opened, with `•••` everywhere. |
| Cross-browser                     | Tracing is supported by Playwright for all browsers; only Chromium was tested (v0.1.0 is Chromium only).                                                                                                                                                                                                                               |

## Measurements

**Setup.** Playwright 1.64.0, Chromium headless shell 156, Windows 10, Node
24.11. A local page with a 6 KB stylesheet, a 30 KB image, a form with a
20-option `select` and a 30-item list. One test of 20 steps: a `goto`, then 19
steps cycling through `fill`, `click` (appends a list item), `selectOption` and
`fill`. Each step was timed from before the action to after the chunk was
written, once with no tracing and once with `tracing.start({ snapshots: true,
screenshots: false })` plus `startChunk` / `group` / `groupEnd` / `stopChunk`
around every step. 5 runs per mode, alternating, giving 95 measured steps per
mode (the `goto` is reported separately). Chunk contents were read with
`adm-zip`; "repeated" counts resource entries whose name (a content hash) was
also in the previous step's chunk. Masking was timed by rewriting every text
entry of each chunk and writing a new zip.

| Measure                                        | Result                                                                                       |
| ---------------------------------------------- | -------------------------------------------------------------------------------------------- |
| Median time per step, no tracing               | 5.6 ms                                                                                       |
| Median time per step, one trace chunk per step | 24.6 ms (**+19 ms per step**)                                                                |
| Mean time per step (no tracing / tracing)      | 15.2 ms / 31.3 ms (+16 ms)                                                                   |
| `goto` step (no tracing / tracing, one run)    | 88 ms / 77 ms (within noise)                                                                 |
| Disk per step (compressed zip)                 | median **7.0 KB** (5.9–7.2 KB); 139 KB for 20 steps                                          |
| Repeated from the previous step                | 49 KB of 139 KB: **35 %** of all bytes are resources (CSS, image) copied again in each chunk |
| Masking rewrite of one chunk                   | 4.2 ms                                                                                       |

Reading: on a small page, a snapshot per step costs about 20 ms and 7 KB, plus
about 4 ms for masking. A 500-step suite would add roughly 12 seconds and
3.5 MB. The repeated share grows with the page's own resources: a page with
1 MB of CSS, fonts and images would add up to that much to every chunk, which is
what the `onFailure` and `off` settings are for. These numbers come from one
machine and one small page; the demo-app suite will re-measure them in CI.

## Decision

- Hide the format behind an internal interface in the `pagestate` module:

  ```ts
  interface PageStateRecorder {
    beginStep(step: StepRef): Promise<void>;
    endStep(step: StepRef, outcome: StepOutcome): Promise<PageState>;
    open(snapshot: SnapshotHandle): Promise<void>;
  }
  ```

  Clients see only `screenshotReady` (a PNG path), `snapshotReady` and the
  snapshot status in the step result, and open snapshots through
  `openSnapshot`. The format is not in the protocol.

- **Implementation: Playwright tracing**, started per browser context with
  `snapshots: true`, `screenshots: false`; one chunk per step, wrapped in a
  group titled with the step ID at the step's YAML location. Each chunk is
  masked ([ADR 0014](0014-secret-masking.md)) before `snapshotReady`.
- **Setting `snapshots`** in the config's `defaults`, overridable per
  environment:

  | Value       | Behaviour                                                                                       |
  | ----------- | ----------------------------------------------------------------------------------------------- |
  | `always`    | Keep a snapshot for every step (**default**, from the measurements above)                       |
  | `onFailure` | Trace every step, keep the chunk only for a failed step, delete the others when the step passes |
  | `off`       | No tracing at all; screenshots only                                                             |

  `onFailure` still pays the time cost (the trace must exist before we know the
  step failed) but saves the disk; `off` saves both.

- **Failures never fail a test.** If tracing, writing the chunk or masking it
  fails, the engine deletes any partial chunk, keeps the screenshot, and reports
  `snapshot: "failed"` with a reason in the step result (and no
  `snapshotReady`). If the viewer cannot be started or loaded on
  `openSnapshot`, the request fails with `SnapshotUnavailable` and includes the
  step's screenshot path so the client can show that instead. The step's own
  status is never changed by any of this.
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
- **Default `onFailure`**: saves disk but leaves nothing to compare a failing
  step with; at 7 KB per step the cost of `always` is small.

## Consequences

- Snapshots show before and after states, network and console, and work for any
  browser Playwright supports, which suits later milestones with more browsers.
- We depend on an internal file layout of `playwright-core`; the exact pin and
  the integration test contain that risk.
- Disk use grows with page size because each step chunk repeats its resources.
- The snapshot is shown inside the trace viewer (in a browser window), not as a
  live page loaded in the test browser.

## Revisit when

- a Playwright upgrade moves or removes `lib/vite/traceViewer`, or the viewer
  stops accepting `?trace=`, or
- Playwright adds a public API for serving the trace viewer (adopt it), or
- the demo-app suite in CI shows more than 50 ms added per step, or a typical
  run folder exceeds 200 MB for 50 steps (reconsider the default), or
- a secret is found unmasked in a trace despite ADR 0014.
