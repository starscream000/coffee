# 0001. Record architecture decisions

- Status: Accepted
- Date: 2026-10-09

## Context

The product has several clients written in different languages and a public
protocol. Reasons for choices are easily lost between sessions.

## Decision

Record each significant technical choice as a short ADR in `docs/adr/`, numbered
in order, using [template.md](template.md). "Proposed" ADRs need owner approval;
"Accepted" ones are binding until superseded by a later ADR. ADRs are not
edited after acceptance except to mark them superseded or to add findings to
"Revisit when".

## Alternatives rejected

- Decisions only in commit messages or pull requests: hard to find later.
- One large design document: hides when and why each decision was made.

## Consequences

A new contributor (or a new session) can learn why things are as they are. The
decisions in the owner's brief are recorded in `CLAUDE.md` and not repeated here.

## Revisit when

ADRs regularly go stale without being superseded, or reviewers stop reading
them.
