# Actions

> Status: **Proposal, revised after the first review** (2026-10-09).

Every step calls an action. Built-in actions and the user's own actions are
defined the same way, with `defineAction`, and live in the same registry.

## Defining an action

```ts
// actions/shop/add-to-cart.ts (in the user's repository)
import { defineAction, target, z } from '@test-tool/engine/sdk';

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
- Proposed (open question 13): the namespaces `expect` and `wait` are also
  reserved, because built-ins use them.
- A user action that breaks these rules, or whose name is already taken, **fails
  at load time** with a diagnostic such as:

  ```
  actions/auth.ts:4:9  error  ActionNameNotNamespaced
    Action "fillOtp" needs a namespace, for example "auth.fillOtp".
    Names without a dot are reserved for built-in actions.
  ```

### Loading user actions

- The config's `actions` globs (default `actions/**/*.ts`) are loaded when a
  project is opened. Each file default-exports one action or an array of
  actions.
- Each file is bundled with esbuild into `.coffee/cache/actions/` and imported
  by the engine ([ADR 0008](adr/0008-loading-user-actions.md)). Files may import
  other files and packages installed in the user's repository.
- The user installs `@test-tool/engine` as a dev dependency for types and
  `defineAction`. The engine accepts any object with the right shape, so a
  different version of the package in the user's repository still works.
- A file that fails to compile, fails to load or breaks a naming rule is
  reported as a diagnostic with file and line. Tests that use a missing action
  fail validation; other tests still run.

### Failing an action

Throw. Errors exported by the SDK give the best messages:

```ts
import { ActionError, AssertionError } from '@test-tool/engine/sdk';

throw new AssertionError('Cart total is wrong', { expected: '€20.00', actual: total });
throw new ActionError('The product is out of stock', {
  hint: 'Use a product with stock in the test data.',
});
```

Any other thrown value is reported as `ActionError` with its message. The
engine adds the step's file and line, masks secrets in the message, and
attaches the screenshot.

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

`ctx.env` never contains the process environment. `ctx.secrets` is the only
way to read an environment variable, and only one declared as a secret.

### `ctx.locate`

Returns a Playwright `Locator` for the first candidate that matches exactly one
element, polling until the step timeout or until `ctx.signal` is aborted. If no
candidate matches, it throws `TargetNotFound` listing every candidate and how
many elements each matched ([ADR 0010](adr/0010-locator-candidates.md)).

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

### Internal extensions

Three built-ins need engine internals that user actions do not get: `call`
(runs a flow), and `wait.response` / `expect.response` (read the page's
response log). They are still defined with `defineAction`; their `ctx` is an
internal extension of the public one.

## Built-in actions

Columns: **Short** is the shorthand parameter. Target parameters accept a
target name or an inline candidate list. URLs may be relative to `env.baseUrl`.
URL patterns are globs (`**/api/orders*`) unless written as `/regex/`.

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

### Waiting

| Action          | Short    | Parameters                                                                                        |
| --------------- | -------- | ------------------------------------------------------------------------------------------------- |
| `wait.element`  | `target` | `target`; `state`: `visible` (default), `hidden`, `attached`, `detached`                          |
| `wait.url`      | `url`    | `url`: URL pattern                                                                                |
| `wait.response` | `url`    | `url`: URL pattern; `method`; `status`; `as`: variable to store `{ status, headers, json, text }` |

`wait.response` also matches responses that arrived **since the previous step
started**, so "click, then wait for the request it caused" works without a race
(the investigation for ADR 0007 hit exactly this race with plain Playwright).

### Assertions

All `expect.*` actions retry until they pass or the step times out, then fail
with `AssertionFailed` showing expected and actual values.

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

`api` uses `ctx.request`, so it is signed in exactly like the page. `mock`
applies to every page in the step's browser context until the test ends.

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
(`stepId` `steps.4/steps.1`). Repeating a flow per data row is not part of
v0.1.0 ([ADR 0013](adr/0013-data-rows.md)).
