// The pages of one test instance (docs/step-format.md, "Pages" and "New tabs
// and pop-ups: opens"): `main` and the declared pages are opened on first use;
// pages with the same login share one browser context, pages with different
// logins get their own. A page the application opens during a step is
// registered under the step's `opens` name, or else under an automatic name
// (`tab-3`) with an `UnnamedPage` warning; both send `pageOpened`. A page that
// was closed is replaced by a blank one for `after` steps (`PageReplaced`).
//
// Playwright reports a new tab only once it has started loading, which can be
// after the step that opened it has ended. Chromium reports the tab's creation
// earlier, through the browser's DevTools protocol, so the step that was
// running then is the one the tab is attributed to.

import type { Location } from '@cfe/protocol';
import type { Browser, BrowserContext, CDPSession, Page } from 'playwright';
import type { EnvironmentProfile } from '../context/environment.js';
import { StepError } from './errors.js';
import type { ResponseLog } from './responses.js';
import type { EmitEvent } from './test-run.js';

/** Playwright's storage state: cookies and local storage of a signed-in session. */
export type StorageState = Awaited<ReturnType<BrowserContext['storageState']>>;

/**
 * Gives the storage state of a saved login, signing in when needed.
 *
 * @param login - The login's name.
 * @param stepId - The step that first needs it, for log events.
 */
export type LoginStates = (login: string, stepId: string) => Promise<StorageState>;

/** What a {@link PageSet} needs. */
export interface PageSetOptions {
  /** The run's browser. */
  readonly browser: Browser;
  /** The environment, for the context settings and the base URL. */
  readonly profile: EnvironmentProfile;
  /** The login of each declared page, `main` included; absent when it has none. */
  readonly pageLogins: ReadonlyMap<string, string | undefined>;
  /** Gives the storage state of a login. */
  readonly logins: LoginStates;
  /** Where every response of the test's contexts is recorded, if anywhere. */
  readonly responses?: ResponseLog | undefined;
  /** The test instance, for events. */
  readonly testId: string;
  /** Sends events. */
  readonly emit: EmitEvent;
}

/** The step being run, for pages that open during it. */
interface CurrentStep {
  readonly stepId: string;
  readonly location: Location;
  readonly opens: string | undefined;
}

/**
 * The pages and browser contexts of one test instance.
 *
 * @example
 * ```ts
 * const pages = new PageSet({ browser, profile, pageLogins, logins, testId, emit });
 * const page = await pages.page('main', 'steps');
 * await pages.close();
 * ```
 */
export class PageSet {
  private readonly options: PageSetOptions;
  private readonly contexts = new Map<string, Promise<BrowserContext>>();
  private readonly pages = new Map<string, Page>();
  private readonly contextOf = new Map<string, BrowserContext>();
  /** Pages the engine itself is creating, so the context's page event skips them. */
  private readonly ownPending = new Map<BrowserContext, number>();
  private opened = 0;
  private current: CurrentStep | undefined;
  private lastStepId = 'before.0';
  /** Watches the browser for new tabs, as early as Chromium reports them. */
  private discovery: Promise<CDPSession | undefined> | undefined;
  /** The step running when each tab the application opened was created, by target id. */
  private readonly createdDuring = new Map<string, CurrentStep | undefined>();

  /**
   * @param options - The browser, environment, logins and event sink.
   */
  constructor(options: PageSetOptions) {
    this.options = options;
  }

  /**
   * The page a step runs on, opened on first use.
   *
   * @param name - The step's page: `main`, a declared page or an `opens` name.
   * @param section - The step's section; only `after` steps get a replacement
   *   for a page that was closed.
   * @returns The page.
   * @throws StepError `PageClosed` when the page was closed and the step is
   *   not an `after` step; `UnknownPage` for a name no step registered.
   */
  async page(name: string, section: 'before' | 'steps' | 'after'): Promise<Page> {
    const existing = this.pages.get(name);
    if (existing !== undefined && !existing.isClosed()) return existing;
    if (existing !== undefined) {
      const context = this.contextOf.get(name);
      if (section !== 'after' || context === undefined) {
        throw new StepError('PageClosed', `The page "${name}" was closed.`);
      }
      const replacement = await this.newPage(context);
      this.register(name, replacement, context);
      this.options.emit('log', {
        level: 'warn',
        code: 'PageReplaced',
        message: `The page "${name}" was closed, so this after step runs on a new blank page in the same browser context.`,
        testId: this.options.testId,
        stepId: this.current?.stepId ?? this.lastStepId,
        ...(this.current === undefined ? {} : { location: this.current.location }),
      });
      return replacement;
    }
    if (name !== 'main' && !this.options.pageLogins.has(name)) {
      throw new StepError(
        'UnknownPage',
        `There is no page "${name}" yet: declare it under "pages", or open it with "opens" in an earlier step.`,
      );
    }
    const login = this.options.pageLogins.get(name);
    const context = await this.context(login);
    const page = await this.newPage(context);
    this.register(name, page, context);
    return page;
  }

  /**
   * Closes a page, for example the page of a user action that kept running
   * after its step ended. An `after` step that uses it gets a replacement.
   *
   * @param name - The page's name.
   */
  async closePage(name: string): Promise<void> {
    await this.pages
      .get(name)
      ?.close()
      .catch(() => undefined);
  }

  /**
   * Marks the start of a step, so pages that open during it are named after
   * its `opens`, or get an automatic name with a warning.
   *
   * @param stepId - The step's id.
   * @param location - The step's location, for warnings.
   * @param opens - The step's `opens` name, if any.
   */
  beginStep(stepId: string, location: Location, opens: string | undefined): void {
    this.current = { stepId, location, opens };
    this.lastStepId = stepId;
  }

  /**
   * Ends a step. For a step with `opens`, waits until the page it named has
   * opened.
   *
   * @param deadline - The step's deadline on the `performance.now()` clock.
   * @param signal - The step's signal; waiting stops when it is aborted.
   * @throws StepError `PageNotOpened` when no new page opened in time.
   */
  async endStep(deadline: number, signal: AbortSignal): Promise<void> {
    const opens = this.current?.opens;
    try {
      if (opens === undefined) return;
      while (!this.pages.has(opens)) {
        signal.throwIfAborted();
        if (performance.now() >= deadline) {
          throw new StepError(
            'PageNotOpened',
            `No new page opened during this step, so there is no page "${opens}".`,
            'Check that the step opens a tab or a pop-up, or remove "opens".',
          );
        }
        await new Promise((resolve) => setTimeout(resolve, 50));
      }
    } finally {
      this.current = undefined;
    }
  }

  /**
   * The storage state of the context of pages without a login, for a login
   * flow that has just signed in.
   *
   * @returns The cookies and storage, or undefined when no page was opened.
   */
  async storageState(): Promise<StorageState | undefined> {
    const context = this.contexts.get('');
    return context === undefined ? undefined : (await context).storageState();
  }

  /** Forgets the current step without waiting, for a step that failed. */
  clearStep(): void {
    this.current = undefined;
  }

  /** Closes every browser context of the test. */
  async close(): Promise<void> {
    await (await this.discovery)?.detach().catch(() => undefined);
    const contexts = await Promise.allSettled([...this.contexts.values()]);
    for (const result of contexts) {
      if (result.status === 'fulfilled') await result.value.close().catch(() => undefined);
    }
  }

  private context(login: string | undefined): Promise<BrowserContext> {
    const key = login ?? '';
    let context = this.contexts.get(key);
    if (context === undefined) {
      context = this.createContext(login);
      this.contexts.set(key, context);
      // A failed sign-in must not be cached: the next use tries again.
      context.catch(() => {
        this.contexts.delete(key);
      });
    }
    return context;
  }

  /** Starts watching for new tabs; without it, tabs are attributed when Playwright reports them. */
  private startDiscovery(): Promise<CDPSession | undefined> {
    this.discovery ??= (async () => {
      try {
        const session = await this.options.browser.newBrowserCDPSession();
        session.on('Target.targetCreated', ({ targetInfo }) => {
          if (targetInfo.type === 'page' && targetInfo.openerId !== undefined) {
            this.createdDuring.set(targetInfo.targetId, this.current);
          }
        });
        await session.send('Target.setDiscoverTargets', { discover: true });
        return session;
      } catch {
        return undefined;
      }
    })();
    return this.discovery;
  }

  /** The DevTools target id of a page, or undefined when it cannot be read. */
  private async targetIdOf(page: Page, context: BrowserContext): Promise<string | undefined> {
    try {
      const session = await context.newCDPSession(page);
      const { targetInfo } = await session.send('Target.getTargetInfo');
      await session.detach().catch(() => undefined);
      return targetInfo.targetId;
    } catch {
      return undefined;
    }
  }

  private async createContext(login: string | undefined): Promise<BrowserContext> {
    await this.startDiscovery();
    const { profile } = this.options;
    const storageState =
      login === undefined
        ? undefined
        : await this.options.logins(login, this.current?.stepId ?? this.lastStepId);
    const context = await this.options.browser.newContext({
      viewport: profile.settings.viewport,
      locale: profile.settings.locale,
      timezoneId: profile.settings.timezone,
      deviceScaleFactor: 1,
      baseURL: profile.baseUrl,
      ...(storageState === undefined ? {} : { storageState }),
    });
    context.on('response', (response) => {
      this.options.responses?.add(response);
    });
    context.on('page', (page) => {
      this.onNewPage(page, context);
    });
    return context;
  }

  private async newPage(context: BrowserContext): Promise<Page> {
    this.ownPending.set(context, (this.ownPending.get(context) ?? 0) + 1);
    this.opened += 1;
    return context.newPage();
  }

  private register(name: string, page: Page, context: BrowserContext): void {
    this.pages.set(name, page);
    this.contextOf.set(name, context);
  }

  /** A page appeared in a context: the engine's own, or one the application opened. */
  private onNewPage(page: Page, context: BrowserContext): void {
    const own = this.ownPending.get(context) ?? 0;
    if (own > 0) {
      this.ownPending.set(context, own - 1);
      return;
    }
    this.opened += 1;
    const order = this.opened;
    const reportedDuring = this.current;
    void this.targetIdOf(page, context).then((targetId) => {
      // The step running when the tab was created; else when Playwright reported it.
      const step =
        targetId !== undefined && this.createdDuring.has(targetId)
          ? this.createdDuring.get(targetId)
          : reportedDuring;
      const stepId = step?.stepId ?? this.lastStepId;
      const opens = step?.opens;
      const named = opens !== undefined && !this.pages.has(opens);
      const name = named ? opens : `tab-${String(order)}`;
      this.register(name, page, context);
      this.options.emit('pageOpened', {
        testId: this.options.testId,
        stepId,
        page: name,
        automatic: !named,
      });
      if (!named) {
        this.options.emit('log', {
          level: 'warn',
          code: 'UnnamedPage',
          message: `A new page opened during ${stepId} and was named "${name}". Add \`opens: <name>\` to this step to use the new page.`,
          testId: this.options.testId,
          stepId,
          ...(step === undefined ? {} : { location: step.location }),
        });
      }
    });
  }
}
