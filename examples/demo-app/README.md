# Demo app

A small local web app that the integration tests run real step files against,
and the project (`cfe.config.yaml`, `tests/`, `fixtures/`, `actions/`) that
holds those step files.

## The web server

`server/server.ts` is a Node web server with no framework and no dependency.
Node runs the TypeScript file directly, so there is no build step:

```sh
node examples/demo-app/server/server.ts              # http://127.0.0.1:4310
node examples/demo-app/server/server.ts --port 0     # a free port
```

Once listening it prints `Demo app listening on http://127.0.0.1:<port>`. All
state lives in memory and is lost when the server stops.

The saved logins `customer` (user `alice`) and `admin` (user `ada`) sign in
through `flows/login.flow.yaml` with the secret `DEMO_PASSWORD`. Its value
comes from `examples/demo-app/.env`, which is committed so the demo opens
without setup. That file is for the demo only and protects nothing: the value
is made up, and the demo server accepts any password unless it is started with
`DEMO_PASSWORD` itself. A real project keeps its `.env` out of Git; this is the
one `.env` the repository allows. An environment variable `DEMO_PASSWORD`
still takes precedence, as for any secret.

The test harness (`packages/engine/src/testing/demo-app.ts`) starts it on a
free port, copies this project to a temporary folder with
`http://localhost:4310` in the config replaced by the server's URL, starts the
engine as a child process and opens the copy.

## Pages

Each page is plain HTML with stable roles, labels and test IDs. Later plan
branches add the pages their own samples need.

| Path            | Used by                  | What is on it                                                                                                                                                                                                                                                                                                                                                                                                               |
| --------------- | ------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/`             | —                        | Links to every page                                                                                                                                                                                                                                                                                                                                                                                                         |
| `/todos`        | S10, S13, F1, F2, F3, F7 | A to-do list kept on the server: field labelled "New to-do" (`new-todo`), checkbox "Done", button "Add", count (`todo-count`), list "To-dos" (`todo-list`) of items (`todo-item`, text `<title>` or `<title> (done)`), button "Clear all" (calls `POST /api/reset`, so `after` steps can reset the app)                                                                                                                     |
| `/fallback`     | S11                      | Button with test ID `place-order` whose name changed from "Place order" to "Submit order"; clicking it sets the status (`order-status`) to "Order placed". Total in `order-total`                                                                                                                                                                                                                                           |
| `/slow-render`  | S12                      | A placeholder `div.checkout-button` at once, replaced after 500 ms by the button "Check out" (also `.checkout-button`); clicking it sets the status (`checkout-status`) to "Checked out"                                                                                                                                                                                                                                    |
| `/frames`       | S14                      | `iframe#checkout` (title "Checkout", page `/frames/checkout`), which holds the iframe titled "Secure payment" (page `/frames/payment`) with the field "Card number" (`card-number`), the button "Pay" and a status (`payment-status`)                                                                                                                                                                                       |
| `/tabs`         | S7                       | Heading "Tabs", a link "Open receipt" that opens `/receipt` (heading "Receipt", total in `receipt-total`) in a new tab, and a button "Open help" that opens `/help` (heading "Help") with `window.open`                                                                                                                                                                                                                     |
| `/settings`     | S17                      | The browser's viewport as `<width>×<height>` (`viewport`), its locale (`locale`) and its timezone (`timezone`)                                                                                                                                                                                                                                                                                                              |
| `/login`        | S8, S18, I11             | Fields "Username" and "Password" and a button "Sign in"; users `alice` and `ada` (any password, unless the server runs with `DEMO_PASSWORD`, which it then requires); success opens `/account`                                                                                                                                                                                                                              |
| `/account`      | S8, S18                  | "Signed in as" the user of the session cookie (`account-user`), or "Not signed in"                                                                                                                                                                                                                                                                                                                                          |
| `/form`         | S2                       | An order form: fields "Name" and "Search", selects "Country" (Germany `de`, France `fr`) and "Toppings" (multiple: `cheese`, `olives`, `ham`), checkbox "Subscribe to the newsletter", file input "Attachment", radio buttons "Standard" and "Express" (group "Delivery"), button "Send order". Enter in "Search" writes "Searching for <text>" (`search-result`); sending writes a summary of every value (`form-summary`) |
| `/interactions` | S3                       | Button "Help" whose tooltip (`tooltip`) gets its text and shows on hover; a list "Fruit" (`fruit-apple`, `fruit-banana`, `fruit-cherry`) sorted by dragging an item onto another, with the order in `fruit-order`                                                                                                                                                                                                           |
| `/products`     | S16                      | A table "Products" with rows "Desk lamp", "Office chair" and "Notebook", each with a "Delete" button that removes the row; the number left is in `product-count`                                                                                                                                                                                                                                                            |
| `/orders`       | S4                       | A button "Load orders" that fetches `GET /api/orders` and lists each order's item in the list "Orders"; the summary (`order-summary`) says "<n> orders loaded"                                                                                                                                                                                                                                                              |
| `/notes`        | S9                       | A field "Note" and a button "Show note" that writes the note into `note-shown`                                                                                                                                                                                                                                                                                                                                              |
| `/token`        | S19                      | A button "Get token" that calls `POST /api/token` and then says "Token received" (`token-status`)                                                                                                                                                                                                                                                                                                                           |
| `/details`      | recorder tests           | Two sections, "Shipping" and "Returns", each with a button "Details" (`shipping-details`, `returns-details`) that writes "<section> details" into `details-shown`; a list "Addresses" with items "Home" and "Office", each with a "Delete" button that removes it; two colour swatches (`div.swatch`, no text) that write "Red chosen" or "Blue chosen"                                                                     |

Test IDs use the attribute `data-testid`.

## API

| Request               | Answer                                                                                                                                                                      |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /api/todos`      | The to-do items, `[{ "title": "…", "done": false }]`                                                                                                                        |
| `POST /api/todos`     | Adds `{ "title": "…", "done": true }` (`done` optional) and answers with all items; 400 for a bad body                                                                      |
| `POST /api/reset`     | Empties the list and counts one reset; answers `{ "todos": [], "resets": <n> }`. `after` steps call it                                                                      |
| `GET /api/state`      | `{ "todos": […], "resets": <n> }`, so a test can prove its `after` steps ran                                                                                                |
| `POST /api/login`     | Signs in `{ "username": "…", "password": "…" }` and sets the session cookie; 401 when the user is unknown                                                                   |
| `GET /api/logins`     | `{ "logins": <n> }`: how many sign-ins succeeded, so a test can see whether a login flow ran                                                                                |
| `GET /api/orders`     | Two orders, `[{ "id": 1, "item": "Desk lamp" }, { "id": 2, "item": "Notebook" }]`                                                                                           |
| `GET /api/whoami`     | `{ "user": "…" }` for a signed-in session whose request carries `X-Demo-Password` (the value of `DEMO_PASSWORD` when the server has one, otherwise anything); 401 otherwise |
| `POST /api/token`     | Issues a token in the response header `Authorization: Bearer <token>`; answers `{ "issued": true }`                                                                         |
| `GET /api/protected`  | `{ "ok": true }` with `Authorization: Bearer <a token POST /api/token issued>`; 401 otherwise                                                                               |
| `GET /api/token/last` | `{ "token": "…" }`: the last token issued, so a test can check that it appears nowhere in the output                                                                        |

Anything else is a 404 in plain text.
