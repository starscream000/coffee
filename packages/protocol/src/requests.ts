// Parameters and results of every request a client can send (docs/protocol.md,
// "Requests"), as Zod schemas with the inferred types (ADR 0020).

import { z } from 'zod';
import { DiagnosticSchema } from './shared.js';

/** Parameters of a request that takes none. Unknown fields are ignored. */
export const NoParamsSchema = z.looseObject({});

/** Result of a request that answers `null`. */
export const NullResultSchema = z.null();

// initialize

/** Parameters of `initialize`: the protocol version the client speaks. */
export const InitializeParamsSchema = z.looseObject({
  protocolVersion: z.string(),
  client: z.looseObject({ name: z.string(), version: z.string() }),
});

/** Parameters of `initialize`. See {@link InitializeParamsSchema}. */
export type InitializeParams = z.infer<typeof InitializeParamsSchema>;

/** Result of `initialize`: the engine's versions and capabilities. */
export const InitializeResultSchema = z.looseObject({
  protocolVersion: z.string(),
  engine: z.looseObject({ name: z.string(), version: z.string() }),
  capabilities: z.looseObject({ browsers: z.array(z.string()) }),
});

/** Result of `initialize`. See {@link InitializeResultSchema}. */
export type InitializeResult = z.infer<typeof InitializeResultSchema>;

// openProject

/** Parameters of `openProject`: the project's root folder. */
export const OpenProjectParamsSchema = z.looseObject({
  root: z.string(),
});

/** Parameters of `openProject`. See {@link OpenProjectParamsSchema}. */
export type OpenProjectParams = z.infer<typeof OpenProjectParamsSchema>;

/** Result of `openProject`: what the config defines, and any problems. */
export const OpenProjectResultSchema = z.looseObject({
  root: z.string(),
  configFile: z.string(),
  environments: z.array(z.string()),
  defaultEnvironment: z.string().optional(),
  logins: z.array(z.string()),
  diagnostics: z.array(DiagnosticSchema),
});

/** Result of `openProject`. See {@link OpenProjectResultSchema}. */
export type OpenProjectResult = z.infer<typeof OpenProjectResultSchema>;

// listTests

/** Parameters of `listTests`: an optional tag filter. */
export const ListTestsParamsSchema = z.looseObject({
  tags: z.array(z.string()).optional(),
});

/** Parameters of `listTests`. See {@link ListTestsParamsSchema}. */
export type ListTestsParams = z.infer<typeof ListTestsParamsSchema>;

/** One test file as `listTests` reports it. */
export const TestSummarySchema = z.looseObject({
  file: z.string(),
  name: z.string(),
  tags: z.array(z.string()),
  rows: z.number().int().min(1),
});

/** One test file. See {@link TestSummarySchema}. */
export type TestSummary = z.infer<typeof TestSummarySchema>;

/** Result of `listTests`. */
export const ListTestsResultSchema = z.looseObject({
  tests: z.array(TestSummarySchema),
});

/** Result of `listTests`. See {@link ListTestsResultSchema}. */
export type ListTestsResult = z.infer<typeof ListTestsResultSchema>;

// listActions

/** Where an action comes from: built in, or a file in the user's repository. */
export const ActionSourceSchema = z.discriminatedUnion('kind', [
  z.looseObject({ kind: z.literal('builtin') }),
  z.looseObject({ kind: z.literal('file'), file: z.string() }),
]);

/** Where an action comes from. See {@link ActionSourceSchema}. */
export type ActionSource = z.infer<typeof ActionSourceSchema>;

/** One action as `listActions` reports it: its spec, with parameters as JSON Schema. */
export const ActionSummarySchema = z.looseObject({
  name: z.string(),
  description: z.string(),
  shorthand: z.string().nullable(),
  paramsSchema: z.record(z.string(), z.unknown()),
  source: ActionSourceSchema,
});

/** One action. See {@link ActionSummarySchema}. */
export type ActionSummary = z.infer<typeof ActionSummarySchema>;

/** Result of `listActions`. */
export const ListActionsResultSchema = z.looseObject({
  actions: z.array(ActionSummarySchema),
});

/** Result of `listActions`. See {@link ListActionsResultSchema}. */
export type ListActionsResult = z.infer<typeof ListActionsResultSchema>;

// validate

/** Parameters of `validate`: files on disk, or one unsaved editor buffer. */
export const ValidateParamsSchema = z.union(
  [
    z.looseObject({ files: z.array(z.string()) }),
    z.looseObject({ content: z.looseObject({ file: z.string(), text: z.string() }) }),
  ],
  { error: 'it needs "files" (a list of paths) or "content" (a file name and its text)' },
);

/** Parameters of `validate`. See {@link ValidateParamsSchema}. */
export type ValidateParams = z.infer<typeof ValidateParamsSchema>;

/** Result of `validate`: every problem found. */
export const ValidateResultSchema = z.looseObject({
  diagnostics: z.array(DiagnosticSchema),
});

/** Result of `validate`. See {@link ValidateResultSchema}. */
export type ValidateResult = z.infer<typeof ValidateResultSchema>;

// startRun

/** Parameters of `startRun`: what to run, where, and how. */
export const StartRunParamsSchema = z.looseObject({
  files: z.array(z.string()).optional(),
  tags: z.array(z.string()).optional(),
  env: z.string().optional(),
  options: z
    .looseObject({
      headed: z.boolean().optional(),
      browser: z.string().optional(),
      refreshLogins: z.boolean().optional(),
    })
    .optional(),
});

/** Parameters of `startRun`. See {@link StartRunParamsSchema}. */
export type StartRunParams = z.infer<typeof StartRunParamsSchema>;

/** Result of `startRun`, returned before the run starts. */
export const StartRunResultSchema = z.looseObject({
  runId: z.string(),
  resultsDir: z.string(),
});

/** Result of `startRun`. See {@link StartRunResultSchema}. */
export type StartRunResult = z.infer<typeof StartRunResultSchema>;

// cancelRun

/** Parameters of `cancelRun`. */
export const CancelRunParamsSchema = z.looseObject({
  runId: z.string(),
});

/** Parameters of `cancelRun`. See {@link CancelRunParamsSchema}. */
export type CancelRunParams = z.infer<typeof CancelRunParamsSchema>;

// openSnapshot

/** Parameters of `openSnapshot`: which step's page state to open. */
export const OpenSnapshotParamsSchema = z.looseObject({
  runId: z.string(),
  testId: z.string(),
  stepId: z.string(),
});

/** Parameters of `openSnapshot`. See {@link OpenSnapshotParamsSchema}. */
export type OpenSnapshotParams = z.infer<typeof OpenSnapshotParamsSchema>;

/**
 * Every request method with the schemas of its parameters and result.
 *
 * @example
 * ```ts
 * const params = REQUESTS.initialize.params.parse(message.params);
 * ```
 */
export const REQUESTS = {
  initialize: { params: InitializeParamsSchema, result: InitializeResultSchema },
  shutdown: { params: NoParamsSchema, result: NullResultSchema },
  openProject: { params: OpenProjectParamsSchema, result: OpenProjectResultSchema },
  listTests: { params: ListTestsParamsSchema, result: ListTestsResultSchema },
  listActions: { params: NoParamsSchema, result: ListActionsResultSchema },
  validate: { params: ValidateParamsSchema, result: ValidateResultSchema },
  startRun: { params: StartRunParamsSchema, result: StartRunResultSchema },
  cancelRun: { params: CancelRunParamsSchema, result: NullResultSchema },
  openSnapshot: { params: OpenSnapshotParamsSchema, result: NullResultSchema },
} as const;

/** Name of a request method, such as `"initialize"`. */
export type RequestMethod = keyof typeof REQUESTS;

/** Parameters of request method `M`, after checking. */
export type RequestParams<M extends RequestMethod> = z.infer<(typeof REQUESTS)[M]['params']>;

/** Result of request method `M`. */
export type RequestResult<M extends RequestMethod> = z.infer<(typeof REQUESTS)[M]['result']>;

/** Every request method name, in the order of `docs/protocol.md`. */
export const REQUEST_METHODS = Object.keys(REQUESTS) as readonly RequestMethod[];
