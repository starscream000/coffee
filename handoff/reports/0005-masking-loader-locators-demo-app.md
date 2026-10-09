# Report 0005: masking and loader fixes, locators, the demo app

- Date: 2026-10-09
- Written by: implementer
- Instruction: [0005](../instructions/0005-masking-loader-locators-demo-app.md)

## Summary

All seven findings of review 0004 are fixed. Each has a test that fails
without its fix, and each failure was checked by removing the fix and running
the test.

- **Masking (finding 1):** protocol messages are now masked field by field from
  one rules table next to the schemas, so a masked message always stays valid
  and keeps its identifiers.
- **Plan branch 6:** `ctx.locate` works against real Chromium, with Playwright
  1.64.0 pinned. A new `integration` CI job runs the browser tests on Linux,
  Windows and macOS with the browser cached.
- **Plan branch 7:** the demo web server and its harness exist.

Every pull request is green on all three systems. One point needs the owner's
attention: #13 is now about 2,300 changed lines, because instruction 0004's work
and the loader fix are on it. It cannot be split without rewriting pushed
history.

## Branches and pull requests

| Branch                   | Last commit                  | Pull request | Pushed | Changed lines\*                              | CI                                                        |
| ------------------------ | ---------------------------- | ------------ | ------ | -------------------------------------------- | --------------------------------------------------------- |
| `fix/review-0004-loader` | `a619ca9`                    | #14          | yes    | 217                                          | `verify` green on Linux, Windows, macOS                   |
| `feat/context-secrets`   | `4b222a6`                    | #13          | yes    | 880 (this instruction); 2,306 against `main` | `verify` green on Linux, Windows, macOS                   |
| `feat/locators`          | `4b6e0d2`                    | #15          | yes    | 1,141                                        | `verify` and `integration` green on Linux, Windows, macOS |
| `test/demo-app-harness`  | `838d87c` (plus this report) | #17          | yes    | 677 before this report                       | `verify` and `integration` green on Linux, Windows, macOS |

\* Insertions plus deletions from `git diff --shortstat`, not counting the
lockfile, fixtures or the generated JSON Schema files. Each count is against the
branch below it, except where it says `main`.

- **#14:** its first CI run failed on macOS. There, `/var` is a symbolic link to
  `/private/var`, and Node reports the real path in stack traces, so the load
  hint did not recognise the action file. `a619ca9` compares against the file's
  real path too. That fix was merged into `feat/context-secrets` again.
- **#13** does not exceed the 1,500-line limit in this instruction's own
  changes, but the whole branch does. See "Departures".

## Tasks

| Task | State | Notes                                                                                                                                                                                                       |
| ---- | ----- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | done  | Merged local branches deleted; `feat/context-secrets` kept.                                                                                                                                                 |
| 2    | done  | Each bundle gets a working `require` through a `createRequire` banner, and source maps are enabled. The hint names the cause: the action file's own top level, a package (by name), or other imported code. |
| 3    | done  | Each bundle is imported with `?inputs=<hash of every input>`, so Node never answers from its module cache when any input changed.                                                                           |
| 4    | done  | Bundles that no current action file maps to are deleted at `openProject`; a file that cannot be deleted is skipped.                                                                                         |
| 5    | done  | `MASK_RULES` in `packages/protocol/src/masking.ts`; see "How the protected fields are kept".                                                                                                                |
| 6    | done  | `packages/engine/src/rpc/masking.property.test.ts`; the four named cases are in `packages/engine/src/context/secrets.protocol.test.ts`.                                                                     |
| 7    | done  | ADR 0014 records the rule, the reason, what is given up, and that stderr is an exit point.                                                                                                                  |
| 8    | done  | The console replacement writes through `mask`, and so does the engine's internal error logging.                                                                                                             |
| 9    | done  | `Object.hasOwn` in every `${…}` path step (`vars`, `env`, `row`, `params`). An empty environment variable falls back to `.env`.                                                                             |
| 10   | done  | Ruling 4 is in `docs/step-format.md`. Ruling 9 was already in `docs/protocol.md` ("only the built-ins when no project is open").                                                                            |
| 11   | done  | `playwright` 1.64.0. The SDK uses Playwright's `Page`, `APIRequestContext` and `Locator`. `playwright` and its subpaths are redirected to the engine's copy.                                                |
| 12   | done  | `packages/engine/src/locate/`.                                                                                                                                                                              |
| 13   | done  | `LocatorReporter` (`locatorUsed`, `locatorFallback`), for the runner to implement.                                                                                                                          |
| 14   | done  | `locate.integration.test.ts`, 11 tests covering every case the instruction lists, plus a hidden duplicate and a non-iframe used as a frame.                                                                 |
| 15   | done  | `pnpm test:integration` and the `integration` CI job with a browser cache. `pnpm verify` excludes `*.integration.test.ts`.                                                                                  |
| 16   | done  | `docs/milestones/v0.1.0-plan.md`.                                                                                                                                                                           |
| 17   | done  | `examples/demo-app/server/`, documented in `examples/demo-app/README.md`.                                                                                                                                   |
| 18   | done  | `packages/engine/src/testing/demo-app.ts`.                                                                                                                                                                  |
| 19   | done  | `demo-app.integration.test.ts`, 7 tests.                                                                                                                                                                    |

### The test for each finding

All of these fail when their fix is removed; I checked each one that way.

| Finding | Test                                                                                                                                                                                                                                                                                                                                                                                                      |
| ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1       | `masking.property.test.ts`: 17 example messages, each with a secret taken in turn from every key and string. In `secrets.protocol.test.ts`, "a secret equal to "jsonrpc" / "result" / "line" / "2.0\",\"" leaves the message valid JSON that matches its schema" runs through a real engine; all four fail with serialised-text masking. `masking-rules.test.ts` fails for a schema field without a rule. |
| 2       | In `actions.protocol.test.ts`, "review 0004 finding 2 through a real engine process", since Vitest's own module runner hides the bug. In `loader.test.ts`, "finding 2: the load-error hint fits the cause".                                                                                                                                                                                               |
| 3       | `loader.test.ts`: "finding 3: a changed helper is used by a second load in the same process"                                                                                                                                                                                                                                                                                                              |
| 4       | `secrets.protocol.test.ts`: "masks a secret that an action file's top-level code logs to the console" and "masks a secret in the message of an internal error logged on stderr" (test engine `test/engines/secret-crash.mjs`)                                                                                                                                                                             |
| 5       | `interpolate.test.ts`: "reaches own properties only: … is PathNotFound" (8 cases) and "… for params"                                                                                                                                                                                                                                                                                                      |
| 6       | `secrets.test.ts`: "treats an empty environment variable as unset, so the .env value is used"                                                                                                                                                                                                                                                                                                             |
| 7       | `loader.test.ts`: "finding 7: bundles no current action file maps to are deleted, and one that cannot be is skipped"                                                                                                                                                                                                                                                                                      |

## Checks

| Command                                            | Result                                                                                                                              |
| -------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `pnpm verify` on `fix/review-0004-loader`          | passed, 291 tests                                                                                                                   |
| `pnpm verify` on `feat/context-secrets`            | passed, 351 tests                                                                                                                   |
| `pnpm verify` on `feat/locators`                   | passed, 357 tests                                                                                                                   |
| `pnpm verify` on `test/demo-app-harness`           | passed, 357 tests                                                                                                                   |
| `pnpm test:integration` on `feat/locators`         | passed, 11 tests                                                                                                                    |
| `pnpm test:integration` on `test/demo-app-harness` | passed, 18 tests                                                                                                                    |
| `pnpm generate:schemas`                            | no change: the protocol schemas did not change                                                                                      |
| CI `verify`                                        | green on Linux, Windows and macOS for #13, #14 and #15. The `verify` job installs no browser, which shows `pnpm verify` needs none. |
| CI `integration`                                   | green on Linux, Windows and macOS for #15 and #17                                                                                   |

### Playwright and the integration job

- **Playwright 1.64.0**, using Chrome Headless Shell 156.0.8078.4 (revision
  1248). CI installs only the headless shell (`--only-shell`), because the tests
  run headless.
- **Download size:** 120 MiB on Linux, 118.1 MiB on Windows, 99 MiB on macOS
  (arm64). Unpacked on Windows: 278 MB.
- **Times of the `integration` job** (whole job, including checkout and
  `pnpm install`):

| System  | Cold cache (first run of #15) | Warm cache (rerun of #15) |
| ------- | ----------------------------- | ------------------------- |
| Linux   | 52 s                          | 48 s                      |
| Windows | 1 min 28 s                    | 1 min 42 s                |
| macOS   | 52 s                          | 30 s                      |

All three warm runs restored the browser from the cache ("Cache hit for:
playwright-<system>-1.64.0") and skipped the download.

- **Linux:** the warm run saves little, because the step that installs
  Chromium's system libraries still runs with `apt` every time.
- **Windows:** the warm run was slower than the cold one. Restoring the cached
  folder (many files, about 280 MB unpacked) is slow on Windows runners, and
  one run per case cannot separate that from normal runner variance. If
  Windows stays slow, the cache could hold the downloaded zip instead of the
  unpacked folder, or the Windows job could skip the cache.

The cache is keyed on the system and the exact Playwright version. GitHub
shares a cache saved by a pull request only with reruns of that pull request
and does not share it with other pull requests. #17's first run was therefore
cold too. Once a push to `main` runs the job, every pull request gets a warm
cache.

## How the protected fields are kept

- **Where the rules are:** one table, `MASK_RULES` in
  `packages/protocol/src/masking.ts`, next to the schemas. It gives each field
  name one of three rules:
  - `keep`: identifiers, fixed values, paths, names, numbers and booleans;
  - `mask`: every string inside is masked (free text and user data);
  - `walk`: a container whose fields are looked up in turn.
- **Overrides:** an entry `parent.field` applies inside one parent field only.
  For example, `error.data` is walked while `data` elsewhere is masked, and
  `snapshot.reason` is masked while `reason` is kept. `envelope.params` (the
  message's own `params`) is walked, while a step's `params` is masked.
- **How masking applies them:** `maskMessage` walks the message by these rules.
  It never touches keys or the serialised text, and it runs before truncation.
- **When someone adds a protocol field and forgets the table:**
  `masking-rules.test.ts` reads every protocol schema, converts it to JSON
  Schema, collects every property name, and fails with the list of names that
  have no rule. Until the table is fixed, the new field is masked, because a
  field the table does not name is masked. A forgotten field can therefore make
  an identifier unreadable, but it can never leak a secret.
- **Limit:** the rules are by field name, not by message. A name means the same
  thing everywhere unless a `parent.field` entry says otherwise.

## Departures from the instruction

- **#13 is over 1,500 changed lines against `main` (2,306), and I did not split
  it.** This instruction's own changes on the branch are 880 lines. The rest is
  instruction 0004's work and the merged loader fix (217). Splitting it now
  would mean rewriting a pushed branch whose pull request the instruction says
  to keep, which the git rules forbid. Once #14 is merged, its 217 lines drop out
  of #13's diff.
- **`SecretRegistry.maskValue` is removed** instead of fixed. It masked keys,
  and the writer no longer needs it now that masking follows the protocol
  rules.
- **Finding 5 also covers lists:** a list position that does not exist
  (`${vars.items.9}` on two items) is now `PathNotFound` instead of an empty
  value. The same applies to a non-numeric step into a list
  (`${vars.items.length}`). Both are documented in `docs/step-format.md`.
- **The I13 console test** now waits for stderr instead of reading it at once.
  stdout and stderr are separate pipes, so stderr can arrive after the response
  that follows it.
- **The Vitest projects are now defined inline** (`extends: true`). Before,
  `projects: ['packages/*']` made each package use Vitest's defaults, so the
  root `include` did not apply. Without this change the browser tests would
  have run in `pnpm verify`.
- **The plan row for branch 7** said the harness drives the engine "in-process
  or as a child process". Task 18 says child process, so the plan now says
  that.

## Decisions I made

1. **"Matches" counts hidden elements.** `ctx.locate` uses `locator.count()`,
   which counts every attached element, visible or not. This is what
   Playwright's strict mode counts, so the locator returned is one that a later
   `click` will not reject as ambiguous. As a result, a candidate that also
   matches a hidden copy (for example a mobile menu) is skipped. A browser test
   shows this. `expect.visible: false`, as ADR 0010 describes it, is the
   built-in's own concern later.
2. **`testId` candidates use the CSS selector `[<attribute>="<value>"]`**
   instead of `getByTestId`. Playwright's test ID attribute is one global
   setting per process, while `testIdAttribute` belongs to each project. The
   selector matches the same elements (exact value, through shadow DOM).
3. **A frame target must match an `<iframe>` or `<frame>`**: the candidate's
   locator is combined with `iframe, frame`, so another element counts as 0
   matches and the message shows it.
4. **The `param` of a `LocatorUse`.** `ctx.locate(target)` does not know which
   step parameter the target came from, so `locate` takes an optional `param`
   (default `target`) for the runner to fill in.
5. **Polling every 50 ms.** The wait also wakes at the end of the grace
   period, so fallbacks are tried as soon as they are allowed.
6. **On abort**, `locate` throws the signal's reason, so the runner decides
   between `ActionTimeout` and `Cancelled`. A `LocatorUse` with
   `candidateIndex: null` is still reported.
7. **On `TargetNotFound`**, the `LocatorUse` keeps the candidate of any outer
   level the last attempt found (for example the `within` row), with `null`
   for the rest. The error carries a structured `report` as well as the
   message.
8. **A `${…}` in a candidate that cannot be resolved** fails at once with
   `InterpolationError`, without retrying, since waiting cannot fix it.
9. **`reason` is kept** (`testSkipped.reason` is the `skip:` text from a step
   file), and `snapshot.reason` is masked (engine error text). `name` is kept
   everywhere: test, action, error and client names all come from the
   repository or the engine.
10. **The demo server is TypeScript run directly by Node 24** (type stripping),
    so it needs no build step and no dependency. Its two files import each
    other with `.ts` extensions; `tsconfig.check.json` allows that, and it emits
    nothing.

### Where the documents were unclear or silent

- **"Matches" and hidden elements:** ADR 0010 says "exactly one element"
  without saying whether hidden elements count. See decision 1.
- **`frame` and `within`:** `docs/actions.md` says "the `frame` chain, then the
  `within` chain", while `docs/step-format.md` forbids both on one target and
  takes the frame from the `within` target. These agree in practice. The code
  resolves each level's own `frame`, then its `within`, from the outside in.
- **`testIdAttribute`:** step-format.md maps `testId` to `getByTestId`, but
  `getByTestId` uses Playwright's one global attribute. See decision 2.
- **Fallback warning wording:** the documents give the code and the `data`
  only. The message says "was found by its candidate N (counting from 0), not
  its first".

## How the base URL reaches the engine

The demo config names the fixed URL `http://localhost:4310`. The harness:

1. starts the server with `--port 0`, so the system picks a free port;
2. reads the URL the server prints;
3. copies `examples/demo-app` to a temporary folder (without `.cfe` and
   `node_modules`);
4. replaces every `http://localhost:4310` in the copy's `cfe.config.yaml` with
   the server's URL, which covers `baseUrl` and `values.apiUrl`;
5. opens the copy in the engine.

If the config ever stops naming that URL, the harness fails with a message
instead of running against the wrong port. Nothing in the engine changed for
this: there is no override flag, because none was asked for.

## Questions for the owner

1. **Should hidden elements count when `ctx.locate` decides whether a
   candidate matches exactly one element?**
   - **(a) Count every attached element (recommended, implemented).** This is
     the same as Playwright's strict mode: the element that is found is the one
     actions then act on, and a click can never hit an ambiguous locator.
   - **(b) Count visible elements only.** This is friendlier to pages with
     hidden duplicates, but a later click on that locator could fail Playwright's
     strict check, so the engine would have to narrow it with
     `filter({ visible: true })`.

## Not done, not pushed, not verified

- The warm-cache time of #17's `integration` job is not measured; all its runs
  are cold, for the cache-scope reason above.
- `pnpm verify` was not run locally with Chromium uninstalled. The CI `verify`
  jobs install no browser and pass, which shows the same thing.

## Suggestions

- Run the CI workflow on pushes to `main` (it already is) and merge #15 early,
  so the browser cache is saved once on `main` and every later pull request
  starts warm.
- When the runner implements `LocatorReporter`, it can fill in `param` from the
  step's target parameters by identity, so user actions need not pass it.
