# Status

Written only by the reviewer. The implementer reads this first.

- Updated: 2026-10-09
- **Open instruction:** none. Instruction 0001 is reviewed and approved; its
  pull request waits for the owner's `merge`.
- Waiting on: owner

## Instructions

| No.  | Title                            | State    | Report                                      | Review                                          | Pull requests                                        |
| ---- | -------------------------------- | -------- | ------------------------------------------- | ----------------------------------------------- | ---------------------------------------------------- |
| 0001 | Finish the Milestone 0 documents | approved | on branch `docs/milestone-0-review-changes` | [0001](reviews/0001-finish-milestone-0-docs.md) | [#2](https://github.com/starscream000/coffee/pull/2) |

States: `open` (published, no review yet), `changes requested`, `approved`,
`merged`, `replaced`.

## Waiting on the owner

- `merge` for pull request #2.
- Names: choose the command, data folder and npm scope from the three options
  in report 0001 (the review recommends `coffeeqa`).
- ADR 0018 (saved-login cache): approve, once the review's findings are applied.
- Go-ahead for the Milestone 1 plan in `docs/milestones/v0.1.0-plan.md`.
- Pull requests: install the GitHub CLI on the implementer's machine, or keep
  having the reviewer open them.
- Repository visibility: it is public. Decide whether it should be private.
- Node: upgrade the local install to the current 24 LTS patch, then say so, so
  the pin can be raised.
