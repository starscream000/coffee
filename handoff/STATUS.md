# Status

Written only by the reviewer. The implementer reads this first, then
[apps/desktop/handoff/STATUS.md](../apps/desktop/handoff/STATUS.md).

- Updated: 2026-10-10
- **Open instruction:** [0010: recording and new projects over the protocol](instructions/0010-recording-and-new-projects-over-the-protocol.md)
- Waiting on: implementer

## Order of work

One implementer works on both tracks. Open instructions, in the order to carry
them out:

1. Engine: [0010](instructions/0010-recording-and-new-projects-over-the-protocol.md).
2. Desktop: [D0004](../apps/desktop/handoff/instructions/D0004-recording-new-projects-and-a-clear-layout.md),
   whose first branch sits on the last branch of 0010.

Finish and report one before starting the other.

## Instructions

| No.  | Title                                               | State  | Report                                                          | Review                                                          | Pull requests                                        |
| ---- | --------------------------------------------------- | ------ | --------------------------------------------------------------- | --------------------------------------------------------------- | ---------------------------------------------------- |
| 0001 | Finish the Milestone 0 documents                    | merged | [0001](reports/0001-finish-milestone-0-docs.md)                 | [0001](reviews/0001-finish-milestone-0-docs.md)                 | [#2](https://github.com/starscream000/coffee/pull/2) |
| 0002 | Review fixes and the final names                    | merged | [0002](reports/0002-review-fixes-and-names.md)                  | [0002](reviews/0002-review-fixes-and-names.md)                  | #3, #4                                               |
| 0003 | Milestone 1 foundation                              | merged | [0003](reports/0003-milestone-1-foundation.md)                  | [0003](reviews/0003-milestone-1-foundation.md)                  | #5, #6, #7, #8, #9                                   |
| 0004 | Review fixes, user actions, variables and secrets   | merged | [0004](reports/0004-fixes-actions-and-secrets.md)               | [0004](reviews/0004-fixes-actions-and-secrets.md)               | #10, #11, #12, #13                                   |
| 0005 | Masking and loader fixes, locators, the demo app    | merged | [0005](reports/0005-masking-loader-locators-demo-app.md)        | [0005](reviews/0005-masking-loader-locators-demo-app.md)        | #14, #13, #15, #17                                   |
| 0006 | Review fixes and the runner                         | merged | [0006](reports/0006-review-fixes-and-the-runner.md)             | [0006](reviews/0006-review-fixes-and-the-runner.md)             | #19, #21, #23, #25                                   |
| 0007 | A build for the owner, then fixes, pages and logins | merged | [0007](reports/0007-a-build-then-fixes-and-pages-and-logins.md) | [0007](reviews/0007-a-build-then-fixes-and-pages-and-logins.md) | #25, #27, #28, #29                                   |
| 0008 | The demo `.env` and the built-in actions            | merged | [0008](reports/0008-demo-env-and-the-built-in-actions.md)       | [0008](reviews/0008-demo-env-and-the-built-in-actions.md)       | #30, #43, #44, #45, #46                              |
| 0009 | The recorder, first part                            | merged | [0009](reports/0009-the-recorder-first-part.md)                 | [0009](reviews/0009-the-recorder-first-part.md)                 | #48 to #52                                           |
| 0010 | Recording and new projects over the protocol        | open   | –                                                               | –                                                               | –                                                    |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

## Where the plan stands

Milestone 0 is finished. Of the 16 branches in
[the v0.1.0 plan](../docs/milestones/v0.1.0-plan.md), 1 to 13 are on `main`,
and so is the first part of the recorder: a person uses a page in the engine's
browser, and the engine writes a test that runs.

Instruction 0010 puts recording and "new project" on the protocol, for the
desktop app. Plan branches 14 (page states), 15 (the command-line `run`) and
16 (the release) follow later.

## The desktop track

The Avalonia app lives under `apps/desktop/`, with its own instructions,
reports and reviews in [apps/desktop/handoff/](../apps/desktop/handoff/). Its
instructions D0001 to D0003 are merged. Since 2026-10-10 the same implementer
works on it, as a separate track with its own scope.

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
- 2026-10-10: the desktop track was on hold for a few hours and resumes with
  its instruction D0003.
- 2026-10-10: the demo project gets a committed, demo-only
  `examples/demo-app/.env` with `DEMO_PASSWORD`, as the one exception to "never
  commit `.env` files" (review 0007, question 1). It goes into instruction 0008.
- 2026-10-10: the recorder is built now, ahead of plan branches 14 to 16;
  "it is the main bit".
- 2026-10-10: the separate desktop implementer is let go. The engine
  implementer carries on the desktop work. The two tracks stay separate:
  separate instructions, reports, branches and scopes.
- 2026-10-10, during instruction 0009, to the implementer: test ID comes
  before text in the order of candidates; a typed password that matches no
  declared secret becomes a placeholder variable marked for review.

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
- 2026-10-10, also to be noted: the desktop app is meant to make the whole
  experience low-code, or even no-code. A tester should be able to record,
  edit and run tests without writing code; the YAML files and the TypeScript
  actions stay underneath for those who want them.

## Waiting on the owner

- npm: create the organisation `cfe` to hold the `@cfe` scope. A free name can
  be taken at any time.
- Repository visibility: it is public. Decide whether it should be private.
- Node: upgrade the local install to the current 24 LTS patch, then say so, so
  the pin can be raised.
- The desktop track's open questions are in
  [its status file](../apps/desktop/handoff/STATUS.md).
