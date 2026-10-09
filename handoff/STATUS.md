# Status

Written only by the reviewer. The implementer reads this first.

- Updated: 2026-10-09
- **Open instruction:** none. Instructions 0002 and 0003 are reviewed and
  approved; their pull requests wait for the owner's `merge`.
- Waiting on: owner

## Instructions

| No.  | Title                            | State    | Report                                          | Review                                          | Pull requests                                        |
| ---- | -------------------------------- | -------- | ----------------------------------------------- | ----------------------------------------------- | ---------------------------------------------------- |
| 0001 | Finish the Milestone 0 documents | merged   | [0001](reports/0001-finish-milestone-0-docs.md) | [0001](reviews/0001-finish-milestone-0-docs.md) | [#2](https://github.com/starscream000/coffee/pull/2) |
| 0002 | Review fixes and the final names | approved | on branch `chore/apply-product-names`           | [0002](reviews/0002-review-fixes-and-names.md)  | #3, #4                                               |
| 0003 | Milestone 1 foundation           | approved | on branch `feat/stepfile-validate`              | [0003](reviews/0003-milestone-1-foundation.md)  | #5, #6, #7, #8, #9                                   |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

Merge order: #3, #4, #5, #6, #7, #8, #9.

## Owner decisions on record

- 2026-10-09: the command name is `cfe`; the Milestone 1 plan is approved; ADR
  0018 is approved; instructions 0002 and 0003 were open together.

## Waiting on the owner

- `merge` for pull requests #3 to #9.
- One format question from review 0003: `${row.…}` is not available inside
  flows; a flow takes row values as parameters. Say if you want otherwise.
- npm: create the organisation `cfe` to hold the `@cfe` scope. A free name can
  be taken at any time.
- Repository visibility: it is public. Decide whether it should be private.
- Node: upgrade the local install to the current 24 LTS patch, then say so, so
  the pin can be raised.
