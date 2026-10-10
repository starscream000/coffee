// Integration tests of the recorder (instruction 0009, task 13): a person's
// interactions on the demo app, made with real mouse moves and clicks at
// coordinates and real key presses (never locator actions), recorded into a
// test file. Each test checks the file as written and that verify passes; the
// login tests search the file, the session's events, the command's output and
// the run folder for the typed password.
import { readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Locator, Page } from 'playwright';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { BUILTIN_ACTIONS } from '../actions/builtins/index.js';
import { Project } from '../project/project.js';
import { startDemoProject, type DemoProject } from '../testing/demo-app.js';
import type { CheckStats } from './candidates.js';
import { formatNotice, formatStep, formatVerify } from './dev-record.js';
import type { RecordedStep, RecordedTarget } from './document.js';
import { RecordingSession, type RecordingNotice } from './session.js';
import { verifyRecording, type VerifyResult } from './verify.js';

/** The demo project's declared secret, from its committed .env. */
const DEMO_PASSWORD = 'demo-only-not-a-secret';

let demo: DemoProject;
let project: Project;
let nextFile = 0;
/** Candidates proposed and rejected over every recording, for the report. */
const totals: CheckStats[] = [];

beforeAll(async () => {
  demo = await startDemoProject();
  project = await Project.open(demo.root, BUILTIN_ACTIONS, { environment: {} });
});
afterAll(async () => {
  const sum = (key: 'proposed' | 'rejected'): Record<string, number> => {
    const result: Record<string, number> = {};
    for (const stats of totals) {
      for (const [kind, count] of Object.entries(stats[key])) {
        result[kind] = (result[kind] ?? 0) + count;
      }
    }
    return result;
  };
  console.log(`Candidates proposed: ${JSON.stringify(sum('proposed'))}`);
  console.log(`Candidates rejected: ${JSON.stringify(sum('rejected'))}`);
  await demo.close();
});

/** A recording, everything it told its listener, and the lines `pnpm record` prints. */
interface Recorded {
  readonly session: RecordingSession;
  readonly file: string;
  readonly events: {
    kind: string;
    index: number;
    step: RecordedStep;
    targets?: Readonly<Record<string, RecordedTarget>>;
  }[];
  readonly notices: RecordingNotice[];
  readonly output: string[];
}

async function record(startUrl: string): Promise<Recorded> {
  nextFile += 1;
  const file = `tests/recorded/r${String(nextFile)}.test.yaml`;
  const events: Recorded['events'] = [];
  const notices: RecordingNotice[] = [];
  const output: string[] = [];
  const session = await RecordingSession.start(
    { project, file, startUrl, headless: true },
    {
      stepRecorded: (index, step, targets) => {
        events.push({ kind: 'recorded', index, step, targets });
        output.push(...formatStep(index, step, false, targets));
      },
      stepChanged: (index, step) => {
        events.push({ kind: 'changed', index, step });
        output.push(...formatStep(index, step, true));
      },
      notice: (notice) => {
        notices.push(notice);
        output.push(formatNotice(notice));
      },
    },
  );
  totals.push(session.stats);
  return { session, file, events, notices, output };
}

function fileText(recorded: Recorded): string {
  return readFileSync(join(demo.root, recorded.file), 'utf8');
}

/** Stops recording and verifies the file; the verify's printed lines join the output. */
async function stopAndVerify(recorded: Recorded): Promise<VerifyResult> {
  await recorded.session.stop();
  const result = await verifyRecording(project, recorded.file);
  recorded.output.push(...formatVerify(result));
  return result;
}

/** Moves the mouse to the middle of an element and clicks there, as a person does. */
async function clickAt(page: Page, element: Locator): Promise<void> {
  await element.waitFor();
  const box = await element.boundingBox();
  if (box === null) throw new Error('The element has no box.');
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2, { steps: 3 });
  await page.mouse.down();
  await page.mouse.up();
}

function pageOf(recorded: Recorded, index: number): Page {
  const page = recorded.session.pages()[index];
  if (page === undefined) throw new Error(`The recording browser has no page ${String(index)}.`);
  return page;
}

function lines(...text: string[]): string {
  return [...text, ''].join('\n');
}

/** Every file under a folder, as text. */
function filesUnder(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? filesUnder(path) : [readFileSync(path, 'utf8')];
  });
}

describe('recording on the demo app', () => {
  it('a to-do added on /todos: a click into the field is no step; Enter and Add are', async () => {
    const recorded = await record('/todos');
    const page = pageOf(recorded, 0);
    await clickAt(page, page.getByLabel('New to-do'));
    await page.keyboard.type('Buy milk');
    await page.keyboard.press('Enter');
    await page.getByText('Buy milk').waitFor();
    await clickAt(page, page.getByLabel('New to-do'));
    await page.keyboard.type('Walk the dog');
    await clickAt(page, page.getByRole('button', { name: 'Add' }));
    await page.getByText('Walk the dog').waitFor();
    const result = await stopAndVerify(recorded);
    expect(fileText(recorded)).toBe(
      lines(
        'version: 1',
        'name: R1',
        'targets:',
        '  todos.newTodo:',
        '    - role: textbox',
        '      name: New to-do',
        '    - label: New to-do',
        "    - css: '#new-todo'",
        // todos.add is a shared target of the demo project, so the name is numbered.
        '  todos.add2:',
        '    - role: button',
        '      name: Add',
        '    - text: Add',
        "    - css: '#add-form > button'",
        'steps:',
        '  - goto: /todos',
        '  - fill: { target: todos.newTodo, value: Buy milk }',
        '  - press: { target: todos.newTodo, key: Enter }',
        '  - fill: { target: todos.newTodo, value: Walk the dog }',
        '  - click: todos.add2',
      ),
    );
    expect(recorded.notices).toEqual([]);
    expect(result.status).toBe('passed');
    expect(result.steps.map((step) => step.status)).toEqual(Array(5).fill('passed'));
  });

  it('the form page: a select, a checkbox, a radio and the submit button', async () => {
    const recorded = await record('/form');
    const page = pageOf(recorded, 0);
    // The label focuses the select without opening its popup; typing chooses
    // an option the same way on every system (arrow keys open the popup on macOS).
    await clickAt(page, page.locator('label[for="country"]'));
    await page.keyboard.type('Germany');
    await expect.poll(() => recorded.session.recorded.length).toBe(2);
    await clickAt(page, page.getByLabel('Subscribe to the newsletter'));
    await clickAt(page, page.getByLabel('Express'));
    await clickAt(page, page.getByRole('button', { name: 'Send order' }));
    await page.getByText('subscribed').waitFor();
    const result = await stopAndVerify(recorded);
    expect(fileText(recorded)).toBe(
      lines(
        'version: 1',
        'name: R2',
        'targets:',
        '  form.country2:',
        '    - role: combobox',
        '      name: Country',
        '    - label: Country',
        "    - css: '#country'",
        '  form.subscribeToTheNewsletter:',
        '    - role: checkbox',
        '      name: Subscribe to the newsletter',
        '    - label: Subscribe to the newsletter',
        "    - css: '#subscribe'",
        '  form.express:',
        '    - role: radio',
        '      name: Express',
        '    - label: Express',
        "    - css: '#order-form > fieldset > label:nth-of-type(2) > input'",
        '  form.sendOrder:',
        '    - role: button',
        '      name: Send order',
        '    - text: Send order',
        "    - css: '#order-form > button'",
        'steps:',
        '  - goto: /form',
        '  - select: { target: form.country2, option: Germany }',
        '  - check: form.subscribeToTheNewsletter',
        '  - check: form.express',
        '  - click: form.sendOrder',
      ),
    );
    expect(recorded.notices).toEqual([]);
    expect(result.status).toBe('passed');
  });

  it('a login: the password becomes the declared secret and is written out nowhere', async () => {
    const recorded = await record('/login');
    const page = pageOf(recorded, 0);
    await clickAt(page, page.getByLabel('Username'));
    await page.keyboard.type('alice');
    await clickAt(page, page.getByLabel('Password'));
    await page.keyboard.type(DEMO_PASSWORD);
    await clickAt(page, page.getByRole('button', { name: 'Sign in' }));
    await page.waitForURL('**/account');
    const result = await stopAndVerify(recorded);
    const text = fileText(recorded);
    expect(text).toContain(
      "  - fill: { target: login.password2, value: '${secrets.DEMO_PASSWORD}' }",
    );
    expect(text).not.toContain('review');
    expect(result.status).toBe('passed');
    const written = [
      text,
      JSON.stringify(recorded.events),
      JSON.stringify(recorded.notices),
      recorded.output.join('\n'),
      JSON.stringify(result),
      ...filesUnder(result.resultsDir),
    ].join('\n');
    expect(written).not.toContain(DEMO_PASSWORD);
  });

  it('a password no declared secret has: a placeholder variable, marked for review', async () => {
    const typed = 'not-the-demo-password-42';
    const recorded = await record('/login');
    const page = pageOf(recorded, 0);
    await clickAt(page, page.getByLabel('Username'));
    await page.keyboard.type('ada');
    await clickAt(page, page.getByLabel('Password'));
    await page.keyboard.type(typed);
    await page.keyboard.press('Tab');
    await expect.poll(() => recorded.session.recorded.length).toBe(4);
    const result = await stopAndVerify(recorded);
    const text = fileText(recorded);
    expect(text).toContain(lines('vars:', "  loginPassword2: ''"));
    expect(text).toContain(
      lines(
        '  # review: typed into a password field, and no declared secret has this value; declare a secret and use ${secrets.NAME}',
        "  - fill: { target: login.password2, value: '${vars.loginPassword2}' }",
        '  - press: { target: login.password2, key: Tab }',
      ),
    );
    const written = [
      text,
      JSON.stringify(recorded.events),
      recorded.output.join('\n'),
      JSON.stringify(result),
      ...filesUnder(result.resultsDir),
    ].join('\n');
    expect(written).not.toContain(typed);
  });

  it('a field inside the nested frames of /frames', async () => {
    const recorded = await record('/frames');
    const page = pageOf(recorded, 0);
    const payment = page
      .frameLocator('iframe#checkout')
      .frameLocator('iframe[title="Secure payment"]');
    await clickAt(page, payment.getByLabel('Card number'));
    await page.keyboard.type('4242 4242');
    await clickAt(page, payment.getByRole('button', { name: 'Pay' }));
    await payment.getByText(/Paid/).waitFor();
    const result = await stopAndVerify(recorded);
    expect(fileText(recorded)).toBe(
      lines(
        'version: 1',
        'name: R5',
        'targets:',
        '  frames.checkoutFrame:',
        '    - css: iframe#checkout',
        '    - css: iframe[title="Checkout"]',
        '  frames.securePaymentFrame:',
        '    frame: frames.checkoutFrame',
        '    candidates:',
        '      - css: iframe[title="Secure payment"]',
        '  frames.cardNumber:',
        '    frame: frames.securePaymentFrame',
        '    candidates:',
        '      - role: textbox',
        '        name: Card number',
        '      - label: Card number',
        "      - css: '#card-number'",
        '  frames.pay:',
        '    frame: frames.securePaymentFrame',
        '    candidates:',
        '      - role: button',
        '        name: Pay',
        '      - text: Pay',
        '      - css: body > button',
        'steps:',
        '  - goto: /frames',
        '  - fill: { target: frames.cardNumber, value: 4242 4242 }',
        '  - click: frames.pay',
      ),
    );
    expect(result.status).toBe('passed');
  });

  it('a link that opens a tab: the step gets opens, the next step page', async () => {
    const recorded = await record('/tabs');
    const page = pageOf(recorded, 0);
    await clickAt(page, page.getByRole('link', { name: 'Open receipt' }));
    await expect.poll(() => recorded.events.some((event) => event.kind === 'changed')).toBe(true);
    const receipt = pageOf(recorded, 1);
    await clickAt(receipt, receipt.getByRole('heading', { name: 'Receipt' }));
    const result = await stopAndVerify(recorded);
    expect(fileText(recorded)).toContain(
      lines(
        'steps:',
        '  - goto: /tabs',
        '  - click: tabs.openReceipt2',
        '    opens: receipt',
        '  - click: receipt.receipt',
        '    page: receipt',
      ),
    );
    expect(recorded.events.find((event) => event.kind === 'changed')).toMatchObject({
      index: 1,
      step: { opens: 'receipt' },
    });
    expect(result.status).toBe('passed');
  });

  it('two elements with the same text: role and text are rejected, the test ID is used', async () => {
    const recorded = await record('/details');
    const page = pageOf(recorded, 0);
    await clickAt(page, page.getByTestId('returns-details'));
    await page.getByText('Returns details').waitFor();
    const result = await stopAndVerify(recorded);
    expect(fileText(recorded)).toBe(
      lines(
        'version: 1',
        'name: R7',
        'targets:',
        '  details.details:',
        '    - testId: returns-details',
        '    - css: body > section:nth-of-type(2) > button',
        'steps:',
        '  - goto: /details',
        '  - click: details.details',
      ),
    );
    expect(recorded.session.stats.rejected).toMatchObject({ role: 1, text: 1 });
    expect(result.status).toBe('passed');
  });

  it('a repeated Delete button in a table row: within the row, not by position', async () => {
    const recorded = await record('/products');
    const page = pageOf(recorded, 0);
    await clickAt(page, page.getByRole('row', { name: 'Office chair' }).getByRole('button'));
    await page.getByText('Products left: 2').waitFor();
    const result = await stopAndVerify(recorded);
    expect(fileText(recorded)).toBe(
      lines(
        'version: 1',
        'name: R8',
        'targets:',
        '  products.officeChairRow:',
        '    - role: row',
        '      name: Office chair',
        '      exact: false',
        '    - role: row',
        '      name: Office chair €120 Delete',
        '  products.officeChairDelete:',
        '    within: products.officeChairRow',
        '    candidates:',
        '      - role: button',
        '        name: Delete',
        '      - text: Delete',
        'steps:',
        '  - goto: /products',
        '  - click: products.officeChairDelete',
      ),
    );
    expect(result.status).toBe('passed');
  });

  it('the second Delete button of a list: within its item; nothing but CSS is marked; a background click is a notice', async () => {
    const recorded = await record('/details');
    const page = pageOf(recorded, 0);
    await clickAt(
      page,
      page.getByRole('listitem').filter({ hasText: 'Office' }).getByRole('button'),
    );
    await expect.poll(() => recorded.session.recorded.length).toBe(2);
    await clickAt(page, page.locator('.swatch').nth(1));
    await page.getByText('Blue chosen').waitFor();
    await page.mouse.click(5, 700);
    await expect.poll(() => recorded.notices.length).toBe(1);
    const result = await stopAndVerify(recorded);
    expect(fileText(recorded)).toBe(
      lines(
        'version: 1',
        'name: R9',
        'targets:',
        '  details.officeItem:',
        '    - text: Office',
        '      exact: false',
        '    - text: Office Delete',
        '  details.officeDelete:',
        '    within: details.officeItem',
        '    candidates:',
        '      - role: button',
        '        name: Delete',
        '      - text: Delete',
        '  details.div:',
        '    - css: body > div > div:nth-of-type(2)',
        'steps:',
        '  - goto: /details',
        '  - click: details.officeDelete',
        '  # review: only CSS identifies this element',
        '  - click: details.div',
      ),
    );
    expect(recorded.notices).toEqual([
      expect.objectContaining({
        kind: 'background',
        message: 'A click on the page background (on no element) is not recorded.',
      }),
    ]);
    expect(result.status).toBe('passed');
  });

  it('an interaction that cannot be mapped: a notice, and no step', async () => {
    const recorded = await record('/interactions');
    const page = pageOf(recorded, 0);
    const from = await page.getByTestId('fruit-apple').boundingBox();
    const to = await page.getByTestId('fruit-cherry').boundingBox();
    if (from === null || to === null) throw new Error('The fruit have no boxes.');
    await page.mouse.move(from.x + 5, from.y + 5);
    await page.mouse.down();
    await page.mouse.move(to.x + 5, to.y + 5, { steps: 10 });
    await page.mouse.up();
    await expect.poll(() => recorded.notices.length).toBe(1);
    await recorded.session.stop();
    expect(recorded.notices).toEqual([
      expect.objectContaining({
        kind: 'drag',
        page: 'main',
        message: 'A drag of "Apple" is not recorded yet.',
      }),
    ]);
    expect(recorded.session.recorded.map((step) => step.action)).toEqual(['goto']);
  });
});

describe('verify', () => {
  it('names the failed step, why it failed and every candidate that did not match', async () => {
    const file = 'tests/recorded/broken.test.yaml';
    writeFileSync(
      join(demo.root, file),
      lines(
        'version: 1',
        'name: A recording whose page changed',
        'targets:',
        '  details.gone:',
        '    - testId: no-such-details',
        '    - role: button',
        '      name: Details',
        'steps:',
        '  - goto: /details',
        '  - click: details.gone',
        '    timeout: 1s',
      ),
    );
    const result = await verifyRecording(project, file);
    expect(result.status).toBe('failed');
    expect(result.failed).toMatchObject({
      stepId: 'steps.1',
      action: 'click',
      error: { code: 'TargetNotFound' },
    });
    expect(result.failed?.error?.candidates).toEqual([
      { candidate: { testId: 'no-such-details' }, matches: 0 },
      { candidate: { role: 'button', name: 'Details' }, matches: 2 },
    ]);
    expect(formatVerify(result)).toContain(
      '       candidate {"role":"button","name":"Details"} matched 2',
    );
  });

  it('refuses a recording that does not exist', async () => {
    await expect(verifyRecording(project, 'tests/recorded/missing.test.yaml')).rejects.toThrow();
  });
});
