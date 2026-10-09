# Instruction 0004: review fixes, user actions, variables and secrets

- Date: 2026-10-09
- Written by: reviewer
- Based on `main` at: `a812a3e` or later
- Replaces: none
- Follows review: [0003](../reviews/0003-milestone-1-foundation.md)

## Goal

The eight findings of review 0003 are fixed and tested, the accepted rulings
are written into the documents, and plan branches 4 and 5 exist: user actions
load into the engine, and variables, environments and secret masking work.
Checks F9, I12 and I13 of the definition of done pass.

## Owner decisions

1. Pull requests #3 to #9 are merged. Milestone 0 is finished, and the first
   three plan branches are on `main`.
2. The owner chose to merge first and fix afterwards. The fix branch below
   comes before any new feature work.
3. `${row.…}` stays unavailable inside flows (ruling 4 of review 0003). The
   owner was asked and did not object.
4. The other rulings in review 0003 stand as written there.

## Branches

| Branch                     | Based on                   | Plan branch | What it holds          |
| -------------------------- | -------------------------- | ----------- | ---------------------- |
| `fix/review-0003-findings` | `main`                     | none        | Tasks 2 to 9           |
| `docs/review-0003-rulings` | `fix/review-0003-findings` | none        | Tasks 10 and 11        |
| `feat/actions-sdk`         | `docs/review-0003-rulings` | 4           | Tasks 12 to 17         |
| `feat/context-secrets`     | `feat/actions-sdk`         | 5           | Tasks 18 to 23, report |

Open one pull request per branch, all into `main`, and say in each description
which branch it sits on. If a `feat/` branch passes roughly 1,500 changed lines
(not counting fixtures, generated files and the lockfile), split it in two
stacked branches and say so in the report.

`docs/` is the specification. Where this instruction and a document under
`docs/` disagree, stop and say so in the report.

## Tasks

### Part A: housekeeping (no files change)

1. `git switch main` and `git pull --ff-only`. Delete the local branches that
   are now contained in `main`.

### Part B: fixes (`fix/review-0003-findings`)

Each fix comes with a test that fails without it. The finding numbers are those
of review 0003.

2. **Finding 1.** Nothing but protocol messages may reach stdout, whatever
   console method is called. Replace the global console with one built on
   stderr instead of patching methods one by one. Test every method of
   `console`, including `dir`, `dirxml`, `table`, `group`, `count`, `time`,
   `timeEnd`, `timeLog`, `trace` and `assert`, by starting the engine as a
   child process and checking stdout.
3. **Finding 2.** When stdin ends, the engine waits until every request already
   received has been answered, then exits with code 0. Test it with a handler
   that takes time (a test-only hook is fine), closing stdin straight after
   sending the request.
4. **Finding 3.** `validate` on a path that is a folder returns a diagnostic,
   like a missing file does. No `InternalError`. Cover `"tests"` and `".."`.
5. **Finding 4.** An unclosed `${` in any value is a diagnostic that points at
   the value.
6. **Finding 5.** Validation checks that every regular expression compiles:
   `matches`, `pattern`, and URL patterns written as `/regex/`. `extract`'s
   `pattern` must have exactly one capturing group. Skip the check for a value
   that contains `${…}`, and say in a comment that it is then checked at run
   time.
7. **Finding 6.** Three messages:
   - a duration given as a number (`timeout: 10`) says it must be a duration
     such as `10s`;
   - `validate` with neither `files` nor `content` says it needs one of them;
   - `click: { candidates: […] }`, and the same for any action with a target
     parameter, adds the hint that an inline target goes under `target:`.
8. **Finding 7.** `validate` re-reads the shared `*.targets.yaml` files on
   every call, as it already does for flows.
9. **Finding 8.** An environment's `values` may not contain `name` or
   `baseUrl`; the config diagnostic says they are built in.

### Part C: write the rulings down (`docs/review-0003-rulings`)

10. For every ruling marked "Accepted" or "Document" in the table of review
    0003, make the named document say it. Where the review names no document,
    use `docs/step-format.md` for the step-file format and `docs/protocol.md`
    for the protocol. Add the new diagnostics from Part B to the documents that
    list diagnostics.
11. **Ruling 7.** Add an optional `data` object to the `log` event in the
    protocol schema, regenerate the JSON Schema files, and update
    `docs/protocol.md`: `LocatorFallback` carries the target name and the
    candidate index in `data`. This is a compatible change, so
    `PROTOCOL_VERSION` stays `0.1.0`.

### Part D: user actions (`feat/actions-sdk`, plan branch 4)

The specification is `docs/actions.md`, ADR 0008, ADR 0016 and ADR 0021.

12. **`defineAction` and the SDK entry point.** `@cfe/engine/sdk` exports
    `defineAction` (an `ActionSpec` plus `run`), `target()`, `z`, the errors
    `ActionError` and `AssertionError` carrying the `sdkError` tag, and the
    TypeScript type of `ctx` exactly as `docs/actions.md` describes it. Only
    the type of `ctx` exists in this branch; the object is built by later
    branches.
13. **The registry.** One registry holds the built-in specs and the user's
    actions. It enforces the name rules of ADR 0016 through one
    `RESERVED_NAMESPACES` list, and reports duplicates. The diagnostics are
    `ActionNameNotNamespaced`, `ActionNamespaceReserved` and one for a
    duplicate name, each with the action file and line, and the messages the
    documents show.
14. **Loading.** Follow ADR 0008: find files by the config's `actions` globs,
    bundle each with esbuild into `.cfe/cache/actions/` with a source map,
    cache by content hash, and import the result. Every import of
    `@cfe/engine/sdk` resolves to the running engine's own copy. (ADR 0008 also
    redirects `playwright`; add that in the branch that adds Playwright, and
    leave a comment where it goes.) A file that fails to compile, load or pass
    the name rules becomes a diagnostic with file and line; other files still
    load.
15. **`openProject` and `validate`.** `openProject` loads the user's actions and
    returns their diagnostics, including the `SdkVersionMismatch` warning with
    both versions. `validate` then knows user actions: a step that calls one is
    checked against its parameter schema, and a step that calls an action that
    failed to load gets a diagnostic that says so.
16. **`listActions`.** Returns built-in and user actions as `docs/protocol.md`
    describes, with each parameter schema as JSON Schema.
17. **Fixtures and tests.**
    - `examples/demo-app/actions/demo.ts` with the action `demo.addTodo`, and a
      valid step file that calls it in shorthand and in long form. It only has
      to validate in this branch; running it comes later.
    - `examples/demo-app-bad-actions/` as the definition of done describes it.
    - Protocol tests that prove **F9** through `openProject`, **I12**, and
      **I13**. For I13, the action file calls every console method at load
      time, and stdout must still hold protocol messages only.
    - Unit tests for the registry, the name rules, the cache (an unchanged file
      is not rebuilt; a changed one is), and a file with a syntax error.

### Part E: variables and secrets (`feat/context-secrets`, plan branch 5)

The specification is `docs/step-format.md` ("Variables and interpolation",
"Environments", "Secrets") and ADR 0014.

18. **Variables and interpolation at run time.** A variable store for a test or
    a flow, and a function that resolves `${…}` in any value: a value that is
    exactly one `${…}` keeps its type, anything else becomes text; dotted paths
    into objects and lists; `$${` gives a literal `${`. An unknown variable is
    an error that names the step and lists the variables that exist. The rule
    for unset variables in `after` steps is the runner's; expose what the
    runner needs to apply it.
19. **Environment profiles.** Selecting an environment by name or by the
    config's default, with `name`, `baseUrl` and `values`, and the settings an
    environment may override.
20. **Secrets.** Load each declared secret from the process environment, then
    from a `.env` file at the project root. Reject values shorter than 4
    characters. `validate` now reports a declared secret that has no value, in
    the tests that use it, naming the variable to set (decision 6 of report
    0003).
21. **The registry and `mask`.** Every secret is registered with the forms ADR
    0014 lists. `mask(text)` replaces each form with `•••`. When two registered
    values overlap, the longer one is replaced first. The registry also accepts
    values at run time, for the sensitive header values of a later branch.
22. **Wire `mask` into the writer.** The message writer's hook now masks every
    message. **A secret must not leak in part when a long string is
    truncated:** mask before truncating. Test it with a secret that straddles
    the cut.
23. **Tests.** Unit tests for each rule above, including each encoded form of a
    secret, a secret inside a longer value (`Bearer <secret>`), and the
    4-character rule. One protocol test: with a secret set in the engine's
    environment, a diagnostic or error message that would contain the secret
    reaches the client masked.

## Done when

- [ ] `pnpm verify` passes on every branch.
- [ ] Every pull request is open, and CI is green on Linux, Windows and macOS
      for each. Wait for CI and put the results in the report.
- [ ] Each of the eight findings has a test that fails without its fix. The
      report names the test for each.
- [ ] Every accepted ruling of review 0003 can be found in a document.
- [ ] F9 passes through `openProject`; I12 and I13 pass.
- [ ] The committed JSON Schema files match the generator.
- [ ] File header comments and TSDoc on every export, no `any`, and every lint
      or type suppression listed in the report with its reason.
- [ ] The only npm dependency added is `esbuild` (engine). Playwright is not
      added yet.
- [ ] No client package imports engine code.
- [ ] `CHANGELOG.md` is updated in each branch.
- [ ] `handoff/reports/0004-fixes-actions-and-secrets.md` exists on the last
      branch and follows [the template](../templates/report.md).

## Out of scope

- Running any action: `ctx` as an object, `ctx.locate`, the runner, browsers.
- Playwright, the demo web server, the integration CI job.
- Masking of screenshots, traces and run-folder files (they arrive with the
  branches that write those files).
- The CLI's `run` command.
- Raising the Node pin.
- Pushing to `main`, merging into `main`, or force-pushing anything.

## Report back

- For each branch: its pull request number, CI result, and changed-line count.
- For each finding: the test that proves the fix.
- Every place where a document under `docs/` was unclear, wrong or silent and
  you had to choose.
- How long `openProject` takes on the demo app with a cold cache and with a
  warm cache, on your machine.
- Plan branch 6 (`feat/locators`) needs Chromium in CI, but the CI job that
  installs it is plan branch 7. Say how you would order or combine the two.
- Anything in this instruction you think is wrong.
