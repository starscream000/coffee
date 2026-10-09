# Review 0003: Milestone 1 foundation

- Date: 2026-10-09
- Written by: reviewer
- Instruction: [0003](../instructions/0003-milestone-1-foundation.md)
- Report: `handoff/reports/0003-milestone-1-foundation.md` on the branch
  `feat/stepfile-validate` (it reaches `main` when pull request #9 is merged)
- Verdict: **Approved**, with fixes required in instruction 0004 before plan
  branch 4 starts

## Pull requests

| Pull request                                         | Branch                       | Reviewed at | CI                                | Verdict  |
| ---------------------------------------------------- | ---------------------------- | ----------- | --------------------------------- | -------- |
| [#5](https://github.com/starscream000/coffee/pull/5) | `docs/milestone-1-approvals` | `3b62d3b`   | green on Linux, Windows and macOS | Approved |
| [#6](https://github.com/starscream000/coffee/pull/6) | `feat/protocol-types`        | `08561a5`   | green on Linux, Windows and macOS | Approved |
| [#7](https://github.com/starscream000/coffee/pull/7) | `feat/engine-rpc`            | `215d277`   | green on Linux, Windows and macOS | Approved |
| [#8](https://github.com/starscream000/coffee/pull/8) | `feat/stepfile-schemas`      | `117c542`   | green on Linux, Windows and macOS | Approved |
| [#9](https://github.com/starscream000/coffee/pull/9) | `feat/stepfile-validate`     | `88e106c`   | green on Linux, Windows and macOS | Approved |

Merge in order, after #3 and #4 of instruction 0002.

## What I checked

- **Every source file, line by line**: the protocol package, the `rpc`,
  `actions`, `schema`, `stepfile` and `project` modules. Roughly 4,800 lines of
  source. I read the two protocol test files that prove I1, I3 and F4 to F6 in
  full. I did not read the other test files; I relied on them passing.
- **`pnpm verify` from a clean build** on the top of the stack (`88e106c`), on
  Linux with Node 24.11.0: type check, lint, format check, 260 tests in 21
  files, all passing.
- **CI** on all five pull requests, on all three systems.
- **The engine, by hand**, as a client would use it:
  - the handshake, a refused version (exit code 3), a request before
    `initialize`, invalid JSON, a JSON array, a line ending in `\r\n`, a line
    starting with a byte-order mark;
  - a 6 MiB line: answered with `MessageTooLarge`, and the next request still
    worked;
  - the three fixtures: F4, F5 and F6 return exactly the expected diagnostics,
    with clear messages;
  - 400 test files validated in one request in about 0.4 seconds;
  - a project of my own with awkward input (see findings 3 to 6).
- **The report's claims**: no `any`, no `eslint-disable`, no `@ts-expect-error`;
  every source file starts with a comment; the only dependencies added are
  `zod` and `yaml`; no client imports engine code. All true.
- **The 25 built-in specs** against `docs/actions.md`, parameter by parameter.
- **The stack**: each branch sits on the one before it.

## What I could not check

- Behaviour on Windows and macOS beyond CI. I ran everything on Linux.
- `pnpm verify` on each lower branch separately. I ran it on the top of the
  stack and relied on CI for the others.

## Findings

The work is good: careful, well tested, and honest about what it chose where
the documents were silent. None of the findings below can be triggered by
anything on `main` after these merges, which is why the verdict is Approved.
Findings 1 and 2 must be fixed before user code runs in the engine (plan
branch 4).

1. **Must fix before plan branch 4. `console.dir` and `console.dirxml` still
   write to stdout.** `packages/engine/src/rpc/console.ts` replaces six console
   methods. After `redirectConsoleToStderr()`, `console.dir({a: 1})` and
   `console.dirxml(...)` still print on stdout, which puts a line that is not
   JSON into the protocol stream. I ran it and saw both. `table`, `group`,
   `count`, `time` and `assert` were fine. Fix: replace the global console with
   one built on stderr (`new console.Console(process.stderr, process.stderr)`)
   instead of patching methods one by one, and test every console method.
2. **Should fix. The engine can exit before it has answered.** In
   `packages/engine/src/rpc/stdio-server.ts`, when stdin ends and no partial
   line is left, the code waits on `Promise.resolve()` and exits at once. It
   does not wait for requests still in the session's queue. Today's handlers
   do all their work synchronously, so I could not make it lose a response
   (25 runs, none lost). It will lose responses as soon as a handler waits for
   anything, which plan branch 4 does. Fix: on stdin end, wait for the session's
   queue to drain, then exit. Add a test with a deliberately slow handler.
3. **Should fix. `validate` on a folder is an internal error.**
   `{"files": ["tests"]}` or `{"files": [".."]}` answers `InternalError`, with
   `EISDIR` on stderr (`project.ts`, where it reads the file). It should be a
   diagnostic like the one for a missing file.
4. **Should fix. An unclosed `${` is silently accepted.**
   `value: '${vars.email'` produces no diagnostic and would be sent as literal
   text. Report it, pointing at the value.
5. **Should fix. Regular expressions are not checked.** `matches: '('`, a URL
   pattern written as `/[/`, and `extract` with a `pattern` of two groups (the
   document says one) all pass validation and would fail only at run time.
   Check that each compiles, and that `extract`'s pattern has exactly one
   group.
6. **Should fix. Three messages a tester could not act on.**
   - `timeout: 10` says `"timeout" must be text.` It should say it must be a
     duration such as `10s`.
   - `validate` with `{}` says `(params): Invalid input.` It should say it
     needs `files` or `content`.
   - `click: { candidates: [...] }` says `candidates` is not a key of `click`.
     Add the hint that an inline target goes under `target:`.
7. **Should fix. Shared targets go stale.** `validate` re-reads tests and flows
   each time but reads `*.targets.yaml` only at `openProject` (the report's
   decision 17). Re-read them on each `validate` too; it is cheap, and it
   removes a class of confusing results for any client that stays open.
8. **Should fix. An environment value may not be named `name` or `baseUrl`.**
   `${env.X}` reads `values` directly (decision 3, accepted below), so a value
   called `name` or `baseUrl` would be hidden by the built-in ones. Reject both
   names under `values:` in the config.
9. **Note. Flows are cross-checked once per call, not once per file.** A flow
   called from many steps, or flows that call each other in a diamond, repeat
   the work. Results are correct because duplicates are removed. Cache the
   cross-check per flow within one `validate` when it becomes measurable.
10. **Note. `${row.x}` is not checked against the data's columns.** Not
    required by the documents. Worth adding in the branch that loads data rows
    (plan branch 9), since a misspelt column is an easy mistake.
11. **Note. One commit on `docs/milestone-1-approvals` holds three tasks** but
    its message names two. The report says so. Not worth rewriting.

## Rulings on the report's "Decisions I made"

The reviewer rules on these for the owner; the owner can reverse any of them.
Each accepted decision must be written into the named document in instruction
0004, because the documents are the specification.

| No. | Decision                                                     | Ruling                                                                                          |
| --- | ------------------------------------------------------------ | ----------------------------------------------------------------------------------------------- |
| 1   | `*.targets.yaml` is `{ version, targets: { name: target } }` | Accepted. Document in step-format.md.                                                           |
| 2   | `call` paths are relative to the project root                | Accepted. Document, next to the rule that data paths are relative to the test file.             |
| 3   | `${env.X}` reads environment `values` directly               | Accepted, with finding 8. Document.                                                             |
| 4   | `row` is not available in flows                              | Accepted: a flow gets row values as parameters. **Owner: say if you want otherwise.** Document. |
| 5   | Page names are not checked inside flows                      | Accepted. They are checked at run time. Document.                                               |
| 6   | A declared secret without a value is not reported yet        | Accepted. It arrives with secrets (plan branch 5).                                              |
| 7   | `LocatorFallback` cannot carry the target and index          | Add an optional `data` object to the `log` event (a compatible change) and put them there.      |
| 8   | Truncation limit in bytes, removed count in code points      | Accepted. Make protocol.md say exactly that.                                                    |
| 9   | `runFinished.status` is `passed`, `failed` or `cancelled`    | Accepted. Document.                                                                             |
| 10  | `defaultEnvironment` falls back to the first environment     | Accepted. Document.                                                                             |
| 11  | `capabilities.browsers` is `[]` until a runner exists        | Accepted. Reporting a browser the engine cannot run would be wrong.                             |
| 12  | Without `--stdio` the engine exits with 1                    | Accepted. Document.                                                                             |
| 13  | Notifications from the client are ignored                    | Accepted. Document.                                                                             |
| 14  | Default globs when the config names none                     | Accepted. Document.                                                                             |
| 15  | Duplicate YAML keys are `YamlSyntax` errors                  | Accepted.                                                                                       |
| 16  | A mapping after an action is the long form of its parameters | Accepted, with the hint in finding 6. Document.                                                 |
| 17  | Shared targets are read at `openProject` only                | Not accepted. See finding 7.                                                                    |
| 18  | A missing or wrong `version` gives one diagnostic            | Accepted.                                                                                       |
| 19  | Where each kind of diagnostic points                         | Accepted. Document in step-format.md, "Validation errors".                                      |
| 20  | The F6 fixture uses a mis-indented list item                 | Accepted.                                                                                       |

R1, R2 and R3 stand. The report's note on R1 is right: an unknown optional
field in a request is ignored, as the protocol requires, so the CLI must check
its own options before sending them. That goes into the CLI branch.

The report's answer on `listTests` (plan branch 9, because it must count data
rows) is accepted.

## Done-when checks

| Check                                              | Result                                            |
| -------------------------------------------------- | ------------------------------------------------- |
| `pnpm verify` passes on every branch               | Yes (top of stack by reviewer; each branch by CI) |
| Every pull request open, CI green on three systems | Yes                                               |
| I1 passes; F4, F5, F6 pass through `validate`      | Yes, by test and by hand                          |
| Committed JSON Schema files match the generator    | Yes (test)                                        |
| File header comments and TSDoc on exports          | Yes                                               |
| No `any`; no lint or type suppressions             | Yes, none                                         |
| Only `zod` and `yaml` added                        | Yes (lockfile compared with `main`)               |
| No client imports engine code                      | Yes                                               |
| `CHANGELOG.md` updated in each branch              | Yes                                               |
| The report exists and follows the template         | Yes                                               |

## Owner decisions needed

1. **`row` in flows** (ruling 4). Recommended: keep it unavailable; flows take
   values as parameters.
2. Nothing else. The remaining rulings are technical and the reviewer has made
   them.

## For the next instruction

Instruction 0004, after #3 to #9 are merged:

- A `fix/` branch for findings 1 to 8, with a test for each.
- A `docs/` branch that writes every accepted ruling into its document, and
  ruling 7 into the protocol (schema, generated files and protocol.md).
- Then plan branch 4 (`feat/actions-sdk`) and onward.
