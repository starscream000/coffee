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

The test harness (`packages/engine/src/testing/demo-app.ts`) starts it on a
free port, copies this project to a temporary folder with
`http://localhost:4310` in the config replaced by the server's URL, starts the
engine as a child process and opens the copy.

## Pages

Each page is plain HTML with stable roles, labels and test IDs. Later plan
branches add the pages their own samples need.

| Path           | Used by                  | What is on it                                                                                                                                                                                                                                                                                           |
| -------------- | ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/`            | —                        | Links to every page                                                                                                                                                                                                                                                                                     |
| `/todos`       | S10, S13, F1, F2, F3, F7 | A to-do list kept on the server: field labelled "New to-do" (`new-todo`), checkbox "Done", button "Add", count (`todo-count`), list "To-dos" (`todo-list`) of items (`todo-item`, text `<title>` or `<title> (done)`), button "Clear all" (calls `POST /api/reset`, so `after` steps can reset the app) |
| `/fallback`    | S11                      | Button with test ID `place-order` whose name changed from "Place order" to "Submit order"; clicking it sets the status (`order-status`) to "Order placed". Total in `order-total`                                                                                                                       |
| `/slow-render` | S12                      | A placeholder `div.checkout-button` at once, replaced after 500 ms by the button "Check out" (also `.checkout-button`); clicking it sets the status (`checkout-status`) to "Checked out"                                                                                                                |
| `/frames`      | S14                      | `iframe#checkout` (title "Checkout", page `/frames/checkout`), which holds the iframe titled "Secure payment" (page `/frames/payment`) with the field "Card number" (`card-number`), the button "Pay" and a status (`payment-status`)                                                                   |

Test IDs use the attribute `data-testid`.

## API

| Request           | Answer                                                                                                 |
| ----------------- | ------------------------------------------------------------------------------------------------------ |
| `GET /api/todos`  | The to-do items, `[{ "title": "…", "done": false }]`                                                   |
| `POST /api/todos` | Adds `{ "title": "…", "done": true }` (`done` optional) and answers with all items; 400 for a bad body |
| `POST /api/reset` | Empties the list and counts one reset; answers `{ "todos": [], "resets": <n> }`. `after` steps call it |
| `GET /api/state`  | `{ "todos": […], "resets": <n> }`, so a test can prove its `after` steps ran                           |

Anything else is a 404 in plain text.
