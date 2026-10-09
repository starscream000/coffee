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
