# Review 0001: finish the Milestone 0 documents

- Date: 2026-10-09
- Written by: reviewer
- Instruction: [0001](../instructions/0001-finish-milestone-0-docs.md)
- Report: `handoff/reports/0001-finish-milestone-0-docs.md` on the branch
  `docs/milestone-0-review-changes` (it reaches `main` when pull request #2 is
  merged)
- Verdict: **Approved**

## Pull requests

| Pull request                                         | Branch                            | Reviewed at | CI                                | Verdict  |
| ---------------------------------------------------- | --------------------------------- | ----------- | --------------------------------- | -------- |
| [#2](https://github.com/starscream000/coffee/pull/2) | `docs/milestone-0-review-changes` | `bd994d5`   | green on Linux, Windows and macOS | Approved |

The reviewer opened #2 on the implementer's behalf, because the implementer's
machine cannot open pull requests yet. The content is the implementer's,
unchanged.

## What I checked

- **The report**, against the template and against the branch. Every claim I
  could test was true.
- **`pnpm verify`** on `bd994d5`, on Linux with Node 24.11.0: type check, lint,
  format check and 10 tests in 4 files all pass.
- **CI** on `bd994d5`: `verify` passed on Linux, Windows and macOS.
- **Scope**: 22 files changed, all under `docs/` or `handoff/reports/`, plus
  `CHANGELOG.md` and `README.md`. Nothing under `packages/`, no dependency, no
  workflow or config change.
- **Every changed document, line by line**: ADRs 0005 to 0019, step-format.md,
  actions.md, protocol.md, architecture.md, the definition of done and the
  plan.
- **The plan's claim** that every definition-of-done check appears exactly once:
  true for A1–A9, S1–S18, F1–F9 and I1–I13.
- **Relative links** in `docs/` and the report: none broken (the ADR template's
  placeholder link aside).
- **The npm names** in the report: `coffeeqa`, `coffeetest` and `coffee-e2e`
  each returned 404 for the package and for the organisation on
  registry.npmjs.org, and a known organisation returned 200 on the same
  endpoint. The implementer's result holds as of this review.
- **History**: `7f0006b` (the implementer's local `main`) is an ancestor of
  the branch tip, so that local `main` will fast-forward after the merge.

## What I could not check

- **The trace measurements** in ADR 0007. They come from one Windows machine
  and I did not repeat them. The ADR says so and commits to re-measuring in CI.
- **How `main` was renamed.** The reflog lines the report quotes are on the
  owner's machine. One thing supports them: GitHub recorded the first push at
  2026-10-09 01:21:49 UTC, eleven seconds after the rename the report cites.
  The two commands (`git branch -M main`, `git push -u origin main`) are the
  ones GitHub shows on a new repository's page.

## Findings

None of these blocks the merge: no code exists yet, and `main` is better with
this branch than without it. All of them go into instruction 0002 and are fixed
before any Milestone 1 code.

1. **Should fix. Two documents disagree on how login cache keys are made.**
   `docs/step-format.md`, "Saved logins", says "a SHA-256 hash".
   `docs/adr/0018-saved-logins.md` says HMAC-SHA-256 and lists plain SHA-256
   under "Alternatives rejected". step-format.md also names only "the flow
   file's content", where the ADR adds every flow it calls. Make step-format.md
   say what the ADR says.
2. **Should fix. Masked header values would be unusable in tests.**
   `docs/adr/0014-secret-masking.md` says the header rule also applies to the
   `headers` that `api`, `wait.response` and `expect.response` "store in
   variables". A test could then never read a token from a response header and
   send it on. The instruction asked for masking in traces only, and the report
   flags this as wider than asked. Fix: variables keep the real value; the
   engine registers each such header value as a secret for the rest of the run,
   so it is masked wherever it is written out. Say the same in actions.md.
3. **Should fix. Old saved logins are never deleted.** ADR 0018 says old cache
   files "remain until the folder is cleared". Those files hold live session
   cookies. A changed password or flow gives a new key, and the old file stays
   on disk for good. Fix: at the start of a run, delete saved states older than
   their `maxAge`, and any state whose metadata names a login that the config no
   longer has. Add it to check I11.
4. **Should fix. The status lines disagree.** architecture.md and protocol.md
   read "Accepted design"; step-format.md and actions.md still read "Proposal,
   revised after the second review". After this merge all four are the agreed
   design and should say so.
5. **Note. ADR 0018 overstates what the HMAC key protects.** The key file sits
   in the same folder as the state files, so anyone who copies the folder has
   both. The key helps only when a file name leaks without the folder (in a log
   or a screenshot of a file listing). Say that plainly.
6. **Note. architecture.md has stale rows.** Review decision 5 still says the
   snapshot format is "awaiting approval", though decision 15 below it settles
   it. The open question about ADR 0018 reuses the number 15.
7. **Note. actions.md, "Trust model".** The paragraph opens with two bold
   sentences run together. One opening sentence is enough.
8. **Note. The definition of done, A7 and F9.** F9's project has three bad
   actions (`fillOtp`, `expect.priceFormat`, `click`), but A7 lists the expected
   output for two. Add the expected diagnostic for `click`.
9. **Note. The plan's branch 8 is too large.** It turns 14 checks green. The
   plan already describes how it would split; split it now rather than at
   review time.

What is good and should be kept as a habit: the report separated what was done
from what was not verified, flagged its own departures (ADRs 0018 and 0019, the
wider header masking), and left ADR 0018 as Proposed instead of accepting its
own design.

## Done-when checks

| Check                                                                  | Result                                      |
| ---------------------------------------------------------------------- | ------------------------------------------- |
| `pnpm verify` passes on the branch                                     | Yes (reviewer, Linux; implementer, Windows) |
| CI is green on Linux, Windows and macOS for the pull request           | Yes, on `bd994d5`                           |
| Every item in tasks 2 to 13 can be found in the named document         | Yes                                         |
| ADRs 0005 to 0017 read Accepted; no settled question still listed open | Yes; see finding 6 for one stale row        |
| `docs/milestones/v0.1.0-plan.md` exists                                | Yes                                         |
| `CHANGELOG.md` is updated in the same branch                           | Yes                                         |
| The report exists, follows the template, answers task 1 and task 14    | Yes                                         |
| The diff touches no file under `packages/*/src` and adds no dependency | Yes                                         |

## Owner decisions needed

1. **Command, data folder and npm scope.** Options: A `coffeeqa`, B
   `coffeetest`, C `coffee-e2e` (each with the matching folder and scope).
   Recommended: **A**, as the report argues. Whichever is chosen, create the npm
   organisation soon after, because a free name can be taken at any time. Not
   checked by anyone: trademarks, and programs with the same command name.
2. **ADR 0018 (saved-login cache).** Recommended: approve it once findings 1, 3
   and 5 are applied.
3. **How pull requests get opened.** Recommended: install the GitHub CLI on the
   implementer's machine (`winget install GitHub.cli`, then `gh auth login`), so
   the implementer finishes its own step and CI runs before `check`. Until
   then the reviewer opens them during `check`, as it did here.
4. **Go-ahead for the Milestone 1 plan**, after the names are chosen. Renaming
   is cheapest before the first code branch.
5. Still open from before: whether the repository stays public, and the local
   Node upgrade.

## For the next instruction

Instruction 0002, after pull request #2 is merged:

- Findings 1 to 9.
- Apply the chosen names, if the owner has chosen.
- After the merge, the implementer runs `git switch main` and
  `git pull --ff-only`, and deletes its four local branches that are already in
  `main`.
- No Milestone 1 code until the owner has approved the plan.
