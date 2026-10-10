// The demo app's HTML pages: plain HTML with stable roles, labels and test IDs,
// one page per group of samples (examples/demo-app/README.md lists them).
// Later plan branches add the pages their own samples need.

/** Wraps a page body in a complete HTML document. */
function document(title: string, body: string): string {
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>${title}</title>
</head>
<body>
${body}
</body>
</html>
`;
}

/** `/`: links to every page. */
export const homePage = document(
  'Demo app',
  `<h1>Demo app</h1>
<nav aria-label="Pages">
  <ul>
    <li><a href="/todos">To-dos</a></li>
    <li><a href="/fallback">Locator fallback</a></li>
    <li><a href="/slow-render">Slow render</a></li>
    <li><a href="/frames">Frames</a></li>
    <li><a href="/tabs">Tabs</a></li>
    <li><a href="/settings">Browser settings</a></li>
    <li><a href="/login">Sign in</a></li>
    <li><a href="/form">Order form</a></li>
    <li><a href="/interactions">Interactions</a></li>
    <li><a href="/products">Products</a></li>
  </ul>
</nav>`,
);

/**
 * `/todos`: a to-do list kept on the server, so `after` steps can reset it
 * (S10, S13, F1, F2, F3, F7).
 */
export const todosPage = document(
  'To-dos',
  `<h1>To-dos</h1>
<form id="add-form">
  <label for="new-todo">New to-do</label>
  <input id="new-todo" name="title" data-testid="new-todo" required>
  <label><input type="checkbox" id="new-done" name="done"> Done</label>
  <button type="submit">Add</button>
</form>
<p>Items: <span data-testid="todo-count">0</span></p>
<button type="button" id="clear-all">Clear all</button>
<ul aria-label="To-dos" data-testid="todo-list"></ul>
<script>
  const list = document.querySelector('[data-testid="todo-list"]');
  const count = document.querySelector('[data-testid="todo-count"]');
  function render(todos) {
    list.replaceChildren(
      ...todos.map((todo) => {
        const item = document.createElement('li');
        item.textContent = todo.title + (todo.done ? ' (done)' : '');
        item.dataset.testid = 'todo-item';
        return item;
      }),
    );
    count.textContent = String(todos.length);
  }
  fetch('/api/todos').then((response) => response.json()).then(render);
  // Resets the server's state, as POST /api/reset does: after steps click it.
  document.getElementById('clear-all').addEventListener('click', async () => {
    const response = await fetch('/api/reset', { method: 'POST' });
    render((await response.json()).todos);
  });
  document.getElementById('add-form').addEventListener('submit', async (event) => {
    event.preventDefault();
    const title = document.getElementById('new-todo');
    const done = document.getElementById('new-done');
    const response = await fetch('/api/todos', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ title: title.value, done: done.checked }),
    });
    render(await response.json());
    title.value = '';
    done.checked = false;
  });
</script>`,
);

/**
 * `/fallback`: the button's accessible name changed from "Place order" to
 * "Submit order" but its test ID did not, so a target whose first candidate is
 * the old role and name must fall back to the test ID (S11).
 */
export const fallbackPage = document(
  'Locator fallback',
  `<h1>Order summary</h1>
<p>Total: <span data-testid="order-total">€20.00</span></p>
<button type="button" data-testid="place-order">Submit order</button>
<p role="status" data-testid="order-status"></p>
<script>
  document.querySelector('[data-testid="place-order"]').addEventListener('click', () => {
    document.querySelector('[data-testid="order-status"]').textContent = 'Order placed';
  });
</script>`,
);

/**
 * `/slow-render`: shows a placeholder that matches the CSS fallback
 * `.checkout-button` at once, and replaces it with the real "Check out" button
 * after 500 ms (S12).
 */
export const slowRenderPage = document(
  'Slow render',
  `<h1>Cart</h1>
<div class="checkout-button placeholder" aria-busy="true">Loading…</div>
<p role="status" data-testid="checkout-status"></p>
<script>
  setTimeout(() => {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'checkout-button';
    button.textContent = 'Check out';
    button.addEventListener('click', () => {
      document.querySelector('[data-testid="checkout-status"]').textContent = 'Checked out';
    });
    document.querySelector('.placeholder').replaceWith(button);
  }, 500);
</script>`,
);

/** `/frames`: a checkout iframe that holds a payment iframe (S14). */
export const framesPage = document(
  'Frames',
  `<h1>Checkout with frames</h1>
<iframe id="checkout" title="Checkout" src="/frames/checkout" width="600" height="300"></iframe>`,
);

/** `/frames/checkout`: the outer frame; it holds the payment frame. */
export const checkoutFramePage = document(
  'Checkout',
  `<h2>Checkout</h2>
<iframe title="Secure payment" src="/frames/payment" width="500" height="200"></iframe>`,
);

/** `/frames/payment`: the inner frame, with the field S14 fills. */
export const paymentFramePage = document(
  'Secure payment',
  `<h3>Secure payment</h3>
<label for="card-number">Card number</label>
<input id="card-number" data-testid="card-number" autocomplete="off">
<button type="button">Pay</button>
<p role="status" data-testid="payment-status"></p>
<script>
  document.querySelector('button').addEventListener('click', () => {
    const digits = document.getElementById('card-number').value.replace(/\\D/g, '');
    document.querySelector('[data-testid="payment-status"]').textContent =
      'Paid with card ending ' + digits.slice(-4);
  });
</script>`,
);

/** `/tabs`: a link that opens a receipt in a new tab, and a button that opens help (S7). */
export const tabsPage = document(
  'Tabs',
  `<h1>Tabs</h1>
<p><a href="/receipt" target="_blank">Open receipt</a></p>
<button type="button" id="open-help">Open help</button>
<script>
  document.getElementById('open-help').addEventListener('click', () => {
    window.open('/help', '_blank');
  });
</script>`,
);

/** `/receipt`: the page S7 opens as a named tab. */
export const receiptPage = document(
  'Receipt',
  `<h1>Receipt</h1>
<p>Total: <span data-testid="receipt-total">€20.00</span></p>`,
);

/** `/help`: the page S7 opens without naming it. */
export const helpPage = document('Help', '<h1>Help</h1>');

/** `/settings`: shows the browser's viewport, locale and timezone (S17). */
export const settingsPage = document(
  'Browser settings',
  `<h1>Browser settings</h1>
<dl>
  <dt>Viewport</dt><dd data-testid="viewport"></dd>
  <dt>Locale</dt><dd data-testid="locale"></dd>
  <dt>Timezone</dt><dd data-testid="timezone"></dd>
</dl>
<script>
  document.querySelector('[data-testid="viewport"]').textContent =
    window.innerWidth + '×' + window.innerHeight;
  document.querySelector('[data-testid="locale"]').textContent = navigator.language;
  document.querySelector('[data-testid="timezone"]').textContent =
    Intl.DateTimeFormat().resolvedOptions().timeZone;
</script>`,
);

/** `/login`: signs in with a user name and a password (S8, S18, I11). */
export const loginPage = document(
  'Sign in',
  `<h1>Sign in</h1>
<form id="login-form">
  <label for="username">Username</label>
  <input id="username" name="username" autocomplete="username">
  <label for="password">Password</label>
  <input id="password" name="password" type="password" autocomplete="current-password">
  <button type="submit">Sign in</button>
</form>
<p role="alert" data-testid="login-error"></p>
<script>
  document.getElementById('login-form').addEventListener('submit', async (event) => {
    event.preventDefault();
    const response = await fetch('/api/login', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        username: document.getElementById('username').value,
        password: document.getElementById('password').value,
      }),
    });
    if (response.ok) {
      location.href = '/account';
    } else {
      document.querySelector('[data-testid="login-error"]').textContent = 'Wrong user or password.';
    }
  });
</script>`,
);

/**
 * `/account`: who is signed in, from the session cookie.
 *
 * @param user - The signed-in user, HTML-escaped; empty when nobody is.
 * @returns The page.
 */
export function accountPage(user: string): string {
  return document(
    'Account',
    user === ''
      ? '<h1>Account</h1>\n<p>Not signed in. <a href="/login">Sign in</a></p>'
      : `<h1>Account</h1>\n<p>Signed in as <span data-testid="account-user">${user}</span></p>`,
  );
}

/**
 * `/form`: an order form for the interaction actions (S2): a text field, a
 * select, a multi-select, a checkbox, a file input and a search field that
 * reacts to Enter. Sending the form writes a summary of every value.
 */
export const formPage = document(
  'Order form',
  `<h1>Order form</h1>
<form id="order-form">
  <p><label for="name">Name</label> <input id="name" name="name"></p>
  <p><label for="country">Country</label>
    <select id="country" name="country">
      <option value="">Choose…</option>
      <option value="de">Germany</option>
      <option value="fr">France</option>
    </select></p>
  <p><label for="toppings">Toppings</label>
    <select id="toppings" name="toppings" multiple>
      <option value="cheese">Cheese</option>
      <option value="olives">Olives</option>
      <option value="ham">Ham</option>
    </select></p>
  <p><label><input type="checkbox" id="subscribe" name="subscribe"> Subscribe to the newsletter</label></p>
  <p><label for="attachment">Attachment</label> <input type="file" id="attachment" name="attachment" multiple></p>
  <p><label for="search">Search</label> <input id="search" type="search" name="search"></p>
  <p role="status" data-testid="search-result"></p>
  <button type="submit">Send order</button>
</form>
<p data-testid="form-summary"></p>
<script>
  document.getElementById('search').addEventListener('keydown', (event) => {
    if (event.key !== 'Enter') return;
    event.preventDefault();
    document.querySelector('[data-testid="search-result"]').textContent =
      'Searching for ' + event.target.value;
  });
  document.getElementById('order-form').addEventListener('submit', (event) => {
    event.preventDefault();
    const toppings = [...document.getElementById('toppings').selectedOptions].map((o) => o.value);
    const files = [...document.getElementById('attachment').files].map((f) => f.name);
    document.querySelector('[data-testid="form-summary"]').textContent = [
      document.getElementById('name').value || 'no name',
      document.getElementById('country').value || 'no country',
      toppings.join('+') || 'no toppings',
      document.getElementById('subscribe').checked ? 'subscribed' : 'not subscribed',
      files.join(' ') || 'no files',
    ].join(', ');
  });
</script>`,
);

/**
 * `/interactions`: a button with a tooltip shown on hover, and a list sorted by
 * drag and drop (HTML5), for `hover` and `drag` (S3).
 */
export const interactionsPage = document(
  'Interactions',
  `<h1>Interactions</h1>
<p><button type="button" id="help">Help</button>
  <span role="tooltip" data-testid="tooltip" hidden></span></p>
<ul aria-label="Fruit" data-testid="fruit-list">
  <li draggable="true" data-testid="fruit-apple">Apple</li>
  <li draggable="true" data-testid="fruit-banana">Banana</li>
  <li draggable="true" data-testid="fruit-cherry">Cherry</li>
</ul>
<p>Order: <span data-testid="fruit-order">Apple, Banana, Cherry</span></p>
<script>
  const help = document.getElementById('help');
  const tooltip = document.querySelector('[data-testid="tooltip"]');
  // The tooltip gets its text on the first hover, so a test can see the hover happened.
  help.addEventListener('mouseenter', () => {
    tooltip.textContent = 'Opens the help pages';
    tooltip.hidden = false;
  });
  help.addEventListener('mouseleave', () => { tooltip.hidden = true; });

  const list = document.querySelector('[data-testid="fruit-list"]');
  const order = document.querySelector('[data-testid="fruit-order"]');
  let dragged;
  list.addEventListener('dragstart', (event) => { dragged = event.target; });
  list.addEventListener('dragover', (event) => { event.preventDefault(); });
  list.addEventListener('drop', (event) => {
    event.preventDefault();
    const onto = event.target.closest('li');
    if (dragged && onto && dragged !== onto) list.insertBefore(dragged, onto);
    order.textContent = [...list.children].map((item) => item.textContent).join(', ');
  });
</script>`,
);

/**
 * `/products`: a table of products, each row with its own "Delete" button
 * (S16: the Delete button of the row named after the data row, through
 * `within`). Deleting removes the row from the page only.
 */
export const productsPage = document(
  'Products',
  `<h1>Products</h1>
<table aria-label="Products">
  <thead><tr><th>Product</th><th>Price</th><th></th></tr></thead>
  <tbody>
    <tr><td>Desk lamp</td><td>€25</td><td><button type="button">Delete</button></td></tr>
    <tr><td>Office chair</td><td>€120</td><td><button type="button">Delete</button></td></tr>
    <tr><td>Notebook</td><td>€3</td><td><button type="button">Delete</button></td></tr>
  </tbody>
</table>
<p>Products left: <span data-testid="product-count">3</span></p>
<script>
  const body = document.querySelector('tbody');
  body.addEventListener('click', (event) => {
    const button = event.target.closest('button');
    if (!button) return;
    button.closest('tr').remove();
    document.querySelector('[data-testid="product-count"]').textContent = String(body.rows.length);
  });
</script>`,
);
