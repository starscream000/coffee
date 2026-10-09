# Actions

> Status: **Proposal** (Milestone 0). Open questions are marked **(Open)** and
> numbered as in [architecture.md](architecture.md#open-questions).

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
- shop.addToCart: { product: Chair, quantity: 2 }
```

| Field         | Required | Meaning                                                                                    |
| ------------- | -------- | ------------------------------------------------------------------------------------------ |
| `name`        | yes      | Name used in step files. Letters, digits, `.`, `_`, `-`; must start with a letter          |
| `params`      | yes      | A Zod object schema (see [ADR 0006](adr/0006-zod-schemas.md)). Use `z.object({})` for none |
| `run`         | yes      | `async (ctx, params) => void`. `params` is typed from the schema                           |
| `description` | no\*     | One sentence shown in clients and in `listActions`                                         |
| `shorthand`   | no\*     | The parameter set by `- action: value`                                                     |

\* `description` and `shorthand` are proposed additions to the
`defineAction({ name, params, run })` shape from the brief. Without `shorthand`,
every step would need the mapping form (`- goto: { url: /products }`).

`params` serves three purposes from one source: validation of step files (with
line numbers), the TypeScript type of `params` in `run`, and a JSON Schema that
clients receive through `listActions` to build editing forms.

`target()` is a schema helper for target parameters. In a step file it accepts a
target name or an inline candidate list; in `run` it is an unresolved
`TargetRef` passed to `ctx.locate`.

### Loading user actions

- The config's `actions` globs (default `actions/**/*.ts`) are loaded when a
  project is opened. Each file default-exports one action or an array of
  actions.
- Files are compiled with esbuild and imported by the engine
  ([ADR 0008](adr/0008-loading-user-actions.md)). They may import other files
  and packages installed in the user's repository.
- The user installs `@test-tool/engine` as a dev dependency only for types and
  `defineAction`; the engine accepts any object with the right shape, so
  version differences in the user's copy do not matter.
- A file that fails to compile or load, a duplicate name, or a name that equals
  a built-in is reported as a diagnostic with file and line. Tests that use a
  missing action fail validation; other tests still run.
- **(Open 8)** User actions may not reuse built-in names. A project prefix
  (`shop.`) is recommended, not required.

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
| `env`     | `Readonly<Environment>`              | Selected environment profile: `name`, `baseUrl`, values (Open 2)          |
| `secrets` | `Secrets`                            | `get(name)`: the value of a declared secret; it is masked in all output   |
| `log`     | `Logger`                             | `debug`, `info`, `warn`; sent to clients as `log` events, secrets masked  |
| `locate`  | `(t: TargetRef) => Promise<Locator>` | Resolves a target's candidates (proposed addition, Open 9)                |
| `signal`  | `AbortSignal`                        | Aborted on step timeout or run cancellation (proposed addition, Open 9)   |

`ctx.locate` returns a Playwright `Locator` for the first candidate that
matches exactly one element, waiting up to the step timeout. If no candidate
ever matches, it throws `TargetNotFound` listing every candidate and how many
elements each matched. See [ADR 0010](adr/0010-locator-candidates.md).

Three built-ins need engine internals that user actions do not get: `call`
(runs a flow), and `wait.response` / `expect.response` (read the page's
response log). They are still defined with `defineAction`; their `ctx` is an
internal extension of the public one.

## Built-in actions

Columns: **Short** is the shorthand parameter. Target parameters accept a
target name or an inline candidate list. URLs may be relative to `env.baseUrl`.
URL patterns are globs (`**/api/orders*`) unless written as `/regex/`.

### Navigation

| Action   | Short | Parameters                                                         |
| -------- | ----- | ------------------------------------------------------------------ |
| `goto`   | `url` | `url`; `waitUntil`: `load` (default), `domcontentloaded`, `commit` |
| `back`   | –     | none                                                               |
| `reload` | –     | none                                                               |

### Interaction

| Action   | Short    | Parameters                                                                                                                                     |
| -------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `click`  | `target` | `target`; `button`: `left`/`right`/`middle`; `clickCount`; `modifiers`: list of `Alt`, `Control`, `Meta`, `Shift`; `opens`: page name (Open 6) |
| `fill`   | –        | `target`, `value` (clears the field first)                                                                                                     |
| `select` | –        | `target`, `option`: a value or label, or a list for multi-selects                                                                              |
| `check`  | `target` | `target`; `checked`: `true` (default) or `false` to uncheck                                                                                    |
| `hover`  | `target` | `target`                                                                                                                                       |
| `press`  | `key`    | `key` (`Enter`, `Control+A`, …); `target` (optional, otherwise the focused element)                                                            |
| `upload` | –        | `target`, `files`: a path or list of paths relative to the step file; `[]` clears                                                              |
| `drag`   | –        | `from`, `to`: targets                                                                                                                          |

### Waiting

| Action          | Short    | Parameters                                                                                        |
| --------------- | -------- | ------------------------------------------------------------------------------------------------- |
| `wait.element`  | `target` | `target`; `state`: `visible` (default), `hidden`, `attached`, `detached`                          |
| `wait.url`      | `url`    | `url`: URL pattern                                                                                |
| `wait.response` | `url`    | `url`: URL pattern; `method`; `status`; `as`: variable to store `{ status, headers, json, text }` |

`wait.response` also matches responses that arrived **since the previous step
started**, so the common pattern "click, then wait for the request it caused"
works without a race.

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

`expect.count` counts every match of the first candidate that matches at least
one element (it is the one action where several matches are expected).

### Variables and data

| Action    | Short | Parameters                                                                                                         |
| --------- | ----- | ------------------------------------------------------------------------------------------------------------------ |
| `set`     | –     | A mapping of variable names to values: `- set: { email: 'a@b.c', count: 2 }`                                       |
| `extract` | –     | `target`, `as`; `from`: `text` (default), `value`, `attribute`; `attribute`: name; `pattern`: regex with one group |

### HTTP

| Action | Short | Parameters                                                                                                                                     |
| ------ | ----- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `api`  | –     | `method` (default `GET`), `url`, `headers`, `query`; one of `json`, `form`, `body`; `as`; `status`: expected status (default: any 2xx or 3xx)  |
| `mock` | –     | `url` pattern, `method`; `status` (default 200), `headers`; one of `json`, `body`, `file`; `times`: how many requests to answer (default: all) |

`api` uses `ctx.request`, so it is signed in exactly like the page. `mock`
applies to every page in the step's browser context until the test ends.

### Flows

| Action | Short  | Parameters                                                              |
| ------ | ------ | ----------------------------------------------------------------------- |
| `call` | `flow` | `flow`: path of a `*.flow.yaml`; `with`: parameters; `forEach` (Open 3) |

```yaml
- call: flows/accept-cookies.flow.yaml
- call:
    flow: flows/add-to-cart.flow.yaml
    with: { product: Chair, quantity: 2 }
- call: # (Open 3) repeat the flow per data row
    flow: flows/add-to-cart.flow.yaml
    forEach: ./data/cart.csv
    with: { product: '${row.product}', quantity: '${row.qty}' }
```

Each call is one step in the results, with the flow's steps nested under it.
