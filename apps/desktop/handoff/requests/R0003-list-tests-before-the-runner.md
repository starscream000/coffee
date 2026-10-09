# Request R0003: `listTests` before the runner

- Date: 2026-10-09
- Written by: desktop implementer
- To: reviewer, for the engine track's plan
- State: done
- Raised in: [report D0001](../reports/D0001-desktop-foundation.md)

## What is needed

The engine's `listTests` request, ahead of plan branch 9 if the plan allows,
or at least its file, name and tags fields (with `rows` added in branch 9).

## Why

`listTests` is how a client learns which files are tests, their names and
their tags. It is the backbone of the desktop's test explorer. Today the
engine answers "method not found" (report 0003 of the repository), and the
plan delivers it with data rows in branch 9, because `rows` needs the data
files to be read.

A client must not guess which files are tests: the config's globs decide, and
only the engine reads the config.

## Proposal

Either of:

1. Deliver `listTests` in an earlier branch (for example with branch 8), with
   `rows: 1` for every test until branch 9 reads data files. This changes the
   meaning of `rows` for a while; the reviewer may prefer option 2.
2. Leave the plan as it is. The desktop keeps its fallback until branch 9.

## Until then

When `listTests` answers "method not found", the desktop lists the files under
the project root whose names end in `.test.yaml` (skipping the data folder,
`node_modules` and dot-folders), shows them by path only, and says in the
explorer that names and tags appear once the engine can list tests. This
fallback can be wrong when the config's globs differ from that pattern; it is
removed as soon as the engine answers `listTests`.

## Answer

Accepted as option 2 (reviewer, 2026-10-09). The plan stays as it is. The
engine track's instruction 0006 covers plan branches 8 and 9, so `listTests`
arrives with it, complete with `rows`. The desktop keeps its fallback until
that is on `main`; this request is done then.

Done (reviewer, 2026-10-10): `listTests` is on `main` since pull request #25, with `rows`. The explorer's fallback can go in the next desktop instruction.
