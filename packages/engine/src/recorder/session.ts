// A recording session (docs/recording.md; ADR 0022): a visible Chromium with
// the context settings of a run, the page script in every frame, and the
// mapping of each reported interaction to a step. Interactions are handled
// one at a time, in the order they arrive. After every change the test file is
// rendered, validated with the engine's own validator and written, so the
// file on disk is a valid test file at every moment. The session knows nothing
// of any user interface: it tells a listener about recorded steps, changed
// steps and notices.

import { existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { PRODUCT } from '@cfe/protocol';
import type { Browser, BrowserContext, Frame, Locator, Page } from 'playwright';
import { z } from 'zod';
import {
  selectEnvironment,
  UnknownEnvironmentError,
  type EnvironmentProfile,
} from '../context/environment.js';
import { getEngineInfo } from '../engine-info.js';
import type { Project } from '../project/project.js';
import { launchBrowser } from '../runner/browser.js';
import { LoginStore } from '../runner/logins.js';
import { contextOptions } from '../runner/pages.js';
import { candidateLocator } from '../locate/candidates.js';
import type { Candidate } from '../schema/targets.js';
import type { Secrets } from '../sdk/context.js';
import {
  checkCandidates,
  chooseCandidates,
  emptyStats,
  parseAriaLine,
  proposeCandidates,
  proposeContainerCandidates,
  type AriaIdentity,
  type CheckStats,
  type ElementFacts,
} from './candidates.js';
import { renderRecording, type RecordedStep, type RecordedTarget } from './document.js';
import { elementSlug, literal, pageSlug, placeholderVariable, uniqueName } from './names.js';
import { pageScript } from './page-script.js';

/** The kind of a notice (docs/recording.md, "Protocol proposal"). */
export type NoticeKind =
  | 'drag'
  | 'fileChooser'
  | 'contextMenu'
  | 'doubleClick'
  | 'shortcut'
  | 'key'
  | 'history'
  | 'contentEditable'
  | 'background'
  | 'unmapped'
  | 'writeFailed';

/** Something the recorder saw but did not record. */
export interface RecordingNotice {
  /** What kind of thing it was. */
  readonly kind: NoticeKind;
  /** What it saw, for the tester. */
  readonly message: string;
  /** The page it happened on (`main`, or a tab's name). */
  readonly page: string;
  /** The page's URL. */
  readonly url: string;
}

/** Who hears about a recording. Every method is optional. */
export interface RecordingListener {
  /** A step was added at `index`, with the targets it made. */
  stepRecorded?(
    index: number,
    step: RecordedStep,
    targets: Readonly<Record<string, RecordedTarget>>,
  ): void;
  /** The step at `index` changed: a fill's text, or `opens` added. */
  stepChanged?(index: number, step: RecordedStep): void;
  /** Something was seen and not recorded. */
  notice?(notice: RecordingNotice): void;
}

/** What a session starts with (docs/recording.md, "A recording session"). */
export interface RecordingOptions {
  /** The open project. */
  readonly project: Project;
  /** The test file to write, relative to the project root; it must not exist. */
  readonly file: string;
  /** A full URL, or a path relative to the environment's base URL; default `/`. */
  readonly startUrl?: string | undefined;
  /** The environment; default: the config's default. */
  readonly environment?: string | undefined;
  /** A saved login for the main page. */
  readonly login?: string | undefined;
  /** The test's name; default: made from the file name. */
  readonly name?: string | undefined;
  /** Run the browser without a window (for tests); default false. */
  readonly headless?: boolean | undefined;
}

/** How a session ended. */
export type RecordingEnd = 'stopped' | 'browserClosed';

/** A recording that cannot start; `code` is stable. */
export class RecordingError extends Error {
  /** Such as `FileExists` or `UnknownLogin`. */
  readonly code: string;

  /**
   * @param code - Stable code.
   * @param message - What is wrong, for the tester.
   */
  constructor(code: string, message: string) {
    super(message);
    this.name = 'RecordingError';
    this.code = code;
  }
}

/** The binding the page script reports to, and the marker attribute it sets. */
const BINDING = `__${PRODUCT.command}Record`;
const MARKER = `data-${PRODUCT.command}-recorded`;

const REVIEW_CSS = 'only CSS identifies this element';
const REVIEW_PASSWORD =
  'typed into a password field, and no declared secret has this value; declare a secret and use ${secrets.NAME}';

/** How long `stop` waits for interactions the page reported just before. */
const SETTLE_MS = 200;

/** How long after a step a new tab still counts as opened by it. */
const OPENS_WINDOW_MS = 5_000;

const FactsSchema = z.object({
  container: z
    .object({
      mark: z.string().regex(/^[a-z0-9]+$/),
      tag: z.string(),
      label: z.string().optional(),
      text: z.string().optional(),
      testId: z.string().optional(),
    })
    .optional(),
  tag: z.string(),
  type: z.string().optional(),
  id: z.string().optional(),
  placeholder: z.string().optional(),
  testId: z.string().optional(),
  text: z.string().optional(),
  field: z.boolean(),
  css: z.string(),
});
const Mark = z.string().regex(/^[a-z0-9]+$/);
const PageEventSchema = z.discriminatedUnion('kind', [
  z.object({ kind: z.literal('focus'), mark: Mark, facts: FactsSchema }),
  z.object({ kind: z.literal('fieldClick'), mark: Mark, facts: FactsSchema }),
  z.object({ kind: z.literal('click'), mark: Mark, facts: FactsSchema }),
  z.object({
    kind: z.literal('fill'),
    mark: Mark,
    facts: FactsSchema,
    value: z.string(),
    password: z.boolean(),
  }),
  z.object({
    kind: z.literal('press'),
    mark: Mark,
    facts: FactsSchema,
    key: z.enum(['Enter', 'Tab', 'Escape']),
  }),
  z.object({
    kind: z.literal('select'),
    mark: Mark,
    facts: FactsSchema,
    selected: z.array(z.object({ value: z.string(), label: z.string() })),
    labels: z.array(z.string()),
  }),
  z.object({ kind: z.literal('check'), mark: Mark, facts: FactsSchema, checked: z.boolean() }),
  z.object({
    kind: z.literal('notice'),
    notice: z.enum([
      'drag',
      'fileChooser',
      'contextMenu',
      'doubleClick',
      'shortcut',
      'key',
      'history',
      'contentEditable',
      'background',
    ]),
    message: z.string(),
    mark: Mark.optional(),
    facts: FactsSchema.optional(),
  }),
]);
type PageEvent = z.infer<typeof PageEventSchema>;

/** A target the recorder resolved for an interaction. */
interface Resolved {
  readonly name: string;
  readonly review?: string | undefined;
}

/** The test's name made from its file name: `add-todo.test.yaml` → "Add todo". */
function nameFromFile(file: string): string {
  const words = basename(file)
    .replace(/\.test\.yaml$/, '')
    .split(/[-_\s.]+/)
    .filter((word) => word !== '');
  const text = words.join(' ');
  return text === '' ? 'Recorded test' : text.charAt(0).toUpperCase() + text.slice(1);
}

/** A start URL as the file holds it: a path when it is under the base URL. */
function startUrlForFile(startUrl: string, baseUrl: string): string {
  const base = baseUrl.replace(/\/$/, '');
  if (startUrl.startsWith(`${base}/`)) return startUrl.slice(base.length);
  return startUrl === base ? '/' : startUrl;
}

/** A CSS attribute value in double quotes. */
function quoted(value: string): string {
  return `"${value.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`;
}

/** A candidate with its text escaped for a step file. */
function escaped(candidate: Candidate): Candidate {
  return Object.fromEntries(
    Object.entries(candidate).map(([key, value]) => [
      key,
      typeof value === 'string' && key !== 'role' ? literal(value) : value,
    ]),
  );
}

/**
 * One recording: start it, listen to it, stop it.
 *
 * @example
 * ```ts
 * const session = await RecordingSession.start(
 *   { project, file: 'tests/add-todo.test.yaml', startUrl: '/todos' },
 *   { stepRecorded: (index, step) => console.log(index, step.action) },
 * );
 * const end = await session.ended; // 'browserClosed' when the person closes it
 * ```
 */
export class RecordingSession {
  /** The test file, relative to the project root. */
  readonly file: string;
  /** Resolves when the session ends, with how it ended. */
  readonly ended: Promise<RecordingEnd>;
  /** How many candidates of each kind were proposed and rejected. */
  readonly stats: CheckStats = emptyStats();

  private readonly project: Project;
  private readonly listener: RecordingListener;
  private readonly secrets: Secrets;
  private readonly declaredSecrets: readonly string[];
  private readonly testIdAttribute: string;
  private readonly name: string;
  private readonly login: string | undefined;
  private browser: Browser | undefined;
  private context: BrowserContext | undefined;
  private readonly steps: RecordedStep[] = [];
  private readonly stepTimes: number[] = [];
  private readonly targets = new Map<string, RecordedTarget>();
  private readonly targetByDefinition = new Map<string, string>();
  private readonly vars = new Map<string, string>();
  private readonly pageNames = new Map<Page, string>();
  private readonly fieldTargets = new Map<string, Promise<Resolved | undefined>>();
  private newTargets: Record<string, RecordedTarget> = {};
  private pendingFieldClick: { step: RecordedStep } | undefined;
  private queue: Promise<unknown> = Promise.resolve();
  private finished = false;
  private resolveEnded: (end: RecordingEnd) => void = () => undefined;

  private constructor(
    options: RecordingOptions,
    listener: RecordingListener,
    secrets: Secrets,
    declaredSecrets: readonly string[],
    testIdAttribute: string,
  ) {
    this.project = options.project;
    this.file = options.file;
    this.listener = listener;
    this.secrets = secrets;
    this.declaredSecrets = declaredSecrets;
    this.testIdAttribute = testIdAttribute;
    this.name = options.name ?? nameFromFile(options.file);
    this.login = options.login;
    this.ended = new Promise((resolve) => {
      this.resolveEnded = resolve;
    });
  }

  /**
   * Opens the recording browser, goes to the start URL and starts recording.
   * The first step, the `goto`, is written at once.
   *
   * @param options - The project, the file to write and where to start.
   * @param listener - Hears about recorded steps, changes and notices.
   * @returns The running session.
   * @throws RecordingError `ConfigInvalid`, `NotATestFile`, `FileExists`,
   *   `UnknownEnvironment` or `UnknownLogin`; a saved login whose flow fails
   *   rejects with that error.
   */
  static async start(
    options: RecordingOptions,
    listener: RecordingListener = {},
  ): Promise<RecordingSession> {
    const { project } = options;
    const config = project.config;
    const secrets = project.secrets;
    if (config === undefined || secrets === undefined) {
      throw new RecordingError(
        'ConfigInvalid',
        `${PRODUCT.configFile} has errors, so nothing can be recorded. Fix them first.`,
      );
    }
    if (!options.file.endsWith('.test.yaml')) {
      throw new RecordingError(
        'NotATestFile',
        `"${options.file}" is not a test file name; it must end in .test.yaml.`,
      );
    }
    if (existsSync(join(project.root, options.file))) {
      throw new RecordingError(
        'FileExists',
        `"${options.file}" exists already; recording into an existing test is not available yet.`,
      );
    }
    let profile: EnvironmentProfile;
    try {
      profile = selectEnvironment(config, options.environment);
    } catch (error) {
      if (error instanceof UnknownEnvironmentError) {
        throw new RecordingError('UnknownEnvironment', error.message);
      }
      throw error;
    }
    if (options.login !== undefined && config.logins?.[options.login] === undefined) {
      throw new RecordingError(
        'UnknownLogin',
        `There is no saved login "${options.login}". The config has: ${Object.keys(config.logins ?? {}).join(', ') || 'none'}.`,
      );
    }
    const testIdAttribute = config.defaults?.testIdAttribute ?? 'data-testid';
    const session = new RecordingSession(
      options,
      listener,
      secrets,
      config.secrets ?? [],
      testIdAttribute,
    );
    const browser = await launchBrowser(options.headless !== true);
    session.browser = browser;
    try {
      const storageState =
        options.login === undefined
          ? undefined
          : await new LoginStore({
              project,
              config,
              profile,
              secrets,
              registry: project.registry,
              sharedTargets: project.sharedTargetValues(),
              testIdAttribute,
              browser,
              refresh: false,
              engineVersion: getEngineInfo().version,
            }).forTest(
              'recording',
              false,
              () => undefined,
            )(options.login, 'steps.0');
      const context = await browser.newContext(contextOptions(profile, storageState));
      session.context = context;
      await context.exposeBinding(BINDING, (source: { page: Page; frame: Frame }, payload) =>
        session.receive(source.page, source.frame, payload),
      );
      await context.addInitScript({
        content: pageScript({ binding: BINDING, attribute: MARKER, testIdAttribute }),
      });
      context.on('page', (page) => {
        void session.enqueue(() => session.onNewPage(page));
      });
      browser.on('disconnected', () => {
        session.finish('browserClosed');
      });
      const page = await context.newPage();
      session.watchClose(page);
      session.pageNames.set(page, 'main');
      const startUrl = options.startUrl ?? '/';
      session.add({
        action: 'goto',
        params: { url: literal(startUrlForFile(startUrl, profile.baseUrl)) },
      });
      await page.goto(startUrl);
      return session;
    } catch (error) {
      await browser.close().catch(() => undefined);
      // A session that did not start leaves no file behind.
      rmSync(join(project.root, options.file), { force: true });
      throw error;
    }
  }

  /**
   * The open pages of the recording browser, in the order they opened. For
   * tests that drive the browser as a person would; clients never need it.
   *
   * @returns The pages.
   */
  pages(): Page[] {
    return this.context?.pages() ?? [];
  }

  /** The steps recorded so far. */
  get recorded(): readonly RecordedStep[] {
    return this.steps;
  }

  /**
   * Stops recording: the last pending step is written and the browser closes.
   *
   * @returns When the browser is closed.
   */
  async stop(): Promise<void> {
    // An interaction the page reported just before may still be on its way.
    await new Promise((resolve) => setTimeout(resolve, SETTLE_MS));
    await this.queue.catch(() => undefined);
    this.flushFieldClick();
    this.finish('stopped');
    await this.browser?.close().catch(() => undefined);
  }

  private finish(end: RecordingEnd): void {
    if (this.finished) return;
    this.finished = true;
    this.flushFieldClick();
    this.resolveEnded(end);
  }

  private watchClose(page: Page): void {
    page.on('close', () => {
      if (this.context?.pages().length === 0) {
        this.finish('browserClosed');
        void this.browser?.close().catch(() => undefined);
      }
    });
  }

  /** Runs tasks one at a time, in the order they arrive. */
  private enqueue<T>(task: () => Promise<T>): Promise<T> {
    const run = this.queue.then(task);
    this.queue = run.catch(() => undefined);
    return run;
  }

  /** Called by the binding; answers once the event is handled, which releases a held click. */
  private receive(page: Page, frame: Frame, payload: unknown): Promise<void> {
    const parsed = PageEventSchema.safeParse(payload);
    if (!parsed.success || this.finished) return Promise.resolve();
    return this.enqueue(async () => {
      try {
        await this.handle(page, frame, parsed.data);
      } catch (error) {
        // Never the typed text: Playwright's messages hold selectors, not values.
        this.notice(
          'unmapped',
          `An interaction could not be recorded: ${error instanceof Error ? (error.message.split('\n')[0] ?? '') : String(error)}`,
          page,
        );
      }
    });
  }

  private async handle(page: Page, frame: Frame, event: PageEvent): Promise<void> {
    const pageName = this.pageNames.get(page);
    if (pageName === undefined) {
      if (event.kind !== 'focus') {
        this.notice(
          'unmapped',
          'An interaction in a tab that no recorded step opened is not recorded.',
          page,
        );
      }
      return;
    }
    const onPage = pageName === 'main' ? {} : { page: pageName };
    switch (event.kind) {
      case 'focus': {
        const resolving = this.resolve(page, frame, event.mark, event.facts);
        this.fieldTargets.set(event.mark, resolving);
        return;
      }
      case 'fieldClick': {
        const target = await this.fieldTarget(page, frame, event.mark, event.facts);
        this.flushFieldClick();
        if (target === undefined) return;
        this.pendingFieldClick = {
          step: {
            action: 'click',
            params: { target: target.name },
            ...onPage,
            review: target.review,
          },
        };
        return;
      }
      case 'fill': {
        const target = await this.fieldTarget(page, frame, event.mark, event.facts);
        if (target === undefined) return;
        const pending = this.pendingFieldClick?.step;
        if (pending?.params.target === target.name && pending.page === onPage.page) {
          this.pendingFieldClick = undefined;
        } else {
          this.flushFieldClick();
        }
        const { value, review } = this.valueFor(target.name, event.value, event.password);
        const step: RecordedStep = {
          action: 'fill',
          params: { target: target.name, value },
          ...onPage,
          review: joinReviews(target.review, review),
        };
        const index = this.steps.length - 1;
        const last = this.steps[index];
        if (
          last?.action === 'fill' &&
          last.params.target === target.name &&
          last.page === onPage.page
        ) {
          this.change(index, { ...step, opens: last.opens });
        } else {
          this.add(step);
        }
        return;
      }
      case 'press': {
        const target = await this.fieldTarget(page, frame, event.mark, event.facts);
        this.flushFieldClick();
        if (target === undefined) return;
        this.add({
          action: 'press',
          params: { target: target.name, key: event.key },
          ...onPage,
          review: target.review,
        });
        return;
      }
      case 'click': {
        this.flushFieldClick();
        const target = await this.resolve(page, frame, event.mark, event.facts);
        if (target === undefined) return;
        this.add({
          action: 'click',
          params: { target: target.name },
          ...onPage,
          review: target.review,
        });
        return;
      }
      case 'select': {
        this.flushFieldClick();
        const target = await this.resolve(page, frame, event.mark, event.facts);
        if (target === undefined) return;
        const options = event.selected.map((option) =>
          literal(
            event.labels.filter((label) => label === option.label).length === 1
              ? option.label
              : option.value,
          ),
        );
        this.add({
          action: 'select',
          params: { target: target.name, option: options.length === 1 ? options[0] : options },
          ...onPage,
          review: target.review,
        });
        return;
      }
      case 'check': {
        this.flushFieldClick();
        const target = await this.resolve(page, frame, event.mark, event.facts);
        if (target === undefined) return;
        this.add({
          action: 'check',
          params: event.checked ? { target: target.name } : { target: target.name, checked: false },
          ...onPage,
          review: target.review,
        });
        return;
      }
      case 'notice':
        this.notice(event.notice, event.message, page);
        return;
    }
  }

  /** The target of a text field: the one found when it got the focus, or found now. */
  private fieldTarget(
    page: Page,
    frame: Frame,
    mark: string,
    facts: ElementFacts,
  ): Promise<Resolved | undefined> {
    let target = this.fieldTargets.get(mark);
    if (target === undefined) {
      target = this.resolve(page, frame, mark, facts);
      this.fieldTargets.set(mark, target);
    }
    return target;
  }

  /** Finds and checks the candidates of a touched element, and names its target. */
  private async resolve(
    page: Page,
    frame: Frame,
    mark: string,
    facts: ElementFacts,
  ): Promise<Resolved | undefined> {
    const marked = frame.locator(`[${MARKER}="${mark}"]`);
    let snapshot: string;
    try {
      snapshot = await marked.ariaSnapshot({ timeout: 2_000 });
    } catch {
      snapshot = '';
    }
    const aria = parseAriaLine(snapshot);
    const proposals = proposeCandidates(facts, aria);
    const passed = await checkCandidates(
      frame,
      marked,
      proposals,
      this.testIdAttribute,
      this.stats,
    );
    const { candidates, cssOnly } = chooseCandidates(passed);
    const what = `${facts.tag}${aria?.name ? ` "${aria.name}"` : ''}`;
    let frameTarget: string | undefined;
    if (frame.parentFrame() !== null) {
      frameTarget = await this.frameTarget(page, frame);
      if (frameTarget === undefined) {
        this.notice(
          'unmapped',
          `The frame of the ${what} cannot be identified, so the interaction with it is not recorded.`,
          page,
        );
        return undefined;
      }
    }
    // Several elements fit the element's own candidates: scope them within its container.
    if ((candidates.length === 0 || cssOnly) && facts.container !== undefined) {
      const scoped = await this.scoped(page, frame, marked, facts, aria, proposals, frameTarget);
      if (scoped !== undefined) return { name: scoped };
    }
    if (candidates.length === 0) {
      this.notice(
        'unmapped',
        `No candidate identifies the ${what} exactly, so the interaction with it is not recorded.`,
        page,
      );
      return undefined;
    }
    const name = this.nameFor(
      {
        candidates: candidates.map(escaped),
        ...(frameTarget === undefined ? {} : { frame: frameTarget }),
      },
      pageSlug(page.url()),
      elementSlug([aria?.name, facts.placeholder, facts.testId, facts.text, facts.tag]),
    );
    return { name, review: cssOnly ? REVIEW_CSS : undefined };
  }

  /**
   * A target `within` the element's nearest container (review 0009, finding 1):
   * the container's own candidates are checked in the frame, then the
   * element's candidates other than CSS are checked inside the container.
   * Undefined when either finds nothing.
   */
  private async scoped(
    page: Page,
    frame: Frame,
    marked: Locator,
    facts: ElementFacts,
    aria: AriaIdentity | undefined,
    proposals: readonly Candidate[],
    frameTarget: string | undefined,
  ): Promise<string | undefined> {
    const container = facts.container;
    if (container === undefined) return undefined;
    const containerMarked = frame.locator(`[${MARKER}="${container.mark}"]`);
    let snapshot: string;
    try {
      snapshot = await containerMarked.ariaSnapshot({ timeout: 2_000 });
    } catch {
      snapshot = '';
    }
    const containerAria = parseAriaLine(snapshot);
    const outer = (
      await checkCandidates(
        frame,
        containerMarked,
        proposeContainerCandidates(container, containerAria),
        this.testIdAttribute,
        this.stats,
      )
    ).slice(0, 2);
    const first = outer[0];
    if (first === undefined) return undefined;
    const scope = candidateLocator(frame, first, this.testIdAttribute);
    const inner = (
      await checkCandidates(
        scope,
        marked,
        proposals.filter((candidate) => candidate.css === undefined),
        this.testIdAttribute,
        this.stats,
      )
    ).slice(0, 2);
    if (inner.length === 0) return undefined;
    const slug = pageSlug(page.url());
    const containerName = this.nameFor(
      {
        candidates: outer.map(escaped),
        ...(frameTarget === undefined ? {} : { frame: frameTarget }),
      },
      slug,
      `${elementSlug([container.label, containerAria?.name, container.testId, container.tag])}${containerSuffix(container.tag, containerAria?.role)}`,
    );
    const own =
      [aria?.name, facts.placeholder, facts.testId, facts.text].find(
        (text) => text !== undefined && text !== '',
      ) ?? facts.tag;
    return this.nameFor(
      { within: containerName, candidates: inner.map(escaped) },
      slug,
      elementSlug([`${container.label ?? ''} ${own}`, own]),
    );
  }

  /** The target of a frame's `<iframe>` element, from its id, title or name, checked like any candidate. */
  private async frameTarget(page: Page, frame: Frame): Promise<string | undefined> {
    const parent = frame.parentFrame();
    if (parent === null) return undefined;
    const element = await frame.frameElement();
    const [id, title, frameName] = await Promise.all([
      element.getAttribute('id'),
      element.getAttribute('title'),
      element.getAttribute('name'),
    ]);
    await element.dispose();
    const proposals: Candidate[] = [];
    if (id !== null && id !== '') {
      proposals.push({
        css: /^[A-Za-z][\w-]*$/.test(id) ? `iframe#${id}` : `iframe[id=${quoted(id)}]`,
      });
    }
    if (title !== null && title !== '') proposals.push({ css: `iframe[title=${quoted(title)}]` });
    if (frameName !== null && frameName !== '') {
      proposals.push({ css: `iframe[name=${quoted(frameName)}]` });
    }
    const passed: Candidate[] = [];
    for (const candidate of proposals) {
      if (await this.isFrame(parent, parent.locator(candidate.css ?? ''), frame))
        passed.push(candidate);
    }
    if (passed.length === 0) {
      // Nothing names the frame: its position among the frames, as a last resort.
      const all = parent.locator('iframe, frame');
      const count = await all.count();
      for (let index = 0; index < count; index++) {
        if (await this.isFrame(parent, all.nth(index), frame)) {
          passed.push({ css: 'iframe, frame', nth: index });
          break;
        }
      }
    }
    if (passed.length === 0) return undefined;
    const parentTarget =
      parent.parentFrame() === null ? undefined : await this.frameTarget(page, parent);
    if (parent.parentFrame() !== null && parentTarget === undefined) return undefined;
    return this.nameFor(
      {
        candidates: passed.slice(0, 2).map(escaped),
        ...(parentTarget === undefined ? {} : { frame: parentTarget }),
      },
      pageSlug(page.url()),
      `${elementSlug([id ?? undefined, title ?? undefined, frameName ?? undefined, 'frame'])}Frame`.replace(
        /FrameFrame$/,
        'Frame',
      ),
    );
  }

  /** Whether a locator in a parent frame matches exactly the `<iframe>` of a frame. */
  private async isFrame(
    parent: Frame,
    locator: ReturnType<Frame['locator']>,
    frame: Frame,
  ): Promise<boolean> {
    try {
      const only = locator.and(parent.locator('iframe, frame'));
      if ((await only.count()) !== 1) return false;
      const handle = await only.elementHandle({ timeout: 1_000 });
      const content = await handle.contentFrame();
      await handle.dispose();
      return content === frame;
    } catch {
      return false;
    }
  }

  /** The name of a target: the one already made for the same definition, or a new unique one. */
  private nameFor(target: RecordedTarget, page: string, element: string): string {
    const key = JSON.stringify(target);
    const known = this.targetByDefinition.get(key);
    if (known !== undefined) return known;
    const shared = this.project.sharedTargetValues();
    const name = uniqueName(
      `${page}.${element}`,
      (taken) => this.targets.has(taken) || shared.has(taken),
    );
    this.targets.set(name, target);
    this.targetByDefinition.set(key, name);
    this.newTargets[name] = target;
    return name;
  }

  /** The value a fill writes: a declared secret, a placeholder for a password, or the text. */
  private valueFor(
    targetName: string,
    typed: string,
    password: boolean,
  ): { value: string; review?: string } {
    for (const name of this.declaredSecrets) {
      let secret: string | undefined;
      try {
        secret = this.secrets.get(name);
      } catch {
        secret = undefined;
      }
      if (secret !== undefined && secret !== '' && secret === typed) {
        return { value: `\${secrets.${name}}` };
      }
    }
    if (password) {
      const variable = placeholderVariable(targetName);
      this.vars.set(variable, '');
      return { value: `\${vars.${variable}}`, review: REVIEW_PASSWORD };
    }
    return { value: literal(typed) };
  }

  /** A click into a text field that was not typed into is a step after all. */
  private flushFieldClick(): void {
    const pending = this.pendingFieldClick;
    this.pendingFieldClick = undefined;
    if (pending !== undefined) this.add(pending.step);
  }

  private async onNewPage(page: Page): Promise<void> {
    this.watchClose(page);
    if (this.pageNames.size === 0) return;
    await page.waitForLoadState('domcontentloaded', { timeout: 5_000 }).catch(() => undefined);
    const opener = await page.opener().catch(() => null);
    const openerName = opener === null ? undefined : this.pageNames.get(opener);
    const index = this.steps.length - 1;
    const last = this.steps[index];
    const lastAt = this.stepTimes[index] ?? -Infinity;
    const fits =
      last !== undefined &&
      openerName !== undefined &&
      (last.page ?? 'main') === openerName &&
      last.opens === undefined &&
      performance.now() - lastAt < OPENS_WINDOW_MS;
    if (!fits) {
      this.notice(
        'unmapped',
        `A new tab opened (${page.url()}), but no recorded step opened it, so what happens in it is not recorded.`,
        page,
      );
      return;
    }
    const slug = pageSlug(page.url());
    const taken = new Set(this.pageNames.values());
    const name = uniqueName(slug === 'home' ? 'tab' : slug, (candidate) => taken.has(candidate));
    this.pageNames.set(page, name);
    this.change(index, { ...last, opens: name });
  }

  private add(step: RecordedStep): void {
    const clean = withoutUndefined(step);
    this.steps.push(clean);
    this.stepTimes.push(performance.now());
    const targets = this.newTargets;
    this.newTargets = {};
    this.write();
    this.listener.stepRecorded?.(this.steps.length - 1, clean, targets);
  }

  private change(index: number, step: RecordedStep): void {
    const clean = withoutUndefined(step);
    this.steps[index] = clean;
    this.stepTimes[index] = performance.now();
    this.write();
    this.listener.stepChanged?.(index, clean);
  }

  private notice(kind: NoticeKind, message: string, page: Page): void {
    this.listener.notice?.({
      kind,
      message,
      page: this.pageNames.get(page) ?? 'unknown',
      url: page.url(),
    });
  }

  /** Renders, validates and writes the file; a file with errors is not written. */
  private write(): void {
    const text = renderRecording({
      name: this.name,
      login: this.login,
      vars: this.vars,
      targets: this.targets,
      steps: this.steps,
    });
    const errors = this.project
      .validate({ content: { file: this.file, text } })
      .filter((diagnostic) => diagnostic.severity === 'error');
    const first = errors[0];
    if (first !== undefined) {
      this.listener.notice?.({
        kind: 'writeFailed',
        message: `The recording was not written, because it would not be valid: ${first.code}: ${first.message} (line ${String(first.line)}).`,
        page: 'main',
        url: '',
      });
      return;
    }
    const path = join(this.project.root, this.file);
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, text);
  }
}

/** The word a container's target name ends in: `Row`, `Item`, `Form`, … */
function containerSuffix(tag: string, role: string | undefined): string {
  if (tag === 'tr' || role === 'row') return 'Row';
  if (tag === 'li' || role === 'listitem') return 'Item';
  if (tag === 'form' || role === 'form') return 'Form';
  if (tag === 'dialog' || role === 'dialog' || role === 'alertdialog') return 'Dialog';
  if (tag === 'fieldset' || role === 'group') return 'Group';
  if (tag === 'article' || role === 'article') return 'Article';
  return 'Section';
}

/** Joins review reasons; undefined when there are none. */
function joinReviews(...reasons: (string | undefined)[]): string | undefined {
  const given = reasons.filter((reason): reason is string => reason !== undefined);
  return given.length === 0 ? undefined : given.join('; ');
}

/** A step without its undefined keys, so it renders and compares cleanly. */
function withoutUndefined(step: RecordedStep): RecordedStep {
  return Object.fromEntries(
    Object.entries(step).filter(([, value]) => value !== undefined),
  ) as unknown as RecordedStep;
}
