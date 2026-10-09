// Schemas of targets and their locator candidates (docs/step-format.md,
// "Targets"; ADR 0010): the short form (a list of candidates), the long form
// with `frame` and `within`, and references by name.

import { z } from 'zod';
import { TargetNameSchema } from './common.js';

/** Candidate kinds, in the order the recorder writes them. */
export const CANDIDATE_KINDS = ['role', 'label', 'placeholder', 'text', 'testId', 'css'] as const;

const TEXT_LIKE = new Set(['role', 'label', 'placeholder', 'text']);

/**
 * One locator candidate: exactly one kind (`role`, `label`, `placeholder`,
 * `text`, `testId`, `css`), `name` only with `role`, `exact` only with the
 * text-like kinds, and an optional `nth`.
 */
export const CandidateSchema = z
  .strictObject({
    role: z.string().min(1).optional(),
    name: z.string().optional(),
    label: z.string().min(1).optional(),
    placeholder: z.string().min(1).optional(),
    text: z.string().min(1).optional(),
    testId: z.string().min(1).optional(),
    css: z.string().min(1).optional(),
    exact: z.boolean().optional(),
    nth: z.number().int().min(0).optional(),
  })
  .superRefine((candidate, ctx) => {
    const kinds = CANDIDATE_KINDS.filter((kind) => candidate[kind] !== undefined);
    if (kinds.length !== 1) {
      ctx.addIssue({
        code: 'custom',
        message:
          kinds.length === 0
            ? `A candidate needs exactly one of ${CANDIDATE_KINDS.join(', ')}`
            : `A candidate has exactly one kind, but this one has ${kinds.join(' and ')}; split it into separate candidates`,
        params: { diagnostic: 'InvalidCandidate' },
      });
      return;
    }
    const [kind] = kinds;
    if (candidate.name !== undefined && kind !== 'role') {
      ctx.addIssue({
        code: 'custom',
        path: ['name'],
        message: '"name" belongs to a "role" candidate only',
        params: { diagnostic: 'InvalidCandidate' },
      });
    }
    if (candidate.exact !== undefined && (kind === undefined || !TEXT_LIKE.has(kind))) {
      ctx.addIssue({
        code: 'custom',
        path: ['exact'],
        message: '"exact" applies only to role, label, placeholder and text candidates',
        params: { diagnostic: 'InvalidCandidate' },
      });
    }
  });

/** One locator candidate. See {@link CandidateSchema}. */
export type Candidate = z.infer<typeof CandidateSchema>;

/**
 * A target written in the long form: candidates, and where to look for them.
 */
export interface LongTarget {
  /** Ordered candidates; the first that matches exactly one element wins. */
  candidates: Candidate[];
  /** The `<iframe>` the element is in. */
  frame?: TargetValue | undefined;
  /** An element the element is inside. */
  within?: TargetValue | undefined;
}

/**
 * A target as written in a step file: a name, a list of candidates (short
 * form) or a long-form mapping.
 */
export type TargetValue = string | Candidate[] | LongTarget;

/**
 * Schema of a target value. Recursive through `frame` and `within`; giving
 * both on one target is an error ("put frame on the outer target").
 */
export const TargetSchema: z.ZodType<TargetValue> = z.union([
  TargetNameSchema,
  z.array(CandidateSchema).min(1, { error: 'needs at least one candidate' }),
  z
    .strictObject({
      candidates: z.array(CandidateSchema).min(1, { error: 'needs at least one candidate' }),
      get frame(): z.ZodOptional<z.ZodType<TargetValue>> {
        return TargetSchema.optional();
      },
      get within(): z.ZodOptional<z.ZodType<TargetValue>> {
        return TargetSchema.optional();
      },
    })
    .superRefine((target, ctx) => {
      if (target.frame !== undefined && target.within !== undefined) {
        ctx.addIssue({
          code: 'custom',
          path: ['frame'],
          message:
            'A target cannot have both "frame" and "within"; it takes its frame from the "within" target, so put "frame" on the outer target',
          params: { diagnostic: 'FrameWithWithin' },
        });
      }
    }),
]);

/**
 * Brings any target value into the long form.
 *
 * @param target - A long-form target or a list of candidates. Names are
 *   resolved by the caller first.
 * @returns The long form.
 */
export function toLongTarget(target: Candidate[] | LongTarget): LongTarget {
  return Array.isArray(target) ? { candidates: target } : target;
}
