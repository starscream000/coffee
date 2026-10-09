# Instruction 0001: finish the Milestone 0 documents

- Date: 2026-10-09
- Written by: reviewer
- Based on `main` at: `1c873f1`
- Replaces: the review messages relayed in chat before this folder existed
- Follows review: none

## Goal

The design documents on `main` carry every change the two chat reviews asked
for, every ADR from 0005 to 0017 is marked Accepted, and a Milestone 1 plan is
ready for the owner's go-ahead. No engine logic is written.

Everything you need is in this file. Do not rely on the earlier chat messages:
where they differ from this file, this file wins.

## Owner decisions

1. From now on, work is passed through `handoff/`. Read
   [handoff/README.md](../README.md) before starting.
2. The implementer no longer merges into `main` and never pushes to `main`. It
   pushes branches and opens pull requests; the reviewer merges on the owner's
   word.
3. `main` on GitHub stays as it is. It points at `1c873f1`, which is the tip of
   `docs/milestone-0-proposals`, so the design proposals reached `main` before
   they were approved and without a `--no-ff` merge. The owner accepts this once
   rather than rewriting history. No force-push.
4. The display name "Coffee" is final. The command name, data folder name and
   npm scope are still open (task 14).
5. Page snapshots use Playwright tracing, one trace per step (ADR 0007), with
   the conditions in task 3.
6. The engine has its own runner on the Playwright library (ADR 0012).
7. Built-in action names keep their dots. The namespaces `expect`, `wait`,
   `api` and the product's command name are reserved for built-ins.
8. Old runs: a "keep last N runs" setting, default 20, where 0 keeps all.
9. v0.1.0 is not tagged until CI has passed on Linux, Windows and macOS.

## Branches

| Branch                            | Based on | What it holds                |
| --------------------------------- | -------- | ---------------------------- |
| `docs/milestone-0-review-changes` | `main`   | Tasks 2 to 15 and the report |

Task 1 changes no files.

## Tasks

### Part A: bring the two copies of the repository back in step

1. Before changing anything, find out and write in the report:
   - what your local `main` points at, and whether it has commits that
     `origin/main` lacks;
   - which command put the tip of `docs/milestone-0-proposals` on GitHub as
     `main`;
   - every local branch that is not on GitHub, and what each holds.

   Then:
   - if local `main` is behind or equal to `origin/main`, run
     `git switch main` and `git pull --ff-only`;
   - if local `main` has commits that `origin/main` lacks, stop, do not push
     `main`, and say so in the report;
   - if you have unpushed work that already covers some of the tasks below,
     bring it onto `docs/milestone-0-review-changes` (merge or cherry-pick, no
     rewriting of pushed history) and say which tasks it covers.

### Part B: ADR changes

2. **ADR 0005 (protocol framing).** Protocol messages never carry file
   contents. Screenshots, snapshots and other artifacts are referenced by path.
   State a maximum message size and what the engine and a client do when a
   message exceeds it. Make protocol.md agree.
3. **ADR 0007 (page snapshots).**
   - Measure the cost of one trace per step on a small page: added time per
     step, disk size per step, and how much of that is repeated from step to
     step. Put the numbers and how you measured them in the ADR.
   - Add a setting with three values: snapshots always, on failure only, off.
     Propose the default from your measurements.
   - If the viewer or the masking step fails, the engine falls back to the
     screenshot alone and says so in the step result. A test never fails
     because a snapshot failed.
4. **ADR 0008 (loading user actions).** A user action must use the running
   engine's single copy of the SDK. The ADR already keeps the engine package
   external when bundling; remove the statement that the user's copy "may be a
   different version", say what happens when the user's installed version does
   not match the engine (a clear diagnostic, not silent acceptance), and add a
   check for the single copy to the definition of done.
5. **ADR 0010 (locator candidates).** A weaker candidate must not win only
   because the page is still loading. Give the first candidate a short grace
   period before any fallback is tried, make the period configurable, and emit
   a warning event whenever a candidate other than the first is used.
6. **ADR 0012 (own runner).** Add to "Revisit when": parallel runs and
   JUnit-style report output are required before the server milestone.
7. **ADR 0014 (secret masking).** Declared secrets are not the only sensitive
   data. Mask `Cookie`, `Set-Cookie` and `Authorization` headers in traces by
   default. Saved logins are stored under the git-ignored data folder, never in
   step files, and the acceptance scan includes them.
8. **ADR 0015 (results layout).** Add the "keep last N runs" setting from owner
   decision 8. It is applied at the start of a run.
9. **ADR 0016 (action names).** Record owner decision 7. Keep the reserved
   namespaces in one list in the code design, and have user actions that use
   one fail at load time with a clear message.
10. **Statuses.** When tasks 2 to 9 are done, set ADRs 0005 to 0017 to
    `Accepted (owner, 2026-10-09)`. ADR 0017 keeps a note that three names are
    pending. Move the matching entries in architecture.md from "Open questions"
    to "Review decisions".

### Part C: step format and actions

11. **docs/step-format.md.**
    - Frames: a target may say which embedded frame it is in, with a `frame` key
      that holds a locator for the frame and can be nested. `ctx.locate`
      supports it.
    - Scoping: a target may be `within` another target, and may use
      interpolation, so "the Delete button in the row named `${row.product}`"
      needs no custom action.
    - After steps: when an after step uses a variable that was never set, it is
      skipped and reported as "skipped: <name> was never set". It is not a
      failure.
    - Saved logins: add `maxAge` per login, default 12 hours, and a per-test
      option to sign in fresh without using or overwriting the saved state.
      Cache keys hash the parameter values and never store them.
    - Tests: add `skip: "<reason>"`. A skipped test appears in the results with
      its reason.
    - Config: add `viewport`, `locale` and `timezone` to `defaults`, each
      overridable per environment. Set a fixed default viewport so runs match
      across machines.
    - Add a section "Not in v0.1.0" that lists these as deliberate gaps: browser
      dialogs (alert and confirm), downloads, scrolling, typing key by key,
      assertions that continue after a failure, retries, a whole-test timeout,
      access to other named pages from inside an action, stable IDs for data
      rows.
    - Fix the example email `guest+${row.product}@example.com`, which produces
      a space for "Desk lamp".
12. **docs/actions.md.**
    - Remove the sentence saying a different version of the package in the
      user's repository "still works", and describe the rule from task 4.
    - SDK errors are recognised by a tag field, not by `instanceof`.
    - Say what happens when a user action ignores `ctx.signal` after a timeout:
      the step is marked failed, and the engine closes that page so the stray
      code cannot act during after steps.
    - Add a short "Trust model" section: user actions are code and run with the
      engine's full access to the machine and the network.
    - Update "Action names" for task 9.
13. **Keep the other documents in step.** architecture.md, protocol.md and
    docs/milestones/v0.1.0-definition-of-done.md must agree with tasks 2 to 12.
    Add sample step files or acceptance checks to the definition of done where
    a task adds behaviour that v0.1.0 must prove.

### Part D: names and plan

14. **Names.** Propose three options for the command name, the data folder name
    and the npm scope. `coffee` is CoffeeScript's command and `.coffee` is its
    file extension, so neither can stay. For each npm scope, check on the npm
    registry whether it is free and say how you checked. Put the options and
    your recommendation in the report under "Questions for the owner". Do not
    rename anything yet.
15. **Milestone 1 plan.** Write `docs/milestones/v0.1.0-plan.md`: the branches
    in order, what each delivers, and which definition-of-done checks each one
    turns green. Keep each branch small enough to review in one sitting.

## Done when

- [ ] `pnpm verify` passes on `docs/milestone-0-review-changes`.
- [ ] CI is green on Linux, Windows and macOS for the pull request.
- [ ] Every item in tasks 2 to 13 can be found in the named document.
- [ ] ADRs 0005 to 0017 read `Accepted`, and architecture.md has no open
      question that an owner decision above has settled.
- [ ] `docs/milestones/v0.1.0-plan.md` exists.
- [ ] `CHANGELOG.md` is updated in the same branch.
- [ ] `handoff/reports/0001-finish-milestone-0-docs.md` exists, follows
      [the template](../templates/report.md), and answers task 1 and task 14.
- [ ] The diff touches no file under `packages/*/src` and adds no dependency.

## Out of scope

- Any engine, protocol or CLI logic. Milestone 1 starts only after the owner
  approves the plan from task 15.
- Renaming the command, data folder or npm scope.
- Raising the Node pin. The owner has not confirmed the local upgrade yet.
- Pushing to `main`, merging into `main`, or force-pushing anything.

## Report back

- The answers to task 1.
- The measurements from task 3 and the default you propose.
- The name options from task 14.
- Anything in this instruction you think is wrong. Say so rather than working
  around it.
