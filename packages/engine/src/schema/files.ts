// Schemas of whole step files: tests, flows, shared targets and the project
// config (docs/step-format.md). Steps are checked one by one afterwards
// (stepfile/steps.ts), so here they are only "a list".

import { z } from 'zod';
import {
  AnyMappingSchema,
  DurationSchema,
  PageNameSchema,
  TargetNameSchema,
  VarNameSchema,
} from './common.js';
import { TargetSchema } from './targets.js';

/** The only format version this engine reads. */
export const SUPPORTED_VERSION = 1;

const StepListSchema = z.array(z.unknown());
const TargetsMapSchema = z.record(TargetNameSchema, TargetSchema);
const NonEmptyText = z.string().trim().min(1, { error: 'must not be empty' });

/** A test file (`*.test.yaml`). */
export const TestFileSchema = z.strictObject({
  version: z.literal(SUPPORTED_VERSION),
  name: NonEmptyText,
  description: z.string().optional(),
  tags: z.array(NonEmptyText).optional(),
  skip: NonEmptyText.optional(),
  login: z.string().min(1).optional(),
  freshLogin: z.boolean().optional(),
  pages: z
    .record(PageNameSchema, z.strictObject({ login: z.string().min(1).optional() }))
    .optional(),
  data: z
    .union([
      z.string().min(1),
      z.array(AnyMappingSchema).min(1, { error: 'needs at least one row' }),
    ])
    .optional(),
  vars: z.record(VarNameSchema, z.unknown()).optional(),
  targets: TargetsMapSchema.optional(),
  before: StepListSchema.optional(),
  steps: StepListSchema.min(1, { error: 'needs at least one step' }),
  after: StepListSchema.optional(),
});

/** A test file's data. See {@link TestFileSchema}. */
export type TestFile = z.infer<typeof TestFileSchema>;

/** A flow parameter declaration: its type and an optional default. */
export const FlowParamSchema = z
  .strictObject({
    type: z.enum(['string', 'number', 'boolean']),
    default: z.union([z.string(), z.number(), z.boolean()]).optional(),
  })
  .superRefine((param, ctx) => {
    if (param.default !== undefined && typeof param.default !== param.type) {
      ctx.addIssue({
        code: 'custom',
        path: ['default'],
        message: `The default must be a ${param.type}`,
        params: { diagnostic: 'InvalidValue' },
      });
    }
  });

/** A flow parameter. See {@link FlowParamSchema}. */
export type FlowParam = z.infer<typeof FlowParamSchema>;

/** A flow file (`*.flow.yaml`). */
export const FlowFileSchema = z.strictObject({
  version: z.literal(SUPPORTED_VERSION),
  name: NonEmptyText,
  description: z.string().optional(),
  params: z.record(VarNameSchema, FlowParamSchema).optional(),
  outputs: z.array(VarNameSchema).optional(),
  targets: TargetsMapSchema.optional(),
  steps: StepListSchema.min(1, { error: 'needs at least one step' }),
});

/** A flow file's data. See {@link FlowFileSchema}. */
export type FlowFile = z.infer<typeof FlowFileSchema>;

/** A shared targets file (`*.targets.yaml`). */
export const TargetsFileSchema = z.strictObject({
  version: z.literal(SUPPORTED_VERSION),
  targets: TargetsMapSchema,
});

/** A shared targets file's data. See {@link TargetsFileSchema}. */
export type TargetsFile = z.infer<typeof TargetsFileSchema>;

const ViewportSchema = z.strictObject({
  width: z.number().int().min(1),
  height: z.number().int().min(1),
});

/** Settings an environment may override. */
const overridable = {
  timeout: DurationSchema.optional(),
  fallbackGrace: DurationSchema.optional(),
  snapshots: z.enum(['always', 'onFailure', 'off']).optional(),
  viewport: ViewportSchema.optional(),
  locale: z.string().min(1).optional(),
  timezone: z.string().min(1).optional(),
};

/** One environment profile in the config. */
export const EnvironmentSchema = z.strictObject({
  baseUrl: z.url({ error: 'must be an absolute URL such as http://localhost:5173' }),
  values: z.record(VarNameSchema, z.union([z.string(), z.number(), z.boolean()])).optional(),
  ...overridable,
});

/** One environment profile. See {@link EnvironmentSchema}. */
export type Environment = z.infer<typeof EnvironmentSchema>;

/** The project configuration (`cfe.config.yaml`). */
export const ConfigFileSchema = z
  .strictObject({
    version: z.literal(SUPPORTED_VERSION),
    tests: z.array(z.string().min(1)).optional(),
    flows: z.array(z.string().min(1)).optional(),
    targets: z.array(z.string().min(1)).optional(),
    actions: z.array(z.string().min(1)).optional(),
    defaults: z
      .strictObject({
        environment: z.string().min(1).optional(),
        browser: z.string().min(1).optional(),
        testIdAttribute: z.string().min(1).optional(),
        keepRuns: z.number().int().min(0).optional(),
        ...overridable,
      })
      .optional(),
    environments: z.record(
      z.string().regex(/^[A-Za-z][A-Za-z0-9_-]*$/, { error: 'must be an environment name' }),
      EnvironmentSchema,
    ),
    secrets: z
      .array(
        z
          .string()
          .regex(/^[A-Za-z_][A-Za-z0-9_]*$/, { error: 'must be an environment variable name' }),
      )
      .optional(),
    logins: z
      .record(
        z.string().regex(/^[A-Za-z][A-Za-z0-9_-]*$/, { error: 'must be a login name' }),
        z.strictObject({
          flow: z.string().min(1),
          with: AnyMappingSchema.optional(),
          maxAge: DurationSchema.optional(),
        }),
      )
      .optional(),
  })
  .superRefine((config, ctx) => {
    const names = Object.keys(config.environments);
    if (names.length === 0) {
      ctx.addIssue({
        code: 'custom',
        path: ['environments'],
        message: 'Declare at least one environment',
        params: { diagnostic: 'InvalidValue' },
      });
    }
    const wanted = config.defaults?.environment;
    if (wanted !== undefined && !names.includes(wanted)) {
      ctx.addIssue({
        code: 'custom',
        path: ['defaults', 'environment'],
        message: `"${wanted}" is not one of the environments (${names.join(', ')})`,
        params: { diagnostic: 'UnknownEnvironment' },
      });
    }
  });

/** The project configuration's data. See {@link ConfigFileSchema}. */
export type ConfigFile = z.infer<typeof ConfigFileSchema>;

/** Default globs when the config does not name its files. */
export const DEFAULT_GLOBS = {
  tests: ['**/*.test.yaml'],
  flows: ['**/*.flow.yaml'],
  targets: ['**/*.targets.yaml'],
  actions: ['actions/**/*.ts'],
} as const;

/** The kind of a step file, decided by its name. */
export type FileKind = 'test' | 'flow' | 'targets' | 'config';

/**
 * Tells a file's kind from its name (docs/step-format.md, "File kinds").
 *
 * @param path - A file path with any separators.
 * @param configFileName - The config file's name, such as `cfe.config.yaml`.
 * @returns The kind, or `undefined` when the name matches none.
 *
 * @example
 * ```ts
 * fileKindOf('tests/a.test.yaml', 'cfe.config.yaml'); // "test"
 * ```
 */
export function fileKindOf(path: string, configFileName: string): FileKind | undefined {
  const name = path.split(/[\\/]/).at(-1) ?? path;
  if (name === configFileName) return 'config';
  if (name.endsWith('.test.yaml')) return 'test';
  if (name.endsWith('.flow.yaml')) return 'flow';
  if (name.endsWith('.targets.yaml')) return 'targets';
  return undefined;
}

/** Schema of each file kind. */
export const FILE_SCHEMAS = {
  test: TestFileSchema,
  flow: FlowFileSchema,
  targets: TargetsFileSchema,
  config: ConfigFileSchema,
} as const;
