# Step file format

> Status: **Proposal** (Milestone 0). Open questions are marked **(Open)** and
> numbered as in [architecture.md](architecture.md#open-questions).

Step files are YAML documents in the user's Git repository. They are the source
of truth: the engine, the CLI and the desktop app only read and write them. The
format is designed to be readable in a code review and stable under
re-recording.

## File kinds

The file name decides the kind:

| Pattern                | Kind    | Contains                                              |
| ---------------------- | ------- | ----------------------------------------------------- |
| `*.test.yaml`          | test    | One test: pages, data, before, steps, after           |
| `*.flow.yaml`          | flow    | A reusable sequence of steps with parameters          |
| `*.targets.yaml`       | targets | Shared, named targets (element locators)              |
| `testtool.config.yaml` | config  | Project settings, environments, logins (one per repo) |

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
  - { product: Desk lamp, price: '€20.00' }
  - { product: Chair, price: '€45.00' }

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
        email: guest+${row.product}@example.com
  - expect.text: { target: order.total, equals: '${row.price}' }
  - extract: { target: order.number, as: orderNumber }

after:
  - api:
      method: DELETE
      url: ${env.apiUrl}/orders/${vars.orderNumber}
      headers: { Authorization: 'Bearer ${secrets.SHOP_API_TOKEN}' }
```

## Test files

| Key           | Required | Meaning                                                                    |
| ------------- | -------- | -------------------------------------------------------------------------- |
| `version`     | yes      | Format version, currently `1`                                              |
| `name`        | yes      | Human-readable name. May use `${row.…}` so data rows have distinct names   |
| `description` | no       | Free text                                                                  |
| `tags`        | no       | List of strings, used to select tests (`--tag smoke`)                      |
| `login`       | no       | Saved login for the default page `main` (shorthand for `pages.main.login`) |
| `pages`       | no       | Named pages for multi-tab or multi-user flows, see [Pages](#pages)         |
| `data`        | no       | Data rows; the test runs once per row, see [Data rows](#data-rows)         |
| `vars`        | no       | Initial variables                                                          |
| `targets`     | no       | Targets used only in this file                                             |
| `before`      | no       | Setup steps                                                                |
| `steps`       | yes      | The test itself                                                            |
| `after`       | no       | Clean-up steps; **always run**, see [Execution rules](#execution-rules)    |

## Steps

Every step calls exactly one action. A step is one of:

```yaml
- back # action without parameters
- goto: /products # action with its shorthand parameter
- fill: # action with named parameters
    target: checkout.email
    value: ${vars.email}
```

Besides the action key, a step may have these common keys:

| Key       | Meaning                                                           |
| --------- | ----------------------------------------------------------------- |
| `name`    | Label shown in results instead of the generated one               |
| `page`    | Named page the action runs on (default `main`)                    |
| `timeout` | Overrides the default timeout for this step: `500ms`, `10s`, `2m` |

```yaml
- click: approveOrder
  page: backoffice
  timeout: 20s
  name: Admin approves the order
```

A mapping with zero or two action keys is a validation error. Unknown keys are
errors, with a "did you mean" hint where one is close (`exepct.text` → `expect.text`).

Action names are listed in [actions.md](actions.md). Names with a dot
(`expect.text`, `wait.url`) are ordinary names; the dot only groups related
actions.

There is deliberately **no `if`, `else`, `while` or free loop**. The only ways to
reuse or repeat steps are calling a flow and repeating for each data row.

## Targets

A target describes one element. It stores several **locator candidates in order
of reliability**; the engine tries them in that order (see
[ADR 0010](adr/0010-locator-candidates.md)).

```yaml
checkoutButton:
  - role: button # 1. ARIA role and accessible name
    name: Check out
  - testId: checkout # 2. test ID attribute (data-testid by default)
  - css: '#cart .btn-primary' # 3. CSS, last resort
```

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
nothing else is unique.

A step refers to a target in one of two ways:

- **By name** (`click: checkoutButton`): looked up in the test file's
  `targets`, then in the project's shared `*.targets.yaml` files. Shared target
  names must be unique across the project; a test file may not reuse a shared
  name (both are validation errors with both locations).
- **Inline** (`target:` followed by a list of candidates), for one-off elements.

Names may contain dots as a naming convention (`cart.count`, `order.total`).

## Variables and interpolation

Values may contain `${namespace.path}`:

| Namespace | Contents                                                            | Writable |
| --------- | ------------------------------------------------------------------- | -------- |
| `vars`    | Test variables: from `vars:`, `set`, `extract`, `api`, flow outputs | yes      |
| `env`     | The selected environment profile: `name`, `baseUrl` and its values  | no       |
| `secrets` | Secrets declared in the config, read from the process environment   | no       |
| `row`     | The current data row                                                | no       |
| `params`  | Parameters of the current flow (inside flow files only)             | no       |

Rules:

- If a value is exactly one `${…}`, it keeps its type (number, boolean, object).
  Otherwise the result is a string.
- Paths use dots: `${vars.order.items.0.name}`.
- `$${` writes a literal `${`.
- There are no expressions, operators or functions. Anything computed belongs in
  a user action.
- An unknown namespace is a validation error. An unknown `env` or `secrets`
  name is a validation error when the config is known. An unknown `vars` name is
  a runtime error naming the step and listing the variables that do exist.
- Secret values are masked as `•••` everywhere the engine writes text.

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
- A page is opened on first use.
- **(Open 6)** A click that opens a new tab names it with `opens: <pageName>`.

## Data rows

```yaml
data:
  - { user: alice, plan: free }
  - { user: bob, plan: pro }
# or a file in the repository, relative to this file:
data: ./data/users.csv
```

The test runs once per row. Each row is a separate result with its own fresh
browser context; `before` and `after` run for every row. Supported files: CSV
(first line is the header) and YAML (a list of mappings). Values are strings in
CSV and keep their YAML types otherwise.

**(Open 3)** A `call` step may also repeat a flow once per row with `forEach`
(see [actions.md](actions.md#call)).

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
- A flow has only `steps`: `before` and `after` belong to tests.

## Project configuration

```yaml
# testtool.config.yaml
version: 1
tests: ['tests/**/*.test.yaml']
flows: ['flows/**/*.flow.yaml']
targets: ['targets/**/*.targets.yaml']
actions: ['actions/**/*.ts']

defaults:
  timeout: 10s
  testIdAttribute: data-testid

environments:
  local:
    baseUrl: http://localhost:5173
    values:
      apiUrl: http://localhost:5173/api
  staging:
    baseUrl: https://staging.example.com
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
```

### Environments

**(Open 2)** `ctx.env` and `${env.…}` are the selected environment profile
(`--env staging`), not the raw process environment. Relative URLs in `goto`
and `api` resolve against `env.baseUrl`. Environment values are not secret and
are committed; secrets are only ever named here and read from the process
environment (or a git-ignored `.env`).

### Saved logins

A saved login is a named flow that signs in. The engine runs it once, saves the
browser storage state to `.testtool/logins/<env>/<login>.json` (git-ignored) and
reuses it for every page that names the login. It is refreshed when it is
missing, when the flow file changes, or on `--refresh-logins`.

## Execution rules

1. Each test (and each data row) gets a fresh browser context per login.
2. `before` steps run, then `steps`. The first failure stops the remaining
   steps; they are reported as skipped.
3. `after` steps always run: after success, failure or cancellation. Each `after`
   step runs even if a previous one failed; all failures are reported.
4. A failure in `before` is reported as a setup failure.
5. Actions auto-wait up to the step timeout (Playwright semantics); `expect.*`
   actions retry until they pass or time out.

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
the desktop app should produce a minimal Git diff.
