# Instruction 0003: Milestone 1 foundation

- Date: 2026-10-09
- Written by: reviewer
- Based on: the last branch of instruction 0002 (`chore/apply-product-names`)
- Replaces: none
- Follows review: none. It is open at the same time as
  [instruction 0002](0002-review-fixes-and-names.md); do 0002 first.

## Goal

The first three code branches of the v0.1.0 plan exist: the protocol's types
and schemas, an engine that speaks the protocol over stdio and does the version
handshake, and a step-file parser and validator reachable through the
`validate` request. Checks I1, F4, F5 and F6 of the definition of done pass.

## Owner decisions

1. **The Milestone 1 plan is approved.** Coding may start.
2. **ADR 0018 (saved-login cache) is approved.**
3. The owner asked for this instruction to be open together with 0002, so the
   two are carried out one after the other without a review in between. See
   "Several open instructions" in [handoff/README.md](../README.md). **If
   instruction 0002 cannot be finished, stop there and do not start this one.**
4. The owner asked the reviewer to write the coding instructions. Where the
   approved plan left a choice open, the reviewer has decided it below. The
   owner can reverse any of these at review time.

### Reviewer decisions that fill gaps in the plan

- **R1. The protocol is defined once, as Zod schemas.** The plan's branch 1
  generates JSON Schemas but names no way to do it. Decision: every message
  (request parameters, results, events, shared types) is a Zod schema in the
  protocol package. The TypeScript types are inferred from the schemas, and the
  JSON Schema files are generated from the same schemas with Zod's
  `toJSONSchema`. One source, so the three cannot drift, and the engine can
  check incoming parameters with the same schemas. `zod` becomes the protocol
  package's one npm dependency. The dependency rule "`protocol` depends on
  nothing" means no other workspace package.
- **R2. An action's description is separate from its code.** The validator
  needs every built-in action's name, shorthand and parameter schema, but the
  plan delivers the actions themselves in later branches. Decision: an
  `ActionSpec` holds the name, description, shorthand and parameter schema. A
  runnable action is an `ActionSpec` plus `run`. This instruction defines the
  specs of all built-in actions from `docs/actions.md`; each `run` arrives in
  its own later branch, and `defineAction` (plan branch 4) builds on
  `ActionSpec`.
- **R3. `validate` needs an open project.** The plan puts `validate` in branch
  3 and `openProject` in branch 4, but cross-file checks need the config, the
  shared targets and the flows. Decision: branch 3 includes finding the
  project's files and the `openProject` request, without loading user actions.
  Branch 4 adds user-action loading to it.

## Branches

| Branch                       | Based on                     | Plan branch | What it holds          |
| ---------------------------- | ---------------------------- | ----------- | ---------------------- |
| `docs/milestone-1-approvals` | `chore/apply-product-names`  | none        | Tasks 1 to 3           |
| `feat/protocol-types`        | `docs/milestone-1-approvals` | 1           | Tasks 4 to 6           |
| `feat/engine-rpc`            | `feat/protocol-types`        | 2           | Tasks 7 to 10          |
| `feat/stepfile`              | `feat/engine-rpc`            | 3           | Tasks 11 to 16, report |

Open one pull request per branch, all into `main`. Each will also show the
commits of the branches below it until those are merged; say so in each
description, and name the branch it sits on.

`docs/` is the specification. Where this instruction and a document under
`docs/` disagree, stop and say so in the report; do not pick one silently.

## Tasks

### Part A: record the approvals (`docs/milestone-1-approvals`)

1. Set ADR 0018 to `Accepted (owner, 2026-10-09)`. Change the status lines of
   the plan and the definition of done from draft to approved by the owner on
   2026-10-09. In `docs/architecture.md`, move the names and ADR 0018 from
   "Open questions" to "Review decisions".
2. Write two ADRs with status `Accepted (reviewer for the owner, 2026-10-09)`:
   ADR 0020 for R1 and ADR 0021 for R2. Use the template, including the
   alternatives rejected and "Revisit when". Update `docs/protocol.md`,
   `docs/actions.md` and `docs/architecture.md` where they describe these.
3. Update `docs/milestones/v0.1.0-plan.md` for R1, R2 and R3: which branch adds
   `zod`, where the built-in specs and `openProject` land, and what branch 4
   still delivers. In `CLAUDE.md`, change only the wording of "How to work"
   step 2 so it allows `STATUS.md` to list several open instructions in order.

### Part B: protocol types (`feat/protocol-types`)

4. In `packages/protocol/src`, define as Zod schemas, with inferred types
   exported under the names `docs/protocol.md` uses:
   - the JSON-RPC envelopes (request, success response, error response,
     notification);
   - the parameters and result of every request in `docs/protocol.md`;
   - every event and its fields;
   - the shared types (`Location`, `Diagnostic`, `LocatorUse`,
     `SnapshotStatus`, `ErrorInfo` and any other the document defines);
   - the error codes and names as one table, with a check that no code or name
     repeats.
5. Export `PROTOCOL_VERSION` (`0.1.0`), `MAX_MESSAGE_BYTES`, the 64 KiB
   truncation limit, and a function that says whether a client version and an
   engine version are compatible, following the rule in `docs/protocol.md`
   (0.x: major and minor equal; from 1.0.0: major equal).
6. Add a script that generates one JSON Schema file per message into
   `packages/protocol/schema/`, and commit the generated files. Unit tests:
   - the committed schema files equal what the script generates now, so they
     cannot go stale;
   - every example message printed in `docs/protocol.md` validates against its
     schema (keep the examples as test fixtures);
   - the compatibility function, as a table of cases including the edges.

### Part C: engine over stdio (`feat/engine-rpc`)

7. The engine entry point `dist/main.js --stdio`, and the `rpc` module:
   - a reader that turns stdin bytes into messages: one JSON object per line,
     UTF-8, split and merged chunks handled, a multi-byte character split across
     chunks handled, a line ending in `\r\n` accepted;
   - a writer that sends one line per message, and passes every message through
     one masking hook before writing (the hook does nothing yet; masking
     arrives with secrets);
   - the size rules of ADR 0005 in both directions: truncate, then
     `MessageTooLarge`; an oversized line from the client is discarded without
     parsing and the session stays open.
8. The handshake and dispatch of `docs/protocol.md`: `initialize`, `shutdown`,
   `NotInitialized` for anything before a successful `initialize`, a second
   `initialize` rejected, `IncompatibleProtocol` followed by exit code 3,
   invalid JSON and invalid parameters answered with the standard JSON-RPC
   codes, an unknown method answered with method not found. Requests the
   protocol defines but no branch has implemented yet also answer method not
   found for now; list them in the report.
9. `console.*` in the engine process is redirected to stderr at start-up.
   stdout carries protocol messages only. When stdin closes, the engine exits
   with code 0. An unexpected internal error exits with code 1 and details on
   stderr.
10. Tests:
    - unit tests for the reader and writer, covering every case in task 7;
    - protocol tests that start the built engine as a child process, as a
      client would, and prove check **I1**, the oversized-line half of **I3**,
      and that nothing is written to stdout before the first request.

    These tests need no browser and run in the normal `pnpm verify`.

### Part D: step files (`feat/stepfile`)

11. `ActionSpec` (R2) and the specs of every built-in action in
    `docs/actions.md`: name, one-sentence description, shorthand and parameter
    schema, including the `target()` schema helper. No `run`. A unit test
    checks the list of names against a fixed list, so an action cannot be
    dropped by accident.
12. The `stepfile` and `schema` modules, following ADR 0006:
    - parse with the `yaml` package, keeping the position of every node;
    - Zod schemas for test, flow, targets and config files, covering everything
      in `docs/step-format.md` (including the long form of targets, `frame`,
      `within`, `skip`, `freshLogin`, `maxAge` and every `defaults` setting);
    - each step normalised to its canonical long form;
    - every problem reported as a `Diagnostic` with file, line and column, with
      the messages and "did you mean" hints the document shows. All problems
      in a file are reported at once.
13. The cross-file checks of ADR 0006, pass 3: target names (file-local, then
    shared; duplicates), flow paths and parameters, flow cycles, target
    reference cycles, `frame` together with `within`, page names (`page`,
    `opens`, use before opening), `${…}` namespaces, declared secrets,
    environment values.
14. The `project` module (R3): find the config file from a root folder, read
    it, and find the test, flow and targets files from its globs. The
    `openProject` request, returning what `docs/protocol.md` says, with
    `ProjectInvalid` when there is no config. No user-action loading yet: say
    so in a comment where it will be added.
15. The `validate` request, for files on disk and for an unsaved `content`
    buffer, with `ProjectNotOpen` when no project is open. Paths in
    diagnostics are relative to the project root, with forward slashes on
    every system.
16. Fixtures and tests:
    - `examples/demo-app/` gets its config file and the fixtures
      `fixtures/invalid/schema-errors.test.yaml`, `flow-cycle.test.yaml` and
      `yaml-syntax.test.yaml` from the definition of done, each with
      `# expect:` comments naming the diagnostics it must produce. The demo
      web server itself is not part of this instruction.
    - Unit tests for parsing with positions, each schema, normalisation, each
      cross-file check, and the mapping from a schema problem to a line and
      column.
    - Protocol tests that start the engine, open the project and call
      `validate`, proving **F4**, **F5** and **F6**: the diagnostics returned
      equal the fixture's `# expect:` comments, no more and no fewer.

    If `feat/stepfile` grows beyond what can be reviewed in one sitting
    (roughly 1,500 changed lines, not counting fixtures and generated files),
    split it into `feat/stepfile-schemas` (tasks 11 and 12) and
    `feat/stepfile-validate` (tasks 13 to 16), stacked in that order, and say so
    in the report.

## Done when

- [ ] `pnpm verify` passes on every branch.
- [ ] Every pull request is open, and CI is green on Linux, Windows and macOS
      for each. Wait for CI and put the results in the report.
- [ ] I1 passes, and F4, F5 and F6 pass through `validate`.
- [ ] The committed JSON Schema files match the generator.
- [ ] Every new file starts with a comment saying what it is for, and every
      export has a TSDoc comment, as `CLAUDE.md` requires.
- [ ] No `any`. Every `eslint-disable` and every `@ts-expect-error` is listed in
      the report with its reason.
- [ ] The only npm dependencies added are `zod` (protocol and engine) and `yaml`
      (engine). Playwright and esbuild are not added yet.
- [ ] No client package imports engine code; the lint rule still passes.
- [ ] `CHANGELOG.md` is updated in each branch.
- [ ] `handoff/reports/0003-milestone-1-foundation.md` exists on the last
      branch and follows [the template](../templates/report.md).

## Out of scope

- Loading user actions, `defineAction`, `listActions`, esbuild (plan branch 4).
- Variables, interpolation at run time, secrets and masking logic (plan
  branch 5). Only the masking hook's place in the writer exists.
- Locators, the runner, browsers, page states, the CLI's `run` command.
- The demo web server and the integration CI job (plan branch 7).
- Raising the Node pin.
- Pushing to `main`, merging into `main`, or force-pushing anything.

## Report back

- For each branch: its pull request number, CI result, and the count of changed
  lines not counting fixtures and generated files.
- The requests that still answer method not found (task 8).
- `listTests` is in `docs/protocol.md` but no plan branch names it. Say which
  branch should deliver it.
- Every place where a document under `docs/` was unclear, wrong or silent and
  you had to choose. These matter more than anything else in the report.
- Anything in R1, R2 or R3 you think is the wrong call, with your reason.
