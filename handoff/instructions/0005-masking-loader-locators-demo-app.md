# Instruction 0005: masking and loader fixes, locators, the demo app

- Date: 2026-10-09
- Written by: reviewer
- Based on `main` at: `d7584fa` or later
- Replaces: none
- Follows review: [0004](../reviews/0004-fixes-actions-and-secrets.md)

## Goal

Masking can no longer damage a protocol message, user actions that use
CommonJS packages load, pull request #13 can be merged, and plan branches 6 and
7 exist: `ctx.locate` works against a real Chromium, CI runs browser tests on
all three systems, and the demo web server and its test harness are ready for
the runner.

## Owner decisions

1. Pull requests #10, #11 and #12 are merged. #13 stays open until finding 1 of
   review 0004 is fixed on its branch.
2. No `cfe validate` command. The owner will test by hand once tests run.
3. **Not decided:** how a regular expression for a URL is written (`/regex/`
   or a `regex:` prefix). Leave the current behaviour and its documents exactly
   as they are in this instruction.

## Branches

| Branch                   | Based on                                   | Plan branch | What it holds          |
| ------------------------ | ------------------------------------------ | ----------- | ---------------------- |
| `fix/review-0004-loader` | `main`                                     | none        | Tasks 2 to 4           |
| `feat/context-secrets`   | itself (existing branch, pull request #13) | 5           | Tasks 5 to 10          |
| `feat/locators`          | `feat/context-secrets`                     | 6           | Tasks 11 to 16         |
| `test/demo-app-harness`  | `feat/locators`                            | 7           | Tasks 17 to 19, report |

`feat/context-secrets` already exists. Before task 5, merge
`fix/review-0004-loader` into it with a merge commit, so the stack stays in one
line. Add commits; never rewrite what is pushed. Pull request #13 stays the
pull request for that branch; update its description.

Open a pull request for each new branch, all into `main`, and say in each
description which branch it sits on. Merge order will be: the loader fix, #13,
locators, the demo app.

If a `feat/` or `test/` branch passes roughly 1,500 changed lines (not counting
fixtures, generated files and the lockfile), split it in two stacked branches
and say so in the report.

`docs/` is the specification. Where this instruction and a document under
`docs/` disagree, stop and say so in the report.

## Tasks

### Part A: housekeeping (no files change)

1. `git switch main` and `git pull --ff-only`. Delete the local branches that
   are now contained in `main`. Keep `feat/context-secrets`.

### Part B: the loader (`fix/review-0004-loader`)

The finding numbers are those of review 0004. Each fix comes with a test that
fails without it.

2. **Finding 2.** A user action that imports a CommonJS package loads, also
   when that package calls `require` on a Node built-in module. Give each bundle
   a working `require`. Test with a fixture package under
   `packages/engine/test/` that is CommonJS and calls `require('node:path')`
   and `require('path')`. When loading fails, the hint must fit the cause: say
   "move work into run()" only when top-level code of the action file threw.
3. **Finding 3.** After a file that an action imports has changed, a second
   `openProject` in the same engine process uses the new code. Name each bundle
   after the hashes of all its inputs, or import it in a way Node cannot answer
   from its module cache. Test it inside one process: open, change the helper,
   open again, and check the action's description or behaviour changed.
4. **Finding 7.** At `openProject`, delete bundles in `.cfe/cache/actions/`
   that no current action file maps to. A file that cannot be deleted is
   skipped without failing the request.

### Part C: masking and secrets (`feat/context-secrets`, pull request #13)

5. **Finding 1, the design.** Change masking so that this always holds: after
   masking, a message is valid JSON, validates against its protocol schema, and
   its identifiers are unchanged. The rule:
   - object keys are never masked, and the serialised text is never masked;
   - the envelope (`jsonrpc`, `id`, `method`), error codes and error names are
     never masked;
   - fields the protocol defines as identifiers or fixed values are never
     masked: `runId`, `testId`, `stepId`, `parentStepId`, `seq`, `section`,
     `action`, `page`, `status`, `level`, `severity`, `code`, `state`, every
     file and folder path, environment names and login names;
   - everything that carries free text or user data is masked: `message`,
     `hint`, `expected`, `actual`, `params`, `candidate`, `data`, `title`,
     `reason` where it is free text, log text, and anything the list above does
     not protect.

   Derive the protected fields from the protocol schemas or keep them in one
   table next to the schemas, so a new protocol field cannot be forgotten
   silently: a test must fail when a string field of a protocol message is in
   neither the protected set nor the masked set.

6. **Finding 1, the proof.** A property test: for every protocol example
   message, and for a secret taken in turn from every key and every string
   value in that message (when it is 4 characters or longer), the masked
   message parses, validates against its schema, and has every protected field
   unchanged. Add the four cases from review 0004 as named tests (`jsonrpc`,
   `result`, `line`, `2.0","`) through a real engine process.
7. **Finding 1, the record.** Update ADR 0014 with the rule and the reason:
   identifiers come from files in the repository, which never hold secret
   values. Say what is given up: a secret that equals part of a file path is
   not masked in paths.
8. **Finding 4.** Everything the engine writes to stderr passes through `mask`:
   the console replacement and the engine's own error logging. Test with an
   action file whose top-level code logs a registered secret, and with an
   internal error whose message contains one.
9. **Findings 5 and 6.** `${…}` paths reach own properties only
   (`${vars.x.constructor}` is `PathNotFound`). An empty environment variable
   counts as unset, so the `.env` value is used.
10. **Documents.** Write rulings 4 and 9 of review 0004 into the documents
    (what `null` and objects become when interpolated into text; `listActions`
    without an open project). Do not write a report on this branch; the one
    report for this instruction goes on the last branch.

### Part D: locators (`feat/locators`, plan branch 6)

The specification is ADR 0010, `docs/step-format.md` ("Targets"),
`docs/actions.md` ("`ctx.locate`") and the `LocatorUse` type in
`docs/protocol.md`.

11. **Playwright.** Add `playwright` to the engine at one exact version. Record
    the version in the report. Replace the placeholder types in
    `sdk/context.ts` with Playwright's `Page`, `APIRequestContext` and
    `Locator`. Redirect imports of `playwright` in user actions to the engine's
    copy, as ADR 0008 says, with a test.
12. **`ctx.locate`.** Given a target, the page and the current interpolation
    scope, return a Playwright `Locator`:
    - resolve a named target (file-local, then shared), a list of candidates or
      a long-form target;
    - map each candidate kind to the Playwright call in the table of
      step-format.md, honouring `exact`, `nth` and the configured
      `testIdAttribute`;
    - resolve `frame` and `within` chains from the outside in, each level by
      the same rule;
    - interpolate `${…}` in every candidate before each attempt;
    - choose the first candidate that matches exactly one element; skip
      candidates that match none or several;
    - for the first `fallbackGrace` of a call, try only the first candidate;
    - poll at least every 100 ms until the step's deadline, and stop at once
      when the signal is aborted;
    - when time runs out, throw `TargetNotFound` listing every candidate at
      every level with its last match count.
13. **Reports.** Every call is recorded as a `LocatorUse`, nested for `frame`
    and `within`, with the candidate after interpolation. Whenever a candidate
    other than the first is used, at any level, emit a `LocatorFallback`
    warning with `data: { target, candidateIndex }`. In this branch, hand both
    to a small interface the runner will implement; do not build the runner.
14. **Browser tests.** Tests run against a real headless Chromium with static
    HTML set by `page.setContent`; no web server. Cover at least: each
    candidate kind; `exact` and `nth`; a first candidate that matches two
    elements; fallback after the grace period, with the warning; no fallback
    while the first candidate appears within the grace period (the page adds
    the real element after a delay, and a placeholder matches the fallback at
    once); `frame` nested twice; `within` with interpolation; cancellation
    during a wait ending within 200 ms; `TargetNotFound` with counts.
15. **Commands and CI.** `pnpm verify` stays free of any browser. A new
    `pnpm test:integration` runs the browser tests. Add a CI job `integration`
    that installs Chromium and runs it on Linux, Windows and macOS. Cache the
    browser download between runs.
16. **Plan.** Update `docs/milestones/v0.1.0-plan.md`: the integration CI job
    is now part of branch 6, and branch 7 keeps the demo web server and the
    harness.

### Part E: the demo app (`test/demo-app-harness`, plan branch 7)

17. **The demo web server.** `examples/demo-app/server/`: a Node web server
    with no framework and no dependency, serving the pages that the samples of
    plan branch 8 need (S10, S11, S12, S14, F1, F2, F3 and F7 in the definition
    of done), and a reset endpoint that tests use to prove clean-up ran. Later
    branches add the pages their own samples need. Each page is plain HTML with
    stable roles, labels and test IDs. Document the pages in
    `examples/demo-app/README.md`.
18. **The harness.** A test helper that starts the demo server on a free port,
    starts the engine as a child process (reuse the existing helper), opens the
    demo project with the server's URL as the environment's base URL, and
    cleans both up, also when a test fails. Say in the report how the base URL
    reaches the engine, since the config file names a fixed port.
19. **Tests.** An integration test that starts the server through the harness
    and loads each page in Chromium, checking that the elements the samples
    will use can be found with `ctx.locate`. It runs under
    `pnpm test:integration` and in the `integration` CI job.

## Done when

- [ ] `pnpm verify` passes on every branch, with no browser installed.
- [ ] `pnpm test:integration` passes on `feat/locators` and
      `test/demo-app-harness`.
- [ ] Every pull request is open, and CI is green on Linux, Windows and macOS
      for each, including the new `integration` job from `feat/locators` on.
      Wait for CI and put the results in the report.
- [ ] Each finding of review 0004 (1 to 7) has a test that fails without its
      fix. The report names the test for each.
- [ ] The masking property test of task 6 passes, and the four named cases
      pass through a real engine process.
- [ ] Committed JSON Schema files match the generator.
- [ ] File header comments and TSDoc on every export, no `any`, and every lint
      or type suppression listed in the report with its reason.
- [ ] The only npm dependency added is `playwright` (engine), at an exact
      version.
- [ ] No client package imports engine code.
- [ ] `CHANGELOG.md` is updated in each branch.
- [ ] `handoff/reports/0005-masking-loader-locators-demo-app.md` exists on the
      last branch and follows [the template](../templates/report.md).

## Out of scope

- The runner, `startRun`, running any action, building `ctx` as a whole.
- Screenshots, traces and the run folder.
- The CLI's `run` command.
- The rule for URL regular expressions (owner decision 3).
- Guarding `process.stdout.write` against user code; it comes with the runner.
- Raising the Node pin.
- Pushing to `main`, merging into `main`, or force-pushing anything.

## Report back

- For each branch: its pull request number, CI result for both jobs, and
  changed-line count.
- For each finding: the test that proves the fix.
- The Playwright version, the size of the Chromium download, and how long the
  `integration` job takes on each system with a cold and a warm cache.
- How the protected fields of task 5 are derived or kept, and what happens when
  someone adds a protocol field and forgets them.
- Every place where a document under `docs/` was unclear, wrong or silent and
  you had to choose. For `ctx.locate` in particular: whether "matches" counts
  hidden elements, and what you did.
- Anything in this instruction you think is wrong.
