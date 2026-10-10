# Actions

> Status: **Accepted design** (2026-10-09).

Every step calls an action. Built-in actions and the user's own actions are
defined the same way, with `defineAction`, and live in the same registry.

## Defining an action

```ts
// actions/shop/add-to-cart.ts (in the user's repository)
import { defineAction, target, z } from '@cfe/engine/sdk';

export default defineAction({
  name: 'shop.addToCart',
  description: 'Opens a product page and adds the product to the cart.',
  shorthand: 'product',
  params: z.object({
    product: z.string().describe('Product name as shown in the catalogue'),
    quantity: z.number().int().min(1).default(1),
    button: target().optional(),
  }),
  async run(ctx, params) {
    await ctx.page.getByRole('link', { name: params.product }).click();
    await ctx.page.getByLabel('Quantity').fill(String(params.quantity));
    const add = params.button
      ? await ctx.locate(params.button)
      : ctx.page.getByRole('button', { name: 'Add to cart' });
    await add.click();
    ctx.log.info(`Added ${params.quantity} × ${params.product}`);
  },
});
```

Used in a step file:

```yaml
- shop.addToCart: Desk lamp # shorthand
- shop.addToCart: { product: Chair, quantity: 2 } # long form
```

| Field         | Required | Meaning                                                                                    |
| ------------- | -------- | ------------------------------------------------------------------------------------------ |
| `name`        | yes      | Name used in step files; see [Action names](#action-names)                                 |
| `params`      | yes      | A Zod object schema (see [ADR 0006](adr/0006-zod-schemas.md)). Use `z.object({})` for none |
| `run`         | yes      | `async (ctx, params) => void`. `params` is typed from the schema                           |
| `description` | no       | One sentence shown in clients and in `listActions`                                         |
| `shorthand`   | no       | The one parameter set by `- action: value`                                                 |

`description` and `shorthand` are approved additions to the brief's
`defineAction({ name, params, run })`.

### Shorthand and the canonical long form

Every action has exactly one canonical long form: its `params` mapping. The
shorthand is only a way of writing it: `- goto: /products` is normalised by the
validator to `{ url: /products }` before anything else sees it. Consequences:

- `run` always receives the long form; it cannot tell how the step was written.
- `shorthand` must name a parameter that exists in `params`; otherwise loading
  the action fails.
- Protocol events, results and clients only see the long form.
- An action without `shorthand` must be written in the long form (or bare, if it
  has no required parameters).

`params` serves three purposes from one source: validation of step files (with
line numbers), the TypeScript type of `params` in `run`, and a JSON Schema that
clients receive through `listActions` to build editing forms.

### Specs and runnable actions

Everything in the table above except `run` is the action's **`ActionSpec`**:
`name`, `description`, `shorthand` and `params`. A runnable action is an
`ActionSpec` plus `run` ([ADR 0021](adr/0021-action-spec.md)).

- The engine holds the specs of all built-in actions from the start, so the
  validator can check every step before any action can run.
- `defineAction` takes an `ActionSpec` plus `run` and returns a runnable action;
  user actions are described by the same shape as built-ins.
- `listActions` reports specs (with `params` as JSON Schema).

`target()` is a schema helper for target parameters. In a step file it accepts a
target name or an inline candidate list; in `run` it is an unresolved
`TargetRef` that must be passed to `ctx.locate`.

### Action names

([ADR 0016](adr/0016-action-names.md))

- **User actions must be namespaced**: `<namespace>.<name>`, for example
  `auth.fillOtp` or `shop.addToCart`. Namespace and name each start with a
  letter and contain letters and digits.
- **Names without a dot are reserved for built-in actions**, including future
  ones.
- **Reserved namespaces**: `expect`, `wait`, `api` and the product's command
  name (`cfe`). Built-ins keep their dotted names
  (`expect.text`, `wait.url`). The engine keeps this list in one constant,
  `RESERVED_NAMESPACES`, which both the built-ins and the name check use.
- A user action that breaks these rules, or whose name is already taken, **fails
  at load time** with a diagnostic such as:

  ```
  actions/auth.ts:4:9  error  ActionNameNotNamespaced
    Action "fillOtp" needs a namespace, for example "auth.fillOtp".
    Names without a dot are reserved for built-in actions.

  actions/shop.ts:12:9  error  ActionNamespaceReserved
    Action "expect.priceFormat" uses the namespace "expect", which is reserved
    for built-in actions. Use your own namespace, for example
    "shop.expectPriceFormat".
  ```

### Loading user actions

- The config's `actions` globs (default `actions/**/*.ts`) are loaded when a
  project is opened. Each file default-exports one action or an array of
  actions.
- Each file is bundled with esbuild into `.cfe/cache/actions/` and imported
  by the engine ([ADR 0008](adr/0008-loading-user-actions.md)). Files may import
  other files and packages installed in the user's repository.
- **One SDK copy: the running engine's.** When bundling, every import of
  `@cfe/engine/sdk` in a user action is redirected to the SDK module of
  the engine that is running, never to a copy in the user's `node_modules`.
  `defineAction`, `target()`, `z` and the error classes are therefore always
  the engine's own. The user may install the package as a dev dependency so
  their editor and `tsc` know the types; if its version differs from the
  engine's, the engine reports a warning (`SdkVersionMismatch`) because the
  types the user sees may not match what runs.
- A file that fails to compile, fails to load or breaks a naming rule is
  reported as a diagnostic with file and line. Tests that use a missing action
  fail validation; other tests still run. The diagnostics:

  | Code                      | When                                                             |
  | ------------------------- | ---------------------------------------------------------------- |
  | `ActionCompileError`      | esbuild cannot bundle the file (at the line and column it names) |
  | `ActionLoadError`         | the bundled file throws while it is imported                     |
  | `InvalidActionExport`     | the default export is not an action or a list of actions         |
  | `ActionNameNotNamespaced` | the name has no dot                                              |
  | `ActionNamespaceReserved` | the namespace is `expect`, `wait`, `api` or the command name     |
  | `InvalidActionName`       | the name is not `<namespace>.<name>` in letters and digits       |
  | `ActionNameTaken`         | another action file already defines the name                     |
  | `InvalidShorthand`        | `shorthand` names no parameter                                   |
  | `SdkVersionMismatch`      | warning: the project has another version of the SDK installed    |

  A step that calls an action rejected by these rules gets `ActionNotLoaded`,
  naming the reason; a step calling an unknown namespaced action while some
  action files failed to load gets `UnknownAction` with those files in its
  hint.

- Bundles are cached in `.cfe/cache/actions/` by the file's content. A cached
  bundle is reused only while every file it was built from (including local
  files it imports) is unchanged.

### Trust model

**User actions are code, and they run with the engine's full access to the
machine and the network.** They are ordinary TypeScript that the engine imports
into its own process when a project is opened, before any test is selected, so
opening a project runs its code. They run with the same rights as the engine: they can read
and write files, start processes, use the network and read any environment
variable through `process.env`. The engine does not sandbox them, and step
files can send requests anywhere through `api`. Opening a repository in the
engine therefore requires the same trust as running `npm test` in it: open only
repositories you trust.

What the engine does guarantee: it never runs code from step files themselves
(YAML is data; only named actions run), it reads environment variables for
step files only through declared secrets, and it masks every declared secret in
its own output. What it cannot guarantee: an action that reads `process.env`
directly, or sends a secret somewhere itself, bypasses those rules. In the
server (v0.4.0) the isolation boundary is the container each project runs in.

### Failing an action

Throw. Errors exported by the SDK give the best messages:

```ts
import { ActionError, AssertionError } from '@cfe/engine/sdk';

throw new AssertionError('Cart total is wrong', { expected: '€20.00', actual: total });
throw new ActionError('The product is out of stock', {
  hint: 'Use a product with stock in the test data.',
});
```

Any other thrown value is reported as `ActionError` with its message. The
engine adds the step's file and line, masks secrets in the message, and
attaches the screenshot.

The engine recognises SDK errors by a **tag field**, not by `instanceof`: every
SDK error carries `sdkError: 'ActionError'` or `sdkError: 'AssertionError'`
(and the fields `expected`, `actual`, `hint` where given). The tag still works
if an error crosses a module or bundling boundary that would break
`instanceof`.

## The context (`ctx`)

| Member    | Type                                 | Meaning                                                                   |
| --------- | ------------------------------------ | ------------------------------------------------------------------------- |
| `page`    | Playwright `Page`                    | The page the step runs on (`main` unless the step sets `page`)            |
| `request` | Playwright `APIRequestContext`       | HTTP client that shares cookies with `page`'s browser context             |
| `vars`    | `Variables`                          | `get(name)`, `set(name, value)`, `has(name)` for test (or flow) variables |
| `env`     | `Readonly<Environment>`              | The selected environment profile: `name`, `baseUrl`, `values`             |
| `secrets` | `Secrets`                            | `get(name)`: the value of a declared secret; it is masked in all output   |
| `log`     | `Logger`                             | `debug`, `info`, `warn`; sent to clients as `log` events, secrets masked  |
| `locate`  | `(t: TargetRef) => Promise<Locator>` | Resolves a target's candidates (approved addition)                        |
| `signal`  | `AbortSignal`                        | Aborted on step timeout or run cancellation (approved addition)           |

`Page`, `APIRequestContext` and `Locator` are Playwright's own types, from the
engine's copy of Playwright; the SDK re-exports them. Imports of `playwright` in
a user action are redirected to that copy, like the SDK
([ADR 0008](adr/0008-loading-user-actions.md)).

`ctx.env` never contains the process environment. `ctx.secrets` is the only
way to read an environment variable, and only one declared as a secret.

### `ctx.locate`

Returns a Playwright `Locator` for the first candidate that matches exactly one
element, polling until the step timeout or until `ctx.signal` is aborted. If no
candidate matches, it throws `TargetNotFound` listing every candidate and how
many elements each matched ([ADR 0010](adr/0010-locator-candidates.md)). A
candidate whose selector Playwright rejects fails at once with
`InvalidSelector`, naming the target and the candidate.

It supports the long form of targets: it resolves the `frame` chain from the
outside in (each frame target must match exactly one `<iframe>`), then the
`within` chain, then the element's candidates inside that scope, interpolating
`${…}` in every target with the current step's values
([step-format.md](step-format.md#targets)).

Condition from the review: **the step result reports which candidate matched.**
Every `ctx.locate` call is recorded, and `stepPassed` / `stepFailed` carry a
`locators` list with the parameter, the target name, the index of the matching
candidate and the candidate itself ([protocol.md](protocol.md#events)).

### `ctx.signal`

Aborted when the step times out or the client sends `cancelRun`. Condition from
the review: **every built-in action honours it.** In practice:

- Waits the engine controls (`ctx.locate`, `expect.*` retries, `wait.*`,
  `opens`) check the signal on every poll, at least every 100 ms.
- Every Playwright call made by a built-in gets at most the step's remaining
  time as its `timeout`, so nothing outlives the step.
- An integration test cancels a run during a long wait and requires the step to
  end within 2 seconds.

User actions should pass `ctx.signal` on to anything that accepts one
(`fetch`, timers) and stop when it is aborted.

**A user action that ignores the signal.** When a step times out (or is
cancelled), the engine aborts `ctx.signal` and gives `run` 2 seconds to
return. If it has not returned by then:

1. The step is marked **failed** with `ActionTimeout` (or `Cancelled`) and the
   hint "The action did not stop when ctx.signal was aborted".
2. The engine **closes that step's page**, so code still running in the action
   cannot click, type or navigate during the `after` steps; its Playwright
   calls fail against the closed page.
3. The step's `ctx` is sealed: later calls to `ctx.vars.set`, `ctx.log` and
   `ctx.locate` from the stray code are ignored and reported once as a `warn`
   log (`StrayActionCode`).
4. If an `after` step uses that page name, it gets a new blank page in the same
   browser context (same cookies and login), and the engine logs that the page
   was replaced.

The stray JavaScript itself cannot be stopped from outside; closing the page
removes its ability to affect the browser.

### Internal extensions

Three built-ins need engine internals that user actions do not get: `call`
(runs a flow), and `wait.response` / `expect.response` (read the page's
response log). They are still defined with `defineAction`; their `ctx` is an
internal extension of the public one.

## Built-in actions

Columns: **Short** is the shorthand parameter. Target parameters accept a
target name or an inline candidate list. URLs may be relative to `env.baseUrl`.
URL patterns are globs (`**/api/orders*`); a pattern that starts with `regex:`
is a regular expression (`regex:^/orders/\d+$`), tested against the whole URL.
Without the prefix a pattern is always a glob, so `/orders/` is a path. In a
glob, `*` matches any characters except `/`, `**` any characters, `{a,b}`
either alternative, and every other character stands for itself; a glob that
starts with `/` is relative to `env.baseUrl`. Likewise, `expect.url`'s `equals`
compares a value that starts with `/` against `env.baseUrl` followed by it.

Any step, whatever its action, may also use the common keys `name`, `page`,
`timeout` and `opens` ([step-format.md](step-format.md#steps)).

### Navigation

| Action   | Short | Parameters                                                         |
| -------- | ----- | ------------------------------------------------------------------ |
| `goto`   | `url` | `url`; `waitUntil`: `load` (default), `domcontentloaded`, `commit` |
| `back`   | –     | none                                                               |
| `reload` | –     | none                                                               |

### Interaction

| Action   | Short    | Parameters                                                                                                        |
| -------- | -------- | ----------------------------------------------------------------------------------------------------------------- |
| `click`  | `target` | `target`; `button`: `left`/`right`/`middle`; `clickCount`; `modifiers`: list of `Alt`, `Control`, `Meta`, `Shift` |
| `fill`   | –        | `target`, `value` (clears the field first)                                                                        |
| `select` | –        | `target`, `option`: a value or label, or a list for multi-selects                                                 |
| `check`  | `target` | `target`; `checked`: `true` (default) or `false` to uncheck                                                       |
| `hover`  | `target` | `target`                                                                                                          |
| `press`  | `key`    | `key` (`Enter`, `Control+A`, …); `target` (optional, otherwise the focused element)                               |
| `upload` | –        | `target`, `files`: a path or list of paths relative to the step file; `[]` clears                                 |
| `drag`   | –        | `from`, `to`: targets                                                                                             |

`select` waits for the options it is given, as Playwright does. When the step
runs out of time and an option it asked for is not on the element, it fails
with `OptionNotFound`, naming the missing option and every option the element
has (value and label).

### Waiting

| Action          | Short    | Parameters                                                                                        |
| --------------- | -------- | ------------------------------------------------------------------------------------------------- |
| `wait.element`  | `target` | `target`; `state`: `visible` (default), `hidden`, `attached`, `detached`                          |
| `wait.url`      | `url`    | `url`: URL pattern                                                                                |
| `wait.response` | `url`    | `url`: URL pattern; `method`; `status`; `as`: variable to store `{ status, headers, json, text }` |

`wait.response` also matches responses that arrived **since the previous step
started**, so "click, then wait for the request it caused" works without a race
(the investigation for ADR 0007 hit exactly this race with plain Playwright).

Response headers stored by `as` (in `wait.response` and `api`) keep their real
values, so a test can read a token from a response and send it on. When such a
response has a `Cookie`, `Set-Cookie` or `Authorization` header, the engine
registers its value as a secret for the rest of the run (for cookie headers,
each cookie's value), so it is masked wherever the engine writes it out
([ADR 0014](adr/0014-secret-masking.md)). `expect.response` does the same for
the headers it reads.

Header names in a stored response are in lower case
(`${vars.issued.headers.authorization}`). For `Authorization`, both the whole
value and the credential after its scheme (`Bearer …`) are masked. A browser
never finishes loading the body of a `fetch` the page does not read, so
Playwright cannot give it: `wait.response … as` then waits for the body until
the step's time is almost up, logs a `warn` and stores the response with an
empty `text` and a `null` `json`; `expect.response` with `json` or `contains`
fails, saying so.

### Assertions

All `expect.*` actions retry until they pass or the step times out, then fail
with `AssertionFailed` showing expected and actual values.

`matches` values, `extract`'s `pattern` and URL patterns that start with `regex:` are
compiled when the step file is validated (`InvalidRegex`), unless they contain
`${…}`; then they are checked when the step runs. `extract`'s pattern must have
exactly one capturing group: the part to extract.

| Action            | Short    | Parameters                                                                     |
| ----------------- | -------- | ------------------------------------------------------------------------------ |
| `expect.visible`  | `target` | `target`; `visible`: `true` (default) or `false`                               |
| `expect.text`     | –        | `target`; exactly one of `equals`, `contains`, `matches` (regex); `ignoreCase` |
| `expect.value`    | –        | `target`; exactly one of `equals`, `contains`, `matches`                       |
| `expect.url`      | `equals` | exactly one of `equals`, `contains`, `matches`                                 |
| `expect.count`    | –        | `target`; `equals`, or `min` and/or `max`                                      |
| `expect.response` | –        | `url`, `method`; `status`; `json`: partial deep match; `contains`: body text   |

`expect.visible` with `visible: false` passes when no candidate matches a
visible element. `expect.count` counts every match of the first candidate that
matches at least one element (the one action where several matches are
expected).

### Variables and data

| Action    | Short | Parameters                                                                                                         |
| --------- | ----- | ------------------------------------------------------------------------------------------------------------------ |
| `set`     | –     | A mapping of variable names to values: `- set: { email: 'a@b.c', count: 2 }`                                       |
| `extract` | –     | `target`, `as`; `from`: `text` (default), `value`, `attribute`; `attribute`: name; `pattern`: regex with one group |

`set` is the one action whose long form is the mapping of variables itself.

### HTTP

| Action | Short | Parameters                                                                                                                                     |
| ------ | ----- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `api`  | –     | `method` (default `GET`), `url`, `headers`, `query`; one of `json`, `form`, `body`; `as`; `status`: expected status (default: any 2xx or 3xx)  |
| `mock` | –     | `url` pattern, `method`; `status` (default 200), `headers`; one of `json`, `body`, `file`; `times`: how many requests to answer (default: all) |

`api` uses `ctx.request`, so it is signed in exactly like the page; a URL
that starts with `/` is relative to the environment's `baseUrl`. With `as`, the
response is stored before its status is checked, so a failing step still
leaves it for later steps of an `after` section. `mock` applies to every page
in the step's browser context until the test ends; `file` is relative to the
step file. A request `mock` does not answer (another method, or after `times`)
goes on to the next handler or the network.

### Flows

| Action | Short  | Parameters                                          |
| ------ | ------ | --------------------------------------------------- |
| `call` | `flow` | `flow`: path of a `*.flow.yaml`; `with`: parameters |

```yaml
- call: flows/accept-cookies.flow.yaml
- call:
    flow: flows/add-to-cart.flow.yaml
    with: { product: Chair, quantity: 2 }
```

Each call is one step in the results, with the flow's steps nested under it
(`stepId` `steps.4/steps.1`). Each nested step's `stepStarted` carries the call's
`stepId` as `parentStepId` and the call's `section`. A `call` step has no
timeout of its own; each step of the flow has its own. The first failing step
of a flow skips the rest of it and fails the call with `FlowFailed`, whose
message names the failing step and its place in the flow file. The outputs the
flow set are copied back even then, so `after` steps can use them; an output a
passing flow never set gives a `FlowOutputNotSet` warning. Repeating a flow per data row is not part of
v0.1.0 ([ADR 0013](adr/0013-data-rows.md)).
