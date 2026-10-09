# 0010. Resolve targets by trying candidates in stored order

- Status: Accepted (owner, 2026-10-09)
- Date: 2026-10-09

## Context

Each target stores several locator candidates in order of reliability (role and
name, test ID, CSS last). The engine must pick one at run time and, per the
review, report which one matched.

## Decision

`ctx.locate(target)` repeats until the step timeout or until `ctx.signal` is
aborted, at least every 100 ms: try each candidate in stored order and use the
first one that matches **exactly one** element. Candidates that match zero or
several elements are skipped. When time runs out, fail with `TargetNotFound`,
listing every candidate and its last match count.

**Grace period for the first candidate.** A weaker candidate must not win only
because the page is still loading. For the first `fallbackGrace` of each
`ctx.locate` call (default **1 s**, set in the config's `defaults` and
overridable per environment, `0s` turns it off) only the first candidate is
tried. Fallbacks are tried after that, in order, until the step timeout. The
same rule applies at every level (frame, `within`, element). When the step
timeout is shorter than `fallbackGrace`, the call still makes one attempt with
every candidate before it gives up, so a fallback can be found (with its
warning) instead of a `TargetNotFound` whose fallbacks were never tried.

**What "matches" means.** A candidate matches when exactly one attached element
fits it, hidden elements included: `ctx.locate` counts what Playwright's strict
mode counts, so the locator it returns never fails a later action as
ambiguous. Role candidates never see hidden elements, because Playwright's
`getByRole` leaves them out; test ID, CSS, text, label and placeholder
candidates do see them. A page with a hidden copy of an element (a mobile menu,
a template) therefore needs `nth` or `within` on candidates other than role
candidates.

**A candidate Playwright rejects** (a CSS or XPath selector it cannot parse)
fails the call at once with `InvalidSelector`, naming the target, the
candidate and Playwright's reason. Waiting cannot fix it, so it is not
retried.

**Warning on every fallback.** Whenever a candidate other than the first is
used, at any level, the engine emits a `log` event with level `warn`, code
`LocatorFallback`, the target name, the index used and the step's location, in
addition to the `candidateIndex` in the step result.

A target's `frame` and `within` (each itself a target, nestable) are resolved
first with the same rule, from the outside in: each frame target must match
exactly one `<iframe>`, whose content becomes the search scope; each `within`
target must match exactly one element, which becomes the scope. `${…}` in any
of these targets is interpolated with the current step's values before each
attempt.

Every call is recorded as a `LocatorUse` (`param`, `target`, `candidateIndex`,
`candidate`, and nested `frame` / `within` uses) and reported in
`stepPassed.locators` / `stepFailed.locators`. The engine never rewrites step
files on its own.

## Alternatives rejected

- **Only the first candidate, others as documentation**: brittle, defeats the
  purpose of storing several.
- **Combine all candidates into one Playwright locator (`.or()`)**: Playwright
  cannot tell us which branch matched, and strict mode fails when two branches
  match different elements.
- **Score-based choice (most specific match wins)**: harder to explain to
  testers than "first one that works, in the order you see".
- **No grace period**: on a page that renders progressively, a CSS fallback can
  match a placeholder before the role-based first candidate appears.
- **A grace period on every candidate**: makes a broken first candidate cost
  the grace period once per candidate instead of once per call.
- **Self-healing (rewrite the file when a fallback matches)**: changes the
  source of truth behind the user's back; the recorder or desktop app can offer
  this explicitly later.

## Consequences

Tests survive changes that break one candidate, and clients can show which
targets have drifted. A step whose first candidate is broken takes at least
`fallbackGrace` longer; the warning makes such steps easy to find and fix. A wrong but unique fallback match is possible; the
reliability ordering keeps that risk low, and the reported `candidateIndex`
makes it visible. `expect.count` is the documented exception (several matches
expected), and `expect.visible: false` passes when no candidate matches a
visible element.

## Revisit when

Integration or user reports show fallbacks matching the wrong element, or
polling every candidate makes steps measurably slow (more than 200 ms of
overhead per step on the demo app).
