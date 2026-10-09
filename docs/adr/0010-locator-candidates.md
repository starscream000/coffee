# 0010. Resolve targets by trying candidates in stored order

- Status: Proposed
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
- **Self-healing (rewrite the file when a fallback matches)**: changes the
  source of truth behind the user's back; the recorder or desktop app can offer
  this explicitly later.

## Consequences

Tests survive changes that break one candidate, and clients can show which
targets have drifted. A wrong but unique fallback match is possible; the
reliability ordering keeps that risk low, and the reported `candidateIndex`
makes it visible. `expect.count` is the documented exception (several matches
expected), and `expect.visible: false` passes when no candidate matches a
visible element.

## Revisit when

Integration or user reports show fallbacks matching the wrong element, or
polling every candidate makes steps measurably slow (more than 200 ms of
overhead per step on the demo app).
