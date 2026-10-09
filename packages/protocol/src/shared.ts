// Shared protocol types used by several requests and events (docs/protocol.md,
// "Shared types"), defined once as Zod schemas (ADR 0020). Every object schema
// is loose: fields the protocol does not define are accepted and ignored.

import { z } from 'zod';

const locationShape = z.looseObject({
  file: z.string(),
  line: z.number().int().min(1),
  column: z.number().int().min(1),
});

/**
 * A position in a step file: project-relative path with forward slashes,
 * 1-based line and column. Named `Location` in generated JSON Schemas.
 */
export const LocationSchema = locationShape.meta({ id: 'Location' });

/**
 * A position in a step file. See {@link LocationSchema}.
 */
export type Location = z.infer<typeof LocationSchema>;

/**
 * One problem found while validating step files, the config or user actions.
 */
export const DiagnosticSchema = locationShape
  .extend({
    endLine: z.number().int().min(1).optional(),
    endColumn: z.number().int().min(1).optional(),
    severity: z.enum(['error', 'warning']),
    code: z.string(),
    message: z.string(),
    hint: z.string().optional(),
  })
  .meta({ id: 'Diagnostic' });

/**
 * One validation problem. See {@link DiagnosticSchema}.
 */
export type Diagnostic = z.infer<typeof DiagnosticSchema>;

/**
 * How one target of a step was resolved: which candidate matched, and how its
 * frame and containing element were found.
 */
export interface LocatorUse {
  /** Parameter name, such as `target`, `from`, `to`; `frame` or `within` when nested. */
  param: string;
  /** Target name; absent for inline targets. */
  target?: string | undefined;
  /** Index of the matching candidate, or `null` when none matched. */
  candidateIndex: number | null;
  /** The matching candidate after interpolation, secrets masked, or `null`. */
  candidate: Record<string, unknown> | null;
  /** How the target's frame was found. */
  frame?: LocatorUse | undefined;
  /** How the target's containing element was found. */
  within?: LocatorUse | undefined;
  /** Fields from newer protocol versions. */
  [field: string]: unknown;
}

/**
 * Schema of {@link LocatorUse}. Recursive through `frame` and `within`.
 */
export const LocatorUseSchema: z.ZodType<LocatorUse> = z
  .looseObject({
    param: z.string(),
    target: z.string().optional(),
    candidateIndex: z.number().int().min(0).nullable(),
    candidate: z.record(z.string(), z.unknown()).nullable(),
    get frame(): z.ZodOptional<z.ZodType<LocatorUse>> {
      return LocatorUseSchema.optional();
    },
    get within(): z.ZodOptional<z.ZodType<LocatorUse>> {
      return LocatorUseSchema.optional();
    },
  })
  .meta({ id: 'LocatorUse' });

/**
 * What happened to a step's page snapshot. A snapshot problem never changes
 * the step's own status.
 */
export const SnapshotStatusSchema = z
  .discriminatedUnion('state', [
    z.looseObject({ state: z.literal('saved') }),
    z.looseObject({ state: z.literal('skipped') }),
    z.looseObject({ state: z.literal('failed'), reason: z.string() }),
  ])
  .meta({ id: 'SnapshotStatus' });

/**
 * A step's snapshot outcome. See {@link SnapshotStatusSchema}.
 */
export type SnapshotStatus = z.infer<typeof SnapshotStatusSchema>;

/**
 * Why a step failed, as shown to the tester. Secrets are already masked.
 */
export const ErrorInfoSchema = z
  .looseObject({
    code: z.string(),
    message: z.string(),
    hint: z.string().optional(),
    location: LocationSchema.optional(),
    expected: z.unknown().optional(),
    actual: z.unknown().optional(),
    candidates: z
      .array(
        z.looseObject({
          candidate: z.record(z.string(), z.unknown()),
          matches: z.number().int().min(0),
        }),
      )
      .optional(),
  })
  .meta({ id: 'ErrorInfo' });

/**
 * A step failure. See {@link ErrorInfoSchema}.
 */
export type ErrorInfo = z.infer<typeof ErrorInfoSchema>;
