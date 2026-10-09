# Status

Written only by the reviewer. The implementer reads this first.

- Updated: 2026-10-09
- **Open instruction:** [0002: review fixes and the final names](instructions/0002-review-fixes-and-names.md)
- Waiting on: implementer

## Instructions

| No.  | Title                            | State  | Report                                          | Review                                          | Pull requests                                        |
| ---- | -------------------------------- | ------ | ----------------------------------------------- | ----------------------------------------------- | ---------------------------------------------------- |
| 0001 | Finish the Milestone 0 documents | merged | [0001](reports/0001-finish-milestone-0-docs.md) | [0001](reviews/0001-finish-milestone-0-docs.md) | [#2](https://github.com/starscream000/coffee/pull/2) |
| 0002 | Review fixes and the final names | open   | –                                               | –                                               | –                                                    |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

## Waiting on the owner

- Go-ahead for the Milestone 1 plan in `docs/milestones/v0.1.0-plan.md`. No
  code starts without it.
- ADR 0018 (saved-login cache): approve or ask for changes. Instruction 0002
  applies the review's fixes to it first.
- npm: create the organisation `cfe` to hold the `@cfe` scope. A free name can
  be taken at any time.
- Repository visibility: it is public. Decide whether it should be private.
- Node: upgrade the local install to the current 24 LTS patch, then say so, so
  the pin can be raised.
