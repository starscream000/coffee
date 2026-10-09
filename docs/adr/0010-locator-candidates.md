# 0010. Resolve targets by trying candidates in stored order

- Status: Proposed
- Date: 2026-10-09

## Context

Each target stores several locator candidates in order of reliability (role and
name, test ID, CSS last). The engine must pick one at run time.

## Decision

Repeat until the step timeout: try each candidate in stored order and use the
first one that matches **exactly one** element. Candidates that match zero or
several elements are skipped. When the timeout passes, fail with
`TargetNotFound`, listing every candidate and its match count. Report the index
of the candidate used in `stepPassed.locator`. The engine never rewrites step
files on its own.

## Consequences

Tests survive changes that break one candidate, and clients can show which
targets have drifted. A wrong but unique fallback match is possible; the
reliability ordering keeps that risk low, and the "fallback used" signal makes
it visible. `expect.count` is the documented exception, because several matches
are expected there.
