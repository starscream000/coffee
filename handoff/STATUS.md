# Status

Written only by the reviewer. The implementer reads this first.

- Updated: 2026-10-09
- **Open instruction:** [0004: review fixes, user actions, variables and secrets](instructions/0004-fixes-actions-and-secrets.md)
- Waiting on: implementer

## Instructions

| No.  | Title                                             | State  | Report                                          | Review                                          | Pull requests                                        |
| ---- | ------------------------------------------------- | ------ | ----------------------------------------------- | ----------------------------------------------- | ---------------------------------------------------- |
| 0001 | Finish the Milestone 0 documents                  | merged | [0001](reports/0001-finish-milestone-0-docs.md) | [0001](reviews/0001-finish-milestone-0-docs.md) | [#2](https://github.com/starscream000/coffee/pull/2) |
| 0002 | Review fixes and the final names                  | merged | [0002](reports/0002-review-fixes-and-names.md)  | [0002](reviews/0002-review-fixes-and-names.md)  | #3, #4                                               |
| 0003 | Milestone 1 foundation                            | merged | [0003](reports/0003-milestone-1-foundation.md)  | [0003](reviews/0003-milestone-1-foundation.md)  | #5, #6, #7, #8, #9                                   |
| 0004 | Review fixes, user actions, variables and secrets | open   | –                                               | –                                               | –                                                    |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

## Where the plan stands

Milestone 0 is finished. Of the 16 branches in
[the v0.1.0 plan](../docs/milestones/v0.1.0-plan.md), 1 to 3 are on `main`.
Instruction 0004 covers 4 and 5.

## Owner decisions on record

- 2026-10-09: the command name is `cfe`; the Milestone 1 plan is approved; ADR
  0018 is approved; merge first, then fix the findings of review 0003;
  `${row.…}` stays unavailable inside flows.

## Waiting on the owner

- npm: create the organisation `cfe` to hold the `@cfe` scope. A free name can
  be taken at any time.
- Repository visibility: it is public. Decide whether it should be private.
- Node: upgrade the local install to the current 24 LTS patch, then say so, so
  the pin can be raised.
