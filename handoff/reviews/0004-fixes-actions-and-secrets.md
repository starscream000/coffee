# Review 0004: review fixes, user actions, variables and secrets

- Date: 2026-10-09
- Written by: reviewer
- Instruction: [0004](../instructions/0004-fixes-actions-and-secrets.md)
- Report: `handoff/reports/0004-fixes-actions-and-secrets.md` on the branch
  `feat/context-secrets` (it reaches `main` when pull request #13 is merged)
- Verdict: **Changes requested** on #13. #10, #11 and #12 are **approved**.

## Pull requests

| Pull request                                           | Branch                     | Reviewed at | CI                                | Verdict           |
| ------------------------------------------------------ | -------------------------- | ----------- | --------------------------------- | ----------------- |
| [#10](https://github.com/starscream000/coffee/pull/10) | `fix/review-0003-findings` | `a502bd7`   | green on Linux, Windows and macOS | Approved          |
| [#11](https://github.com/starscream000/coffee/pull/11) | `docs/review-0003-rulings` | `42bba43`   | green on Linux, Windows and macOS | Approved          |
| [#12](https://github.com/starscream000/coffee/pull/12) | `feat/actions-sdk`         | `9c824b3`   | green on Linux, Windows and macOS | Approved          |
| [#13](https://github.com/starscream000/coffee/pull/13) | `feat/context-secrets`     | `1127c77`   | green on Linux, Windows and macOS | Changes requested |

Merge #10, #11 and #12 in order. #13 stays open until finding 1 is fixed on the
same branch.

## What I checked

- **`pnpm verify` from a clean build** on the top of the stack (`1127c77`), on
  Linux with Node 24.11.0: 310 tests in 28 files, all passing.
- **CI** on all four pull requests, on all three systems.
- **The eight fixes, with my own probes from review 0003**, not only the
  implementer's tests. Every one now behaves as asked: no console method reaches
  stdout (17 methods tried), a folder gives `NotAFile`, an unclosed `${` and bad
  regular expressions are reported, the three messages are clear, and reserved
  environment value names are rejected.
- **The new source, line by line**: the loader, the registry, the SDK, the
  handlers, interpolation, variables, environments, secrets, masking, and the
  writer's change. I did not read the new test files; I relied on them passing
  and on my own probes.
- **The rulings in the documents**: I searched for each accepted ruling of
  review 0003 and found each.
- **The loader, by hand**, with a project of my own (findings 2 and 3).
- **Masking, by hand**, with secrets chosen to collide with protocol text
  (finding 1).

## What I could not check

- The `openProject` timings (60 ms cold, 29 ms warm) are from the implementer's
  Windows machine. I did not measure them.
- That each fix's test fails without the fix. The report says the implementer
  ran each against the unfixed code; I confirmed the fixed behaviour only.
- Behaviour on Windows and macOS beyond CI.

## Findings

1. **Must fix, and the reason #13 is not approved. Masking can break the
   engine's own messages.** The writer masks object keys, and then masks the
   whole serialised line as text. A secret that happens to equal protocol text
   therefore damages the message itself. With `examples/demo-app` and the
   secret `DEMO_PASSWORD` set to:
   - `jsonrpc`: every response arrives as `{"•••":"2.0","id":2,…}`;
   - `result`: every response arrives without a `result` key;
   - `line`: every diagnostic loses its `line` field;
   - `2.0","`: every response is **invalid JSON** (`{"jsonrpc":"•••id":2,…`).

   The same pass would replace enum values (`"severity":"error"` with the
   secret `error`), event names and file paths. The protocol is a public
   contract; no secret may make the engine send a message a client cannot read.

   Required: after masking, a message is still valid JSON, still validates
   against its protocol schema, and its identifiers are unchanged. In practice:
   - never mask object keys, and never mask the serialised text;
   - never mask the envelope (`jsonrpc`, `id`, `method`), error codes and names,
     or the fields the protocol defines as identifiers or fixed values (`runId`,
     `testId`, `stepId`, `seq`, `section`, `action`, `page`, `status`, `level`,
     `severity`, `code`, `state`, file and folder paths, environment and login
     names);
   - mask everything that carries free text or user data (`message`, `hint`,
     `expected`, `actual`, `params`, `candidate`, `data`, log text and the
     like).

   Identifiers come from files in the repository, which never hold secret
   values, so leaving them unmasked cannot leak one. Record the rule in ADR 0014. Test it as a property: for every protocol example message, and for
   secrets taken from every key and every string value in that message, the
   masked message still validates against its schema and its identifiers are
   unchanged.

2. **Must fix before anyone writes real user actions. An action that imports a
   CommonJS package fails to load.** `actions/shop.ts` importing a package whose
   code calls `require("path")` gives `ActionLoadError: Dynamic require of
"path" is not supported`. Many npm packages are CommonJS and use Node's
   built-in modules, so this will be the first thing a real project hits. It is
   a known limit of bundling CommonJS into an ES module; give the bundle a
   working `require` (esbuild's `banner` with `createRequire`). Add a fixture
   package to the tests. The error's hint ("move work into run()") was also
   wrong for this case; make the hint depend on what failed.
3. **Should fix. A changed helper file is not picked up until the engine
   restarts.** With `actions/shop.ts` importing `../lib/helper`, I edited the
   helper and sent `openProject` again to the same engine: the bundle was
   rebuilt, but the old code stayed in use, because the rebuilt bundle has the
   same file name and Node had already loaded it. A new engine process picked up
   the change. The CLI starts a new engine each time and is not affected; the
   desktop app will be. Name each bundle after the hashes of all its inputs.
4. **Should fix before the runner. stderr is not masked.** ADR 0014 lists
   engine stderr as an exit point, but `console.*` output and the engine's own
   error logging go to stderr as they are. Once actions run, an action that
   logs a secret would print it, and CI logs keep stderr. Pass everything the
   engine writes to stderr through `mask`.
5. **Should fix. `${vars.x.constructor}` resolves.** `walk` in
   `context/interpolate.ts` uses `in`, which also finds inherited members
   (`constructor`, `toString`, `__proto__`). Look at own properties only.
6. **Note. An empty environment variable hides the `.env` value.**
   `environment[name] ?? dotEnv.get(name)` keeps an empty string. Treat empty
   as unset.
7. **Note. The action cache is never cleaned.** Each change to an action file
   leaves three old files in `.cfe/cache/actions/`. Delete bundles that no
   current action file maps to, at `openProject`.
8. **Note. `ctx`'s Playwright types are placeholders** (departure 1). Accepted:
   the instruction asked for both the documented type and no Playwright, and
   the report said so plainly. Replace them in the branch that adds Playwright.

## Rulings on the report's "Decisions I made"

| No. | Decision                                                               | Ruling                        |
| --- | ---------------------------------------------------------------------- | ----------------------------- |
| 1   | `/regex/` URL patterns clash with paths such as `/orders/`             | **Owner decides.** See below. |
| 2   | `SdkVersionMismatch` points at `package.json` line 1                   | Accepted.                     |
| 3   | Environment wins over `.env`; `.env` syntax; too-short secret at use   | Accepted, with finding 6.     |
| 4   | Interpolating `null` and objects into text gives JSON text             | Accepted. Document.           |
| 5   | Namespace suggestions come from the file name                          | Accepted.                     |
| 6   | `UnknownAction` and `ActionNotLoaded` for steps calling failed actions | Accepted.                     |
| 7   | Named groups count as capturing groups in `extract`                    | Accepted.                     |
| 8   | Masking also covers mapping keys                                       | Not accepted. See finding 1.  |
| 9   | `listActions` without an open project returns the built-ins            | Accepted. Document.           |

The report's other departures (esbuild's install script allowed, rebuild when
any input changes, re-finding files on `validate`, tooling for the examples)
are accepted.

The report's answer on plan branches 6 and 7 is accepted: the CI job that
installs Chromium moves into branch 6, and the demo web server stays in 7.

The report's suggestion to guard `process.stdout.write` against user code is
accepted for the runner branch: the writer keeps a private handle to stdout and
anything else written to stdout goes to stderr.

## Done-when checks

| Check                                                            | Result                                           |
| ---------------------------------------------------------------- | ------------------------------------------------ |
| `pnpm verify` passes on every branch                             | Yes (top of stack by reviewer; each by CI)       |
| Every pull request open, CI green on three systems               | Yes                                              |
| Each of the eight findings has a test that fails without its fix | Yes per the report; fixes confirmed by my probes |
| Every accepted ruling of review 0003 is in a document            | Yes                                              |
| F9 through `openProject`; I12; I13                               | Yes (tests pass)                                 |
| Committed JSON Schema files match the generator                  | Yes (test)                                       |
| Headers, TSDoc, no `any`, no suppressions                        | Yes                                              |
| Only `esbuild` added                                             | Yes                                              |
| No client imports engine code                                    | Yes                                              |
| `CHANGELOG.md` updated in each branch                            | Yes                                              |
| The report exists and follows the template                       | Yes                                              |

The work meets the instruction's checks. #13 is held for finding 1, which the
instruction did not foresee: it is a defect in the design of masking, not a
missed task.

## Owner decisions needed

1. **How to write a regular expression for a URL.** Today the documents say a
   URL pattern is a glob unless written as `/regex/`. So `wait.url: /orders/`
   is read as the regular expression `orders`, not the path `/orders/`.
   Options: (a) keep it; (b) require a marker that cannot be a path.
   Recommended: **b**, with the prefix `regex:`, as in
   `wait.url: 'regex:^/orders/\d+$'`. Anything without the prefix is a glob.

## For the next instruction

Instruction 0005:

- On `feat/context-secrets` (same branch, new commits): findings 1, 4, 5 and 6,
  and a new report.
- A `fix/` branch from `main` for findings 2, 3 and 7.
- The owner's answer on URL regular expressions, applied to the documents, the
  schemas and the validator.
- Then plan branch 6 (`feat/locators`) with the Chromium CI job, and branch 7.
