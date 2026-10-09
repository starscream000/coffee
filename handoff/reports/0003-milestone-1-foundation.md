# Report 0003: Milestone 1 foundation

- Date: 2026-10-09
- Written by: implementer
- Instruction: [0003](../instructions/0003-milestone-1-foundation.md)

## Summary

All sixteen tasks are done on five stacked branches (#5 to #9): the approvals,
the protocol types, the engine over stdio, and the step-file validator split
in two. Checks **I1**, the oversized-line half of **I3**, **F4**, **F5** and **F6**
pass through the real protocol, and CI is green on Linux, Windows and macOS
for every pull request. `feat/stepfile` was split as the instruction allows,
because it passed 1,500 lines. The most important part of this report is the
list of places where `docs/` was unclear or silent ("Decisions I made").

## Branches and pull requests

All stacked; merge in order #5, #6, #7, #8, #9 (after #3 and #4 from
instruction 0002). Line counts leave out fixtures, generated schema files and
the lockfile.

| Branch                       | Last commit                                   | Pull request                                         | Pushed | CI (Linux, Windows, macOS)                     | Changed lines (of which tests) |
| ---------------------------- | --------------------------------------------- | ---------------------------------------------------- | ------ | ---------------------------------------------- | ------------------------------ |
| `docs/milestone-1-approvals` | `3b62d3b`                                     | [#5](https://github.com/starscream000/coffee/pull/5) | yes    | pass, pass, pass                               | +187 −57 (docs only)           |
| `feat/protocol-types`        | `08561a5`                                     | [#6](https://github.com/starscream000/coffee/pull/6) | yes    | pass, pass, pass                               | +1,211 −28 (≈ 270)             |
| `feat/engine-rpc`            | `215d277`                                     | [#7](https://github.com/starscream000/coffee/pull/7) | yes    | pass, pass, pass                               | +1,565 −29 (≈ 760)             |
| `feat/stepfile-schemas`      | `117c542`                                     | [#8](https://github.com/starscream000/coffee/pull/8) | yes    | pass, pass, pass                               | +2,317 −1 (522)                |
| `feat/stepfile-validate`     | the commit that adds this report (branch tip) | [#9](https://github.com/starscream000/coffee/pull/9) | yes    | pass, pass, pass (before this report's commit) | +1,587 −93 (487)               |

Prettier's line wrapping inflates the counts; `feat/stepfile-schemas` is the
largest because the 25 action specs and the file schemas are long but
repetitive. If it is still too much for one sitting, its two commits
(schemas and specs; parsing and normalisation) can be reviewed separately.

## Tasks

| Task | State | Notes                                                                                                                                                                    |
| ---- | ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1    | done  | ADR 0018 Accepted; plan and definition of done "Approved by the owner"; Q14 and Q35 moved to review decisions (14, 35) with 36–39 added for the approvals and R1–R3.     |
| 2    | done  | ADR 0020 (R1) and ADR 0021 (R2), status "Accepted (reviewer for the owner, 2026-10-09)"; protocol.md, actions.md and architecture.md describe them.                      |
| 3    | done  | Plan rows 1, 3 and 4 updated for R1–R3; CLAUDE.md "How to work" step 2 reworded, nothing else in CLAUDE.md changed.                                                      |
| 4    | done  | Every message of protocol.md as a Zod schema with inferred types under the document's names; error codes as one table, tested for repeats.                               |
| 5    | done  | `PROTOCOL_VERSION = '0.1.0'`, `MAX_MESSAGE_BYTES`, `TRUNCATED_FIELD_BYTES` (64 KiB), `isCompatibleProtocol`.                                                             |
| 6    | done  | `pnpm generate:schemas` writes 44 files to `packages/protocol/schema/`; tests: committed = generated, 16 protocol.md examples validate, compatibility table of 16 cases. |
| 7    | done  | Reader (split/merged chunks, split multi-byte characters, `\r\n`), writer with one masking hook, size rules both ways.                                                   |
| 8    | done  | Handshake and dispatch as specified; list of "method not found" requests below.                                                                                          |
| 9    | done  | `console.*` to stderr; exit 0 on stdin close or `shutdown`, 1 on internal error, 3 after a refused handshake.                                                            |
| 10   | done  | Unit tests for reader, writer, console, session; protocol tests spawn `dist/main.js` and prove I1, the oversized-line half of I3, and silence before the first request.  |
| 11   | done  | `ActionSpec`, `target()`, specs of all 25 built-ins; a test pins the list of names.                                                                                      |
| 12   | done  | `yaml` parsing with positions; schemas for test, flow, targets and config files; long-form normalisation; diagnostics with messages and "did you mean".                  |
| 13   | done  | All cross-file checks listed in the task.                                                                                                                                |
| 14   | done  | `project` module and `openProject`; a comment in `Project.open` marks where user actions are loaded in plan branch 4.                                                    |
| 15   | done  | `validate` for files and `content`; `ProjectNotOpen`; forward-slash, project-relative paths.                                                                             |
| 16   | done  | `examples/demo-app/cfe.config.yaml` and the three fixtures with `# expect:` comments; F4, F5, F6 proven through the protocol. Split into two branches as allowed.        |

### Requests that still answer "method not found"

`listTests`, `listActions`, `startRun`, `cancelRun`, `openSnapshot`.
(`openProject` and `validate` arrived in `feat/stepfile-validate`.)

### Which branch should deliver `listTests`

Plan branch 9, `feat/runner-rows-results`. `listTests` must report `rows`, which
needs reading CSV and YAML data files, and that branch is where data rows are
loaded. Everything else it needs (finding test files, reading names and tags)
already exists in `Project`.

## Checks

| Command                                                             | Result                                                                                                      |
| ------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `pnpm verify` on every branch (Windows, Node 24.11.0)               | passed; on the last branch: type check, lint, Prettier, 260 tests in 21 files                               |
| CI on #5, #6, #7, #8, #9                                            | `verify` passed on ubuntu-latest, windows-latest and macos-latest for each (#9 before this report's commit) |
| `git grep` for `eslint-disable`, `@ts-expect-error`, `@ts-ignore`   | none                                                                                                        |
| `git grep` for `any` types                                          | none                                                                                                        |
| Every new source file starts with a comment; every export has TSDoc | yes (the TSDoc rule enforces presence; I checked file headers with a script)                                |
| Dependencies added                                                  | `zod` 4.6.5 (protocol and engine), `yaml` 2.9.1 (engine). Nothing else.                                     |

## Departures from the instruction

1. **`feat/stepfile` is two branches**, `feat/stepfile-schemas` (tasks 11, 12)
   and `feat/stepfile-validate` (tasks 13–16), as the instruction allows.
2. **`docs/milestone-1-approvals` has one commit, not two.** An earlier
   `git add -A` had staged everything, so the second commit was empty. The
   single commit's message mentions only tasks 1 and 2; it also holds task 3.
   I did not rewrite the pushed branch.
3. **A small protocol change on `feat/engine-rpc`:** the `RequestParams<M>` and
   `RequestResult<M>` helper types, so the engine types its handlers without
   depending on `zod` itself in that branch.
4. **The lockfile's `vite` entry changed** in `feat/stepfile-schemas`: Vite has an
   optional peer dependency on `yaml`, and pnpm now records it as resolved to
   the same `yaml` 2.9.1. No package was added besides `yaml` and `zod`.
5. **Other small additions:** the engine package exports `./main` (ADR 0009
   needs it later); `src/testing/` holds the child-process helper and is
   excluded from the build; `examples/demo-app/fixtures/` is excluded from
   Prettier because one fixture is broken YAML on purpose; a root script
   `generate:schemas`.

## Decisions I made

Places where `docs/` was unclear, wrong or silent, and what I chose. These are
in the code now; each is easy to reverse.

1. **`*.targets.yaml` has no documented layout.** I chose
   `{ version: 1, targets: { <name>: <target> } }`, like the `targets:` key of a
   test. step-format.md should say so.
2. **`call` paths are relative to the project root.** step-format.md says data
   paths are relative to the test file but says nothing about flow paths; its
   example (`flows/checkout-as-guest.flow.yaml` from `tests/checkout/`) only
   works from the root, so I followed the example.
3. **`${env.X}` reads environment `values` directly** (`${env.apiUrl}`, as in the
   example), not `${env.values.apiUrl}`. A value must exist in every
   environment; otherwise the diagnostic names the environments that lack it.
4. **`row` is not available in flows.** The doc says "tests with data only".
   A flow that needs row values gets them as parameters. Worth confirming,
   because people will try `${row.x}` in flows.
5. **Page names are not checked inside flows**, because a flow does not know
   its caller's pages. They will be checked at run time.
6. **A declared secret without a value is not yet reported.** step-format.md
   says it fails validation, but reading the process environment and `.env`
   belongs to the secrets work of plan branch 5.
7. **`LocatorFallback` cannot carry the target and index** that protocol.md
   says it does: the `log` event has no fields for them. Options: put them in
   the message only, or add an optional `data` object to `log` (a compatible
   change). Not needed until plan branch 6; the reviewer should decide.
8. **Truncation:** the 64 KiB limit is in UTF-8 bytes; the `N` in
   `[truncated N characters]` counts code points. protocol.md mixes "KiB" and
   "characters".
9. **`runFinished.status`** is not listed in protocol.md; I used `passed`,
   `failed`, `cancelled`.
10. **`openProject.defaultEnvironment`** is optional in the schema; when the
    config has no `defaults.environment`, the engine reports the first
    environment.
11. **`capabilities.browsers` is `[]`** until a runner can launch a browser,
    although protocol.md's example shows `["chromium"]`. Reporting a browser
    the engine cannot run seemed wrong.
12. **Starting the engine without `--stdio`** exits with 1 and a message;
    protocol.md lists only 0, 1 and 3.
13. **Notifications from the client** (messages without `id`) are ignored;
    protocol.md does not say.
14. **Default globs** when the config leaves out `tests`, `flows` or `targets`:
    `**/*.test.yaml`, `**/*.flow.yaml`, `**/*.targets.yaml`; `node_modules` and
    `.cfe` are always skipped. The docs are silent.
15. **Duplicate keys in YAML** are reported as `YamlSyntax` errors.
16. **`- click: { candidates: […] }`** is read as the long form of the step's
    parameters (so `candidates` is an unknown key); inline targets go under
    `target:`. The docs' examples agree, but do not say it.
17. **Shared targets are read when the project is opened.** `validate` re-reads
    tests and flows every time, but a changed `*.targets.yaml` needs a new
    `openProject`. Fine for the CLI; the desktop app will want a reload.
18. **A file whose `version` is missing or wrong** gets that one diagnostic and
    nothing else, since the rest of the format may not apply.
19. **Positions:** a problem with a key (unknown key, `FrameWithWithin`) points
    at the key; a problem with a value points at the value; a missing
    parameter points at the action's mapping (or its value, in shorthand).
20. **The F6 fixture** uses a mis-indented list item, because an unterminated
    quote is reported at the end of the file, which is a poor example.

### R1, R2, R3

All three are right, in my view. One consequence of R1 worth knowing: because
the schemas are loose (both sides ignore unknown fields, as protocol.md
requires), a misspelt optional field in a client request (`options.hedaed`)
is silently ignored rather than rejected. That is the documented contract; the
CLI should validate its own options before sending them.

## Questions for the owner

None blocking. Decisions 1, 4 and 7 above are the ones most worth a yes or no.

## Not done, not pushed, not verified

- CI for this report's own commit on #9 had not finished when the report was
  written; the commit only adds this file.
- **Correction to report 0002:** it said CI for its own commit on #4 was still
  running. It finished green on Linux, Windows and macOS.
- Nothing in the out-of-scope list was started.

## Suggestions

- Make step-format.md say decisions 1, 2 and 3 explicitly.
- Run CI on every pushed branch, not only pull requests, so stacked branches
  get results before their pull requests exist.
