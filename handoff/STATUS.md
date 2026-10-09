# Status

Written only by the reviewer. The implementer reads this first.

- Updated: 2026-10-09
- **Open instructions, in this order:**
  1. [0002: review fixes and the final names](instructions/0002-review-fixes-and-names.md)
  2. [0003: Milestone 1 foundation](instructions/0003-milestone-1-foundation.md)
- Carry out 0002 first. If it cannot be finished, stop and do not start 0003.
  See "Several open instructions" in [README.md](README.md).
- Waiting on: implementer

## Instructions

| No.  | Title                            | State  | Report                                          | Review                                          | Pull requests                                        |
| ---- | -------------------------------- | ------ | ----------------------------------------------- | ----------------------------------------------- | ---------------------------------------------------- |
| 0001 | Finish the Milestone 0 documents | merged | [0001](reports/0001-finish-milestone-0-docs.md) | [0001](reviews/0001-finish-milestone-0-docs.md) | [#2](https://github.com/starscream000/coffee/pull/2) |
| 0002 | Review fixes and the final names | open   | –                                               | –                                               | –                                                    |
| 0003 | Milestone 1 foundation           | open   | –                                               | –                                               | –                                                    |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

## Owner decisions on record

- 2026-10-09: the command name is `cfe`; the Milestone 1 plan is approved; ADR
  0018 is approved; instructions 0002 and 0003 are open together.

## Waiting on the owner

- Three design choices the reviewer made where the plan was silent (R1 to R3 in
  instruction 0003). Reverse any of them at the next `check` if you disagree.
- npm: create the organisation `cfe` to hold the `@cfe` scope. A free name can
  be taken at any time.
- Repository visibility: it is public. Decide whether it should be private.
- Node: upgrade the local install to the current 24 LTS patch, then say so, so
  the pin can be raised.
