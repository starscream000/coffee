# Step file format

> Status: **Accepted design** (2026-10-09). The
> display name "Coffee" is final; the command, data-folder and config-file names
> used below (`coffee`, `.coffee/`, `coffee.config.yaml`) are interim until the
> owner chooses them ([ADR 0019](adr/0019-four-product-names.md)).

Step files are YAML documents in the user's Git repository. They are the source
of truth: the engine, the CLI and the desktop app only read and write them. The
format is designed to be readable in a code review and stable under
re-recording.

## File kinds

The file name decides the kind:

| Pattern              | Kind    | Contains                                              |
| -------------------- | ------- | ----------------------------------------------------- |
| `*.test.yaml`        | test    | One test: pages, data, before, steps, after           |
| `*.flow.yaml`        | flow    | A reusable sequence of steps with parameters          |
| `*.targets.yaml`     | targets | Shared, named targets (element locators)              |
| `coffee.config.yaml` | config  | Project settings, environments, logins (one per repo) |

Every file starts with `version: 1`. The engine refuses unknown versions with a
message naming the supported ones, so the format can evolve safely.

## A complete example

```yaml
# tests/checkout/guest-checkout.test.yaml
version: 1
name: Guest checks out ${row.product}
description: A visitor without an account buys one product.
tags: [smoke, checkout]

data:
  - { product: Desk lamp, sku: desk-lamp, price: '€20.00' }
  - { product: Chair, sku: chair, price: '€45.00' }

targets:
  addToCart:
    - role: button
      name: Add to cart
    - testId: add-to-cart
    - css: '.product-actions button.primary'

before:
  - mock:
      url: '**/api/recommendations'
      json: []

steps:
  - goto: /products
  - click:
      target:
        - role: link
          name: ${row.product}
  - click: addToCart
  - expect.text: { target: cart.count, equals: '1' }
  - call:
      flow: flows/checkout-as-guest.flow.yaml
      with:
        email: guest+${row.sku}@example.com
  - expect.text: { target: order.total, equals: '${row.price}' }
  - extract: { target: order.number, as: orderNumber }
  - click: order.printReceipt
    opens: receipt
  - expect.text: { target: receipt.total, equals: '${row.price}' }
    page: receipt

after:
  # Skipped (not failed) if the test stopped before orderNumber was extracted.
  - api:
      method: DELETE
      url: ${env.apiUrl}/orders/${vars.orderNumber}
      headers: { Authorization: 'Bearer ${secrets.SHOP_API_TOKEN}' }
```

## Test files

| Key           | Required | Meaning                                                                          |
| ------------- | -------- | -------------------------------------------------------------------------------- |
| `version`     | yes      | Format version, currently `1`                                                    |
| `name`        | yes      | Human-readable name. May use `${row.…}` so data rows have distinct names         |
| `description` | no       | Free text                                                                        |
| `tags`        | no       | List of strings, used to select tests (`--tag smoke`)                            |
| `skip`        | no       | A non-empty reason; the test is not run, see [Skipping a test](#skipping-a-test) |
| `login`       | no       | Saved login for the default page `main` (shorthand for `pages.main.login`)       |
| `freshLogin`  | no       | `true`: sign in from scratch, see [Saved logins](#saved-logins)                  |
| `pages`       | no       | Named pages for multi-tab or multi-user flows, see [Pages](#pages)               |
| `data`        | no       | Data rows; the test runs once per row, see [Data rows](#data-rows)               |
| `vars`        | no       | Initial variables                                                                |
| `targets`     | no       | Targets used only in this file                                                   |
| `before`      | no       | Setup steps                                                                      |
| `steps`       | yes      | The test itself                                                                  |
| `after`       | no       | Clean-up steps; **always run**, see [Execution rules](#execution-rules)          |

### Skipping a test

```yaml
skip: 'Checkout is down on staging until ticket SHOP-412 is fixed'
```

A skipped test is still **validated** (so it does not rot), but not run. It
appears in results with status `skipped` and its reason, once per data row.
`skip` takes a reason, never just `true`.

## Steps

Every step calls exactly one action. A step is written in one of three forms:

```yaml
- back # action without parameters
- goto: /products # shorthand: the action's shorthand parameter
- fill: # long form: named parameters
    target: checkout.email
    value: ${vars.email}
```

**Every action has exactly one canonical long form.** The validator normalises
the bare and shorthand forms to it, so the runner, the protocol and every
client only ever see the long form (`- goto: /products` becomes
`{ action: goto, params: { url: /products } }`). Shorthand is a writing
convenience, never a second meaning.

Besides the action key, a step may have these common keys:

| Key       | Meaning                                                                       |
| --------- | ----------------------------------------------------------------------------- |
| `name`    | Label shown in results instead of the generated one                           |
| `page`    | Named page the action runs on (default `main`)                                |
| `timeout` | Overrides the default timeout for this step: `500ms`, `10s`, `2m`             |
| `opens`   | Name for a new page (tab or pop-up) that this step opens, see [Pages](#pages) |

```yaml
- click: approveOrder
  page: backoffice
  timeout: 20s
  name: Admin approves the order
```

A mapping with zero or two action keys is a validation error. Unknown keys are
errors, with a "did you mean" hint where one is close (`exepct.text` →
`expect.text`).

Action names are listed in [actions.md](actions.md). User actions are always
namespaced (`auth.fillOtp`); see [ADR 0016](adr/0016-action-names.md).

There is deliberately **no `if`, `else`, `while` or free loop**. The only ways to
reuse or repeat steps are calling a flow and repeating a whole test for each
data row.

## Targets

A target describes one element. It stores several **locator candidates in order
of reliability**; the engine tries them in that order and reports which one it
used ([ADR 0010](adr/0010-locator-candidates.md)).

### Short form and long form

The short form is just the list of candidates:

```yaml
checkoutButton:
  - role: button # 1. ARIA role and accessible name
    name: Check out
  - testId: checkout # 2. test ID attribute (data-testid by default)
  - css: '#cart .btn-primary' # 3. CSS, last resort
```

The long form adds where to look. The short form above is normalised to
`{ candidates: [...] }`:

```yaml
checkoutButton:
  frame: shopFrame # optional: the iframe the element is in
  within: cartPanel # optional: an element the element is inside
  candidates:
    - role: button
      name: Check out
```

| Key          | Meaning                                                                                         |
| ------------ | ----------------------------------------------------------------------------------------------- |
| `candidates` | Ordered list of candidates (required)                                                           |
| `frame`      | A target for the `<iframe>` element that contains this one; see [Frames](#frames)               |
| `within`     | A target for an element that contains this one; see [Scoping with within](#scoping-with-within) |

### Candidates

Candidate kinds, in the order the recorder will write them:

| Kind          | Example                         | Playwright equivalent           |
| ------------- | ------------------------------- | ------------------------------- |
| `role`+`name` | `{ role: button, name: Save }`  | `getByRole('button', { name })` |
| `label`       | `{ label: Email address }`      | `getByLabel`                    |
| `placeholder` | `{ placeholder: Search }`       | `getByPlaceholder`              |
| `text`        | `{ text: Order confirmed }`     | `getByText`                     |
| `testId`      | `{ testId: checkout }`          | `getByTestId`                   |
| `css`         | `{ css: '#cart .btn-primary' }` | `locator(css)`                  |

Text-like fields (`name`, `label`, `placeholder`, `text`) match exactly by
default; add `exact: false` for substring matching. Any candidate may add
`nth: 0` to pick one of several matches, which the recorder only writes when
nothing else is unique. Candidate values may use `${…}` interpolation.

### Frames

`frame` is a target (a name, or an inline target) for the `<iframe>` element.
The frame's own target may have a `frame` too, for frames inside frames:

```yaml
targets:
  checkoutFrame:
    - css: 'iframe#checkout'
  paymentFrame:
    frame: checkoutFrame # the payment iframe sits inside the checkout iframe
    candidates:
      - css: 'iframe[title="Secure payment"]'
  cardNumber:
    frame: paymentFrame
    candidates:
      - label: Card number
```

`ctx.locate` resolves the outermost frame first, then each inner frame inside
it, then the element's candidates inside the innermost frame. Each frame target
must match exactly one `<iframe>`, using the same candidate rules as any target.

### Scoping with `within`

`within` is a target for a containing element. The candidates are only
searched inside the one element that `within` resolves to. Together with
interpolation, this finds an element in a particular row or card without a
custom action:

```yaml
targets:
  productRow:
    - role: row
      name: ${row.product}
  deleteProduct:
    within: productRow
    candidates:
      - role: button
        name: Delete

steps:
  - click: deleteProduct # the Delete button in the row named after the data row
```

Rules for `frame` and `within`:

- Both accept a target name or an inline target, and may be nested to any depth.
- A target with `within` takes its frame from the `within` target; giving both
  `frame` and `within` on the same target is a validation error ("put `frame`
  on the outer target").
- Interpolation in a target is resolved **when the step runs**, with that
  step's `vars`, `row`, `params` and `env`.
- A reference cycle (`a within b`, `b within a`) is a validation error that
  prints the chain.
- Step results report the candidate used at every level (frame, within and the
  element itself).

### Referring to targets

A step refers to a target in one of two ways:

- **By name** (`click: checkoutButton`): looked up in the test file's
  `targets`, then in the project's shared `*.targets.yaml` files. Shared target
  names must be unique across the project; a test file may not reuse a shared
  name (both are validation errors with both locations).
- **Inline** (`target:` followed by a list of candidates, or a long-form
  mapping), for one-off elements.

Names may contain dots as a naming convention (`cart.count`, `order.total`).

## Variables and interpolation

Values may contain `${namespace.path}`:

| Namespace | Contents                                                             | Writable |
| --------- | -------------------------------------------------------------------- | -------- |
| `vars`    | Test variables: from `vars:`, `set`, `extract`, `api`, flow outputs  | yes      |
| `env`     | The selected environment profile: `name`, `baseUrl` and its `values` | no       |
| `secrets` | Secrets declared in the config                                       | no       |
| `row`     | The current data row (tests with `data` only)                        | no       |
| `params`  | Parameters of the current flow (inside flow files only)              | no       |

Rules:

- If a value is exactly one `${…}`, it keeps its type (number, boolean, object).
  Otherwise the result is a string.
- Paths use dots: `${vars.order.items.0.name}`.
- `$${` writes a literal `${`.
- There are no expressions, operators or functions. Anything computed belongs in
  a user action.
- An unknown namespace is a validation error. An unknown `env` value or
  `secrets` name is a validation error.
- A `vars` name that was never set is a runtime error in `before` and `steps`,
  naming the step and listing the variables that do exist. **In `after` steps
  it is not an error:** the step is skipped and reported as
  "skipped: orderNumber was never set", because clean-up often depends on
  values the failed test never produced.
- **There is no access to the process environment.** `${env.…}` is the
  environment profile from the config. Environment variables reach a test only
  as declared secrets.
- Secret values are masked as `•••` everywhere the engine writes data
  ([ADR 0014](adr/0014-secret-masking.md)).

## Pages

Each test has a page called `main`. More pages are declared by name:

```yaml
pages:
  main: { login: customer }
  backoffice: { login: admin }
```

- Pages with the **same login** share one browser context (same cookies): use
  this for multi-tab flows.
- Pages with **different logins** get separate browser contexts: use this for
  multi-user flows.
- A declared page is opened on first use.

### New tabs and pop-ups: `opens`

Any step may name a page it opens:

```yaml
- click: order.printReceipt
  opens: receipt
- expect.visible: receipt.heading
  page: receipt
```

- During a step with `opens`, the engine watches the step page's browser
  context for a new page. When the action finishes it waits (within the step
  timeout) for that page and registers it under the given name; if none
  appears, the step fails with `PageNotOpened`.
- An `opens` name must be new in the test and may then be used by later steps'
  `page`. Using it before the step that opens it is a validation error.
- A new page that **no step names** still gets recorded under an automatic name
  (`tab-2`, `tab-3`, … in order of opening within the test) and the engine
  emits a warning with the step's location and the hint "Add `opens: <name>`
  to this step to use the new page". Automatic names appear in results only;
  step files cannot refer to them.

## Data rows

```yaml
data:
  - { user: alice, plan: free }
  - { user: bob, plan: pro }
# or a file in the repository, relative to this file:
data: ./data/users.csv
```

- The **whole test** runs once per row. Each row is its own test instance with
  its own `testId`, its own fresh browser contexts, its own `before` and
  `after`, and its own result.
- Supported files: CSV (first line is the header) and YAML (a list of
  mappings). Values are strings in CSV and keep their YAML types otherwise.
- `data` is allowed in tests only, not in flows.
- A value used in a URL or e-mail address should be URL-safe; add a column for
  it (like `sku` in the example above) rather than reusing a display name with
  spaces.

Repeating a flow per row inside a test (`forEach` on `call`) is **not** part of
v0.1.0. The format keeps room for it: `call` takes a mapping, so a later
`forEach` key would be a compatible addition ([ADR 0013](adr/0013-data-rows.md)).

## Flows

```yaml
# flows/checkout-as-guest.flow.yaml
version: 1
name: Check out as guest
params:
  email: { type: string }
  express: { type: boolean, default: false }
outputs: [orderNumber]
steps:
  - click: cart.checkout
  - fill: { target: checkout.email, value: '${params.email}' }
  - click: checkout.placeOrder
  - extract: { target: order.number, as: orderNumber }
```

- Parameter types: `string`, `number`, `boolean`. A missing required parameter
  or a wrong type is a validation error at the `call` step.
- A flow has its own `vars`. Only the names listed in `outputs` are copied back
  into the caller's `vars` when the flow finishes.
- Flows can call flows. A cycle (A calls B calls A) is a validation error that
  prints the chain.
- Flows may declare `targets`; they resolve like a test's.
- A flow has only `steps`: `before`, `after`, `data` and `skip` belong to tests.

## Project configuration

```yaml
# coffee.config.yaml
version: 1
tests: ['tests/**/*.test.yaml']
flows: ['flows/**/*.flow.yaml']
targets: ['targets/**/*.targets.yaml']
actions: ['actions/**/*.ts']

defaults:
  environment: local
  browser: chromium
  timeout: 10s
  testIdAttribute: data-testid
  fallbackGrace: 1s
  snapshots: always
  keepRuns: 20
  viewport: { width: 1280, height: 720 }
  locale: en-US
  timezone: UTC

environments:
  local:
    baseUrl: http://localhost:5173
    values:
      apiUrl: http://localhost:5173/api
  staging:
    baseUrl: https://staging.example.com
    locale: de-DE # overrides defaults.locale for this environment
    timezone: Europe/Berlin
    values:
      apiUrl: https://staging.example.com/api

secrets: [SHOP_API_TOKEN, SHOP_CUSTOMER_PASSWORD, SHOP_ADMIN_PASSWORD]

logins:
  customer:
    flow: flows/login.flow.yaml
    with: { user: alice@example.com, password: '${secrets.SHOP_CUSTOMER_PASSWORD}' }
  admin:
    flow: flows/login.flow.yaml
    with: { user: admin@example.com, password: '${secrets.SHOP_ADMIN_PASSWORD}' }
    maxAge: 2h
```

`defaults.browser` is validated against the browsers the engine reports in
`capabilities.browsers` (only `chromium` in v0.1.0); an unsupported value is a
config error listing the supported ones.

### Viewport, locale and timezone

So that runs look the same on every machine, every browser context is created
with a fixed viewport, locale and timezone, also in headed mode:

| Setting    | Default                        | Overridable in an environment |
| ---------- | ------------------------------ | ----------------------------- |
| `viewport` | `{ width: 1280, height: 720 }` | yes                           |
| `locale`   | `en-US`                        | yes                           |
| `timezone` | `UTC`                          | yes                           |

The device scale factor is fixed at 1 so screenshots have the same pixel size
everywhere. The values used are reported in `runStarted`.

### Other defaults

| Setting           | Default       | Overridable in an environment | Meaning                                                                                                                |
| ----------------- | ------------- | ----------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `timeout`         | `10s`         | yes                           | Default step timeout                                                                                                   |
| `testIdAttribute` | `data-testid` | no                            | Attribute used by `testId` candidates                                                                                  |
| `fallbackGrace`   | `1s`          | yes                           | How long only a target's first candidate is tried before fallbacks ([ADR 0010](adr/0010-locator-candidates.md))        |
| `snapshots`       | `always`      | yes                           | `always`, `onFailure` or `off` ([ADR 0007](adr/0007-page-snapshot-format.md))                                          |
| `keepRuns`        | `20`          | no                            | Run folders kept; older ones are deleted at the start of a run; `0` keeps all ([ADR 0015](adr/0015-results-layout.md)) |

### Environments

`ctx.env` and `${env.…}` are the selected environment profile
(`--env staging`, or `defaults.environment`). Relative URLs in `goto` and `api`
resolve against `env.baseUrl`. Environment values are not secret and are
committed; they cannot read process environment variables.

### Secrets

Secrets are only named in the config. Their values come from the process
environment variable of the same name, or a git-ignored `.env` file at the
project root. A secret that is declared but has no value fails validation of
the tests that use it, naming the variable to set. Secrets shorter than 4
characters are rejected, because masking them would damage ordinary output
([ADR 0014](adr/0014-secret-masking.md)).

### Saved logins

A saved login is a named flow that signs in. The engine runs it, saves the
browser storage state, and reuses it for every page that names the login
([ADR 0018](adr/0018-saved-logins.md)).

| Key      | Meaning                                                            |
| -------- | ------------------------------------------------------------------ |
| `flow`   | The login flow                                                     |
| `with`   | Parameters for the flow                                            |
| `maxAge` | How long a saved state may be reused: `30m`, `12h` (default `12h`) |

- The saved state is stored under a **cache key**: an HMAC-SHA-256 of the
  environment name, the login name, the content of the login flow file and of
  every flow it calls, and the resolved `with` values (including secrets). The
  HMAC key is a random key kept in the data folder
  ([ADR 0018](adr/0018-saved-logins.md)). Only the cache key is stored, never
  the parameter values. Changing a password, a flow or the environment
  therefore produces a new cache key, and the old state is not used.
- A saved state older than `maxAge` is not used; the flow runs again and
  replaces it. `--refresh-logins` forces this for the whole run.
- A test with **`freshLogin: true`** signs in from scratch for every page that
  has a login, without reading the saved state and without writing a new one.
  Use it for tests of the login itself or of session handling.

## Execution rules

1. A test with `skip` is validated and reported as skipped, and nothing else
   happens.
2. Each test instance (one per data row) gets a fresh browser context per login.
3. `before` steps run, then `steps`. The first failure stops the remaining
   steps; they are reported as skipped.
4. `after` steps always run: after success, failure or cancellation. Each `after`
   step runs even if a previous one failed; all failures are reported. An
   `after` step that uses a variable that was never set is skipped with that
   reason, not failed.
5. A failure in `before` is reported as a setup failure.
6. Actions auto-wait up to the step timeout; `expect.*` actions retry until they
   pass or time out. Every wait stops promptly when the run is cancelled.

## Validation errors

The engine validates against the schema before running anything and reports
every problem at once, each with file, line and column:

```
tests/checkout/guest-checkout.test.yaml:21:5  error  UnknownAction
  "exepct.text" is not an action. Did you mean "expect.text"?

tests/checkout/guest-checkout.test.yaml:24:7  error  MissingParameter
  "fill" needs "value". Add it under line 24, for example:  value: ${vars.email}

flows/login.flow.yaml:3:1  error  FlowCycle
  Flow calls itself: login.flow.yaml → session.flow.yaml → login.flow.yaml
```

The JSON Schema generated from the step-file schema is published so editors
(for example VS Code with the YAML extension) can offer completion and inline
errors.

## Editing by tools

The recorder and the desktop app edit step files through the `yaml` library's
document model, which keeps comments, key order and formatting. A file edited in
the desktop app should produce a minimal Git diff. Tools write the form the user
wrote (shorthand stays shorthand); normalisation happens only in memory.

## Not in v0.1.0

These are **deliberate gaps**, not oversights. Each can be added later without
breaking existing step files.

| Gap                                            | What happens in v0.1.0                                                                                       |
| ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Browser dialogs (`alert`, `confirm`, `prompt`) | Playwright's default: dialogs are dismissed automatically. No action accepts them or checks their text.      |
| Downloads                                      | No action waits for or checks a download.                                                                    |
| Scrolling                                      | No scroll action. Playwright scrolls elements into view before acting on them.                               |
| Typing key by key                              | `fill` sets the whole value at once; `press` sends one key or chord. No action types text one key at a time. |
| Assertions that continue after failure         | Every failing `expect.*` stops the test (no "soft" assertions).                                              |
| Retries                                        | A failed test is not run again automatically.                                                                |
| Whole-test timeout                             | Only per-step timeouts; a test has no overall time limit.                                                    |
| Other named pages inside an action             | `ctx.page` is the step's page only; an action cannot reach other named pages.                                |
| Stable IDs for data rows                       | A row is identified by its position (`#0`, `#1`); reordering rows changes `testId`s.                         |
