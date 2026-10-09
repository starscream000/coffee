# Report 0001: finish the Milestone 0 documents

- Date: 2026-10-09
- Written by: implementer
- Instruction: [0001](../instructions/0001-finish-milestone-0-docs.md)

## Summary

Tasks 2 to 15 are done on `docs/milestone-0-review-changes`, which is pushed.
It also carries five commits that were on my local `main` but never pushed.
They cover most of tasks 11 and 12. ADRs 0005 to 0017 are Accepted. The trace
cost is measured (about 19 ms and 7 KB per step), and the v0.1.0 plan is
written. **The pull request is not open:** this machine has no GitHub CLI and
no authorised GitHub connector. CI only runs on pull requests, so **CI has not
run**. The owner or reviewer needs to open the pull request (link below), or
give me a way to.

## Branches and pull requests

| Branch                            | Last commit                                          | Pull request                                                                                                | Pushed |
| --------------------------------- | ---------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- | ------ |
| `docs/milestone-0-review-changes` | the commit that adds this report (tip of the branch) | not opened; create it at <https://github.com/starscream000/coffee/pull/new/docs/milestone-0-review-changes> | yes    |

## Tasks

| Task | State | Notes                                                                                                                                                                                                                                                  |
| ---- | ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1    | done  | Answers below. Local `main` had commits that `origin/main` lacked, so I did not pull or push `main`. The unpushed work was merged into the work branch.                                                                                                |
| 2    | done  | [ADR 0005](../../docs/adr/0005-json-rpc-over-stdio.md), [protocol.md, Transport](../../docs/protocol.md#transport): no file contents; 4 MiB limit; engine and client behaviour; error `MessageTooLarge` (-32009).                                      |
| 3    | done  | [ADR 0007](../../docs/adr/0007-page-snapshot-format.md): measurements and method, `snapshots` setting (`always` / `onFailure` / `off`, default `always`), screenshot-only fallback, `SnapshotUnavailable` (-32010), `snapshot` status in step results. |
| 4    | done  | [ADR 0008](../../docs/adr/0008-loading-user-actions.md): single SDK copy, `SdkVersionMismatch` diagnostic with both versions, definition-of-done check I12.                                                                                            |
| 5    | done  | [ADR 0010](../../docs/adr/0010-locator-candidates.md): `fallbackGrace` (default 1 s, configurable, `0s` turns it off) and a `LocatorFallback` warning on every fallback; samples S11 and S12.                                                          |
| 6    | done  | [ADR 0012](../../docs/adr/0012-own-runner.md) "Revisit when".                                                                                                                                                                                          |
| 7    | done  | [ADR 0014](../../docs/adr/0014-secret-masking.md): `Cookie`, `Set-Cookie` and `Authorization` masked in traces by default. Saved logins live only under the data folder. Acceptance scan A9 covers `.coffee/logins/`.                                  |
| 8    | done  | [ADR 0015](../../docs/adr/0015-results-layout.md): `keepRuns`, default 20, 0 keeps all, applied at the start of a run; check I9.                                                                                                                       |
| 9    | done  | [ADR 0016](../../docs/adr/0016-action-names.md): owner decision 7 recorded; one `RESERVED_NAMESPACES` constant; `ActionNamespaceReserved` at load time; checks A7 and F9.                                                                              |
| 10   | done  | ADRs 0005–0017 read `Accepted (owner, 2026-10-09)`. 0017 notes that three names are pending. Open questions 13, 15 and 16 moved to "Review decisions" in architecture.md.                                                                              |
| 11   | done  | [step-format.md](../../docs/step-format.md): frames, `within`, unset variables in `after`, `maxAge`, `freshLogin`, `skip`, viewport/locale/timezone, "Not in v0.1.0", e-mail fix (uses a `sku` column). Mostly from the five carried-over commits.     |
| 12   | done  | [actions.md](../../docs/actions.md): SDK rule, `sdkError` tag, ignored `ctx.signal`, trust model, reserved namespaces.                                                                                                                                 |
| 13   | done  | architecture.md, protocol.md and the [definition of done](../../docs/milestones/v0.1.0-definition-of-done.md) agree. Every check now has an ID (A, S, F, I). New checks: S11, S12, A7, A9, I3, I5–I7, I9, I12.                                         |
| 14   | done  | Options and method under "Questions for the owner". Nothing renamed.                                                                                                                                                                                   |
| 15   | done  | [docs/milestones/v0.1.0-plan.md](../../docs/milestones/v0.1.0-plan.md): 15 branches, each mapped to the checks it turns green; every check appears once.                                                                                               |

### Task 1 answers

**Local `main`.** It points at `7f0006b`. Against `origin/main` (`4f32aba`) it
was 5 commits ahead and 4 behind. The 4 it lacks are PR #1 (the handoff folder).
The 5 that `origin/main` lacks are:

- `45416f5` docs: add frames, within, skip, run settings and login options to the step format
- `6b6d12d` docs: require the engine's single SDK copy and add the trust model
- `189a413` docs: add skip reasons, testSkipped, run settings and nested locator uses to the protocol
- `dea1544` docs: add ADRs 0018 and 0019, supersede 0017
- `7f0006b` docs: record second review decisions and extend the definition of done

I made them for the second chat review, after `main` had been renamed (see
below). They landed on `main` only because the branch I was working on had
become `main`. I did not push them or reset `main`.

**How the tip of `docs/milestone-0-proposals` became `main` on GitHub.** Two
commands, neither run by me; my session never renamed a branch or pushed.

1. The local reflog shows
   `Branch: renamed refs/heads/docs/milestone-0-proposals to refs/heads/main`
   at 2026-10-09 06:51:38 +05:30. That is `git branch -M main`, run while
   `docs/milestone-0-proposals` was checked out. It needs `-M` (forced) because
   `main` already existed; the old `main` tip `4d3cb1d` is contained in the
   renamed branch, so nothing was lost.
2. The remote-tracking reflog for `origin/main` shows `update by push` from
   `0000000` to `1c873f1`, at Unix time 1791508910, right after the rename. That
   is a first push of `main` to the new remote, such as `git push -u origin main`.

**Local branches not on GitHub.** All four are already contained in
`origin/main` and can be deleted locally:

| Branch                         | Tip       | Holds                           |
| ------------------------------ | --------- | ------------------------------- |
| `chore/project-rules`          | `a45b660` | CLAUDE.md (first version)       |
| `chore/scaffold`               | `d209aec` | Workspace, tooling, CI, README  |
| `chore/product-placeholder`    | `da81c01` | First `PRODUCT` constant        |
| `chore/split-product-identity` | `a26c6fb` | `PRODUCT` split into four names |

Local `main` itself also held the 5 unpushed commits above. They are now in
`docs/milestone-0-review-changes`.

### Task 3 measurements

Full method and table in [ADR 0007](../../docs/adr/0007-page-snapshot-format.md#measurements).

- **Setup:** Playwright 1.64.0, headless Chromium, Windows, 5 runs × 19 timed
  steps per mode, on a page with a 6 KB stylesheet and a 30 KB image.
- **Time:** median step 5.6 ms without tracing and 24.6 ms with one chunk per
  step, so **+19 ms**. The mean gives +16 ms.
- **Disk:** median **7.0 KB per step**. **35 %** of all bytes are resources
  repeated from the previous step's chunk.
- **Masking:** rewriting one chunk takes 4.2 ms.
- **Proposed default:** `snapshots: always`.

## Checks

| Command                                                        | Result                                                             |
| -------------------------------------------------------------- | ------------------------------------------------------------------ |
| `pnpm verify` (Windows, Node 24.11.0)                          | passed: type check, lint, Prettier, 10 unit tests in 4 files       |
| Relative-link and anchor check (ad hoc)                        | no broken links except template placeholders (`NNNN-…`)            |
| `git diff origin/main -- packages pnpm-lock.yaml package.json` | empty: no file under `packages/*/src` changed, no dependency added |
| CI on Linux, Windows, macOS                                    | **not run**: no pull request (see below)                           |

## Departures from the instruction

1. **No `git switch main && git pull --ff-only`.** Local `main` had diverged, so
   I followed the "stop, do not push `main`" branch of task 1. I created
   `docs/milestone-0-review-changes` from `origin/main`. I then **merged**
   `7f0006b` into it rather than cherry-picking. That keeps the five commits'
   hashes, so local `main` can fast-forward once this branch is merged into
   `origin/main`.
2. **ADR 0017 and 0019.** One of the carried-over commits had added ADR 0019 and
   marked 0017 "Superseded by 0019". Task 10 asks for 0017 to read Accepted,
   with a note on the pending names. So 0017 now reads "Accepted (owner,
   2026-10-09). Amended by 0019: … still pending", and 0019 says "Amends" rather
   than "Supersedes".
3. **ADRs 0018 and 0019 exist, and the instruction does not mention them.**
   - 0019 records the owner's decision to split the names, so it is Accepted.
   - 0018 (saved-login cache keyed by HMAC) is my design and **stays Proposed**
     for review.
4. **Pull request not opened.** No GitHub CLI and no authorised GitHub connector
   here. Pushing worked through Git's stored credentials, but opening a pull
   request needs the GitHub API.
5. **Header masking is wider than asked.** Task 7 asks for `Cookie`,
   `Set-Cookie` and `Authorization` to be masked in traces. ADR 0014 also masks
   them in headers stored by `api`, `wait.response` and `expect.response`. The
   reviewer may narrow this back.

## Decisions I made

- **Snapshots:** default `always`, because at about 7 KB and 20 ms per step the
  cost is small. `onFailure` still traces every step and keeps only the failing
  step's chunk.
- **Message size:** 4 MiB limit; string fields truncated at 64 KiB first. New
  error codes `-32009 MessageTooLarge` and `-32010 SnapshotUnavailable`, the
  latter carrying `data.screenshot`.
- **`fallbackGrace`:** default 1 s, overridable per environment. It applies at
  every level (frame, `within`, element).
- **`keepRuns`:** project-wide, not per environment. It deletes only folders
  whose name is a valid run ID, and a failed deletion is a warning.
- **`SdkVersionMismatch`** is a warning, not an error, because the running code
  is always the engine's copy. The message names both versions and the command
  to fix it.
- **`RESERVED_NAMESPACES`** includes `PRODUCT.command`, so it follows a rename.
- **Check IDs and plan:**
  - The definition-of-done checks now have IDs.
  - I added S12 (slow render: the first candidate must win during the grace
    period) and I3 (message size).
  - The plan adds the integration CI job in branch 7, so checks are proven on
    all three systems as they turn green.

## Questions for the owner

**1. Command, data folder and npm scope** (task 14). Recommended: **A**.

| Option | Command      | Data folder    | npm scope     | npm package name | npm scope |
| ------ | ------------ | -------------- | ------------- | ---------------- | --------- |
| **A**  | `coffeeqa`   | `.coffeeqa/`   | `@coffeeqa`   | free             | free      |
| B      | `coffeetest` | `.coffeetest/` | `@coffeetest` | free             | free      |
| C      | `coffee-e2e` | `.coffee-e2e/` | `@coffee-e2e` | free             | free      |

- **Why A:** it is short, reads clearly as a testing tool (`coffeeqa run`), and
  has nothing to do with CoffeeScript. B could be read as "tests for
  CoffeeScript". C is the most explicit but the longest to type.
- **How I checked, on 2026-10-09:**
  - Package name: `GET https://registry.npmjs.org/<name>`; HTTP 404 means no
    package with that name.
  - Scope: `GET https://registry.npmjs.org/-/org/<name>/package`. That endpoint
    returned 200 for a known user scope (`sindresorhus`) and a known org scope
    (`types`), and 404 for a made-up name, so a 404 means no npm user or
    organisation owns the scope. All three scopes returned 404.
  - I didn't use the npmjs.com website, because it returns 403 to scripted
    requests.
  - Also free by the same checks: `coffeehq`, `coffeerun`, `coffeelab`,
    `coffeectl`, `brewqa`.
- **Not checked:** GitHub organisation names, other package registries, or
  programs with the same command name on each operating system.
- **Availability can change.** Creating the npm organisation soon after
  choosing secures the scope.

**2. ADR 0018 (saved-login cache).** Approve it as written (HMAC-SHA-256 keys,
`maxAge`, `freshLogin`), or ask for changes. Branch 9 of the plan implements it.
Recommended: approve.

**3. Opening pull requests.** Recommended: install the GitHub CLI
(`winget install GitHub.cli`, then `gh auth login`) so I can open pull requests
as the handoff process requires. Alternatives: authorise the GitHub connector,
or have the reviewer open them.

## Not done, not pushed, not verified

- **Pull request not opened**, so **CI has not run** on Linux, Windows or macOS
  for this branch. The first two "Done when" items are therefore unproven.
- `pnpm verify` ran on Windows only.
- The trace measurements come from one Windows machine and one small page; they
  have not been repeated on Linux or macOS.
- Local `main` still points at `7f0006b` (5 ahead, 4 behind `origin/main`). I
  did not reset it. After this branch is merged, `git pull --ff-only` on `main`
  will fast-forward, because `7f0006b` is part of this branch.

## Suggestions

- Delete the four local branches listed under task 1; they are fully merged.
- The CI workflow runs only on pull requests and on pushes to `main`. Running it
  on every pushed branch would show results before a pull request exists.
