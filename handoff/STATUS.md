# Status

Written only by the reviewer. The engine implementer reads this first. The
desktop implementer reads [apps/desktop/handoff/STATUS.md](../apps/desktop/handoff/STATUS.md)
instead.

- Updated: 2026-10-10
- **Open instruction:** [0007: a build for the owner, then fixes, pages and logins](instructions/0007-a-build-then-fixes-and-pages-and-logins.md)
- Waiting on: implementer

## Instructions

| No.  | Title                                               | State             | Report                                                   | Review                                                   | Pull requests                                        |
| ---- | --------------------------------------------------- | ----------------- | -------------------------------------------------------- | -------------------------------------------------------- | ---------------------------------------------------- |
| 0001 | Finish the Milestone 0 documents                    | merged            | [0001](reports/0001-finish-milestone-0-docs.md)          | [0001](reviews/0001-finish-milestone-0-docs.md)          | [#2](https://github.com/starscream000/coffee/pull/2) |
| 0002 | Review fixes and the final names                    | merged            | [0002](reports/0002-review-fixes-and-names.md)           | [0002](reviews/0002-review-fixes-and-names.md)           | #3, #4                                               |
| 0003 | Milestone 1 foundation                              | merged            | [0003](reports/0003-milestone-1-foundation.md)           | [0003](reviews/0003-milestone-1-foundation.md)           | #5, #6, #7, #8, #9                                   |
| 0004 | Review fixes, user actions, variables and secrets   | merged            | [0004](reports/0004-fixes-actions-and-secrets.md)        | [0004](reviews/0004-fixes-actions-and-secrets.md)        | #10, #11, #12, #13                                   |
| 0005 | Masking and loader fixes, locators, the demo app    | merged            | [0005](reports/0005-masking-loader-locators-demo-app.md) | [0005](reviews/0005-masking-loader-locators-demo-app.md) | #14, #13, #15, #17                                   |
| 0006 | Review fixes and the runner                         | changes requested | on branch `feat/runner-rows-results`                     | [0006](reviews/0006-review-fixes-and-the-runner.md)      | #19, #21, #23 merged; #25 open                       |
| 0007 | A build for the owner, then fixes, pages and logins | open              | –                                                        | –                                                        | continues #25                                        |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

## Where the plan stands

Milestone 0 is finished. Of the 16 branches in
[the v0.1.0 plan](../docs/milestones/v0.1.0-plan.md), 1 to 8 are on `main`: a
test file now runs in a real browser and reports through the event stream.
Branch 9 (data rows, skipped tests, the run folder, `listTests`) is pull
request #25 and waits for two fixes (review 0006, findings 1 and 2).

## The desktop track

The Avalonia app is built by a second implementer under `apps/desktop/`, with
its own instructions, reports and reviews in
[apps/desktop/handoff/](../apps/desktop/handoff/). Its instructions D0001 and
D0002 are merged (pull requests #16, #20, #22 and #24), and so is the CI job
`desktop` that builds and tests it on three systems (pull request #18). That
job runs on every pull request, engine ones included, because the desktop
tests start the real engine and read the protocol's schema files.

## Owner decisions on record

- 2026-10-09: the command name is `cfe`; the Milestone 1 plan is approved; ADR
  0018 is approved; merge first, then fix the findings of review 0003;
  `${row.…}` stays unavailable inside flows; no `cfe validate` command.
- 2026-10-09: the desktop app is a separate track with its own implementer,
  confined to `apps/desktop/`, following the same handoff process.
- 2026-10-10: hidden elements count when `ctx.locate` looks for exactly one
  element (review 0005, question 1). It stays as implemented; instruction 0006
  writes the rule into the documents.
- 2026-10-10: a regular expression for a URL is written with the prefix
  `regex:` (as in `wait.url: 'regex:^/orders/\d+$'`); anything without the
  prefix is a glob, so `/orders/` is always a path (review 0004, question 1).
  Instruction 0006 was already open and leaves the old `/regex/` rule in
  place; the next engine instruction changes the documents, the schemas and
  the validator, before plan branch 12.
- 2026-10-10: CSV data files are read as spreadsheets write them (RFC 4180).
- 2026-10-10: the owner wants a build of the desktop app and the engine on his
  machine before other work; it is the first task of instruction 0007.
- 2026-10-10: the desktop track is on hold.

## Owner's notes (ideas, not decisions)

- 2026-10-10, a rough idea the owner asked to have noted. What he expects of
  the whole system, desktop app and engine together: the user records a flow,
  and the recording is runnable. A flow is a series of actions, and the engine
  must always recognise each thing the user did and map it to the right
  action. The YAML step files matter because they make it easy to create
  different versions of the same flow; that is where generative AI could help
  with test coverage later, perhaps through a plugin. This does not change the
  rule "No AI features" in `CLAUDE.md`: nothing is to be built from this note
  until the owner decides so.

## Waiting on the owner

- npm: create the organisation `cfe` to hold the `@cfe` scope. A free name can
  be taken at any time.
- Repository visibility: it is public. Decide whether it should be private.
- Node: upgrade the local install to the current 24 LTS patch, then say so, so
  the pin can be raised.
- The desktop track is on hold (owner, 2026-10-10). Its open questions are in
  [its status file](../apps/desktop/handoff/STATUS.md).
