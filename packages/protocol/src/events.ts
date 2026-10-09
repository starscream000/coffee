// Every event the engine sends as a notification (docs/protocol.md, "Events"),
// as Zod schemas with the inferred types (ADR 0020).

import { z } from 'zod';
import {
  ErrorInfoSchema,
  LocationSchema,
  LocatorUseSchema,
  SnapshotStatusSchema,
} from './shared.js';

/**
 * Fields every event carries: the run it belongs to and its sequence number
 * (one higher than the previous event of the same run).
 */
export const EventBaseSchema = z.looseObject({
  runId: z.string(),
  seq: z.number().int().min(0),
});

/** Fields of every event. See {@link EventBaseSchema}. */
export type EventBase = z.infer<typeof EventBaseSchema>;

const testScoped = EventBaseSchema.extend({ testId: z.string() });
const stepScoped = testScoped.extend({ stepId: z.string() });
const isoTime = z.string();

/** `runStarted`: the run's environment, browser, settings and test instances. */
export const RunStartedEventSchema = EventBaseSchema.extend({
  env: z.string(),
  browser: z.string(),
  settings: z.looseObject({
    viewport: z.looseObject({ width: z.number().int().min(1), height: z.number().int().min(1) }),
    locale: z.string(),
    timezone: z.string(),
  }),
  startedAt: isoTime,
  tests: z.array(
    z.looseObject({
      testId: z.string(),
      file: z.string(),
      name: z.string(),
      row: z.number().int().min(0).optional(),
      skip: z.string().optional(),
    }),
  ),
});

/** `runStarted`. See {@link RunStartedEventSchema}. */
export type RunStartedEvent = z.infer<typeof RunStartedEventSchema>;

/** `testStarted`: one test instance begins. */
export const TestStartedEventSchema = testScoped.extend({
  startedAt: isoTime,
});

/** `testStarted`. See {@link TestStartedEventSchema}. */
export type TestStartedEvent = z.infer<typeof TestStartedEventSchema>;

/** `stepStarted`: a step begins; `params` is its canonical long form, uninterpolated. */
export const StepStartedEventSchema = stepScoped.extend({
  parentStepId: z.string().optional(),
  section: z.enum(['before', 'steps', 'after']),
  action: z.string(),
  params: z.record(z.string(), z.unknown()),
  page: z.string(),
  title: z.string(),
  location: LocationSchema,
});

/** `stepStarted`. See {@link StepStartedEventSchema}. */
export type StepStartedEvent = z.infer<typeof StepStartedEventSchema>;

/** `stepPassed`: a step succeeded. */
export const StepPassedEventSchema = stepScoped.extend({
  durationMs: z.number().min(0),
  locators: z.array(LocatorUseSchema),
  snapshot: SnapshotStatusSchema,
});

/** `stepPassed`. See {@link StepPassedEventSchema}. */
export type StepPassedEvent = z.infer<typeof StepPassedEventSchema>;

/** `stepFailed`: a step failed (or was cancelled, with error code `Cancelled`). */
export const StepFailedEventSchema = stepScoped.extend({
  durationMs: z.number().min(0),
  error: ErrorInfoSchema,
  locators: z.array(LocatorUseSchema),
  snapshot: SnapshotStatusSchema,
});

/** `stepFailed`. See {@link StepFailedEventSchema}. */
export type StepFailedEvent = z.infer<typeof StepFailedEventSchema>;

/** `stepSkipped`: a step did not run, and why. */
export const StepSkippedEventSchema = stepScoped.extend({
  reason: z.enum(['previousFailure', 'cancelled', 'variableNotSet']),
  message: z.string(),
  variable: z.string().optional(),
});

/** `stepSkipped`. See {@link StepSkippedEventSchema}. */
export type StepSkippedEvent = z.infer<typeof StepSkippedEventSchema>;

/** `screenshotReady`: a step's screenshot file exists. */
export const ScreenshotReadyEventSchema = stepScoped.extend({
  page: z.string(),
  path: z.string(),
  width: z.number().int().min(1),
  height: z.number().int().min(1),
});

/** `screenshotReady`. See {@link ScreenshotReadyEventSchema}. */
export type ScreenshotReadyEvent = z.infer<typeof ScreenshotReadyEventSchema>;

/** `snapshotReady`: a step's page snapshot is saved and masked. */
export const SnapshotReadyEventSchema = stepScoped.extend({
  page: z.string(),
});

/** `snapshotReady`. See {@link SnapshotReadyEventSchema}. */
export type SnapshotReadyEvent = z.infer<typeof SnapshotReadyEventSchema>;

/** `pageOpened`: a new page appeared during a step. */
export const PageOpenedEventSchema = stepScoped.extend({
  page: z.string(),
  automatic: z.boolean(),
});

/** `pageOpened`. See {@link PageOpenedEventSchema}. */
export type PageOpenedEvent = z.infer<typeof PageOpenedEventSchema>;

/** `log`: a message for the tester, optionally tied to a test, step and location. */
export const LogEventSchema = EventBaseSchema.extend({
  level: z.enum(['debug', 'info', 'warn', 'error']),
  message: z.string(),
  code: z.string().optional(),
  testId: z.string().optional(),
  stepId: z.string().optional(),
  location: LocationSchema.optional(),
});

/** `log`. See {@link LogEventSchema}. */
export type LogEvent = z.infer<typeof LogEventSchema>;

/** `testSkipped`: a test instance with `skip` was not run. */
export const TestSkippedEventSchema = testScoped.extend({
  reason: z.string(),
});

/** `testSkipped`. See {@link TestSkippedEventSchema}. */
export type TestSkippedEvent = z.infer<typeof TestSkippedEventSchema>;

/** `testFinished`: a test instance ended. */
export const TestFinishedEventSchema = testScoped.extend({
  status: z.enum(['passed', 'failed', 'cancelled']),
  durationMs: z.number().min(0),
});

/** `testFinished`. See {@link TestFinishedEventSchema}. */
export type TestFinishedEvent = z.infer<typeof TestFinishedEventSchema>;

/** `runFinished`: the run ended; totals count test instances. */
export const RunFinishedEventSchema = EventBaseSchema.extend({
  status: z.enum(['passed', 'failed', 'cancelled']),
  durationMs: z.number().min(0),
  totals: z.looseObject({
    passed: z.number().int().min(0),
    failed: z.number().int().min(0),
    cancelled: z.number().int().min(0),
    skipped: z.number().int().min(0),
  }),
});

/** `runFinished`. See {@link RunFinishedEventSchema}. */
export type RunFinishedEvent = z.infer<typeof RunFinishedEventSchema>;

/**
 * Every event name with the schema of its params, in the order of
 * `docs/protocol.md`.
 *
 * @example
 * ```ts
 * const event = EVENTS.stepFailed.parse(notification.params);
 * ```
 */
export const EVENTS = {
  runStarted: RunStartedEventSchema,
  testStarted: TestStartedEventSchema,
  stepStarted: StepStartedEventSchema,
  stepPassed: StepPassedEventSchema,
  stepFailed: StepFailedEventSchema,
  stepSkipped: StepSkippedEventSchema,
  screenshotReady: ScreenshotReadyEventSchema,
  snapshotReady: SnapshotReadyEventSchema,
  pageOpened: PageOpenedEventSchema,
  log: LogEventSchema,
  testSkipped: TestSkippedEventSchema,
  testFinished: TestFinishedEventSchema,
  runFinished: RunFinishedEventSchema,
} as const;

/** Name of an event, such as `"stepPassed"`. */
export type EventName = keyof typeof EVENTS;

/** Every event name, in the order of `docs/protocol.md`. */
export const EVENT_NAMES = Object.keys(EVENTS) as readonly EventName[];
