// The ActionSpec of every built-in action of docs/actions.md (ADR 0021). The
// validator needs only these; each action's `run` arrives in a later branch.

import { z } from 'zod';
import { AnyMappingSchema, VarNameSchema, interpolatable } from '../schema/common.js';
import { target, type ActionSpec } from './action-spec.js';

const Url = z.string().min(1, { error: 'must not be empty' });

/** Compiles a pattern; returns the error message, or undefined when it is valid. */
function regexError(pattern: string): string | undefined {
  try {
    new RegExp(pattern);
    return undefined;
  } catch (error) {
    return error instanceof Error ? error.message : String(error);
  }
}

/**
 * Text that must be a valid regular expression. A value with ${…} is checked
 * when the step runs, after interpolation, not here.
 */
const Regex = z.string().superRefine((value, ctx) => {
  if (value.includes('${')) return;
  const error = regexError(value);
  if (error !== undefined) {
    ctx.addIssue({
      code: 'custom',
      message: `"${value}" is not a valid regular expression: ${error}`,
      params: { diagnostic: 'InvalidRegex' },
    });
  }
});

/**
 * A URL pattern: a glob, or a regular expression written as /regex/. A value
 * with ${…} is checked when the step runs, after interpolation.
 */
const UrlPattern = Url.superRefine((value, ctx) => {
  if (value.includes('${') || value.length < 2 || !value.startsWith('/') || !value.endsWith('/'))
    return;
  const error = regexError(value.slice(1, -1));
  if (error !== undefined) {
    ctx.addIssue({
      code: 'custom',
      message: `"${value}" is written as /regex/ but is not a valid regular expression: ${error}`,
      params: { diagnostic: 'InvalidRegex' },
    });
  }
});

/**
 * `extract`'s pattern: a valid regular expression with exactly one capturing
 * group. A value with ${…} is checked when the step runs, after interpolation.
 */
const ExtractPattern = z
  .string()
  .min(1)
  .superRefine((value, ctx) => {
    if (value.includes('${')) return;
    const error = regexError(value);
    if (error !== undefined) {
      ctx.addIssue({
        code: 'custom',
        message: `"${value}" is not a valid regular expression: ${error}`,
        params: { diagnostic: 'InvalidRegex' },
      });
      return;
    }
    const groups = (new RegExp(`${value}|`).exec('')?.length ?? 1) - 1;
    if (groups !== 1) {
      ctx.addIssue({
        code: 'custom',
        message: `The pattern must have exactly one capturing group ( … ), the part to extract; it has ${String(groups)}`,
        params: { diagnostic: 'InvalidRegex' },
      });
    }
  });
const Status = interpolatable(z.number().int().min(100).max(599));
const Method = z.enum(['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS']);
const Count = interpolatable(z.number().int().min(0));
const Text = z.union([z.string(), z.number(), z.boolean()]);

/** Exactly one of `equals`, `contains`, `matches` must be given. */
function oneMatcher(
  value: { equals?: unknown; contains?: unknown; matches?: unknown },
  ctx: z.core.$RefinementCtx,
): void {
  const given = (['equals', 'contains', 'matches'] as const).filter(
    (key) => value[key] !== undefined,
  );
  if (given.length !== 1) {
    ctx.addIssue({
      code: 'custom',
      message:
        given.length === 0
          ? 'Give exactly one of "equals", "contains" or "matches"'
          : `Give exactly one of "equals", "contains" or "matches", not ${given.map((key) => `"${key}"`).join(' and ')}`,
      params: { diagnostic: 'InvalidParameters' },
    });
  }
}

/** At most one of the given keys may be present. */
function atMostOne(keys: readonly string[]) {
  return (value: Record<string, unknown>, ctx: z.core.$RefinementCtx): void => {
    const given = keys.filter((key) => value[key] !== undefined);
    if (given.length > 1) {
      ctx.addIssue({
        code: 'custom',
        message: `Give at most one of ${keys.map((key) => `"${key}"`).join(', ')}, not ${given.map((key) => `"${key}"`).join(' and ')}`,
        params: { diagnostic: 'InvalidParameters' },
      });
    }
  };
}

const matchers = {
  equals: Text.optional(),
  contains: z.string().optional(),
  matches: Regex.optional(),
};

/**
 * Every built-in action's spec, in the order of `docs/actions.md`.
 *
 * @example
 * ```ts
 * const goto = BUILTIN_SPECS.find((spec) => spec.name === 'goto');
 * ```
 */
export const BUILTIN_SPECS: readonly ActionSpec[] = [
  // Navigation
  {
    name: 'goto',
    description: 'Opens a URL in the page; relative URLs resolve against the environment base URL.',
    shorthand: 'url',
    params: z.strictObject({
      url: Url,
      waitUntil: z.enum(['load', 'domcontentloaded', 'commit']).optional(),
    }),
  },
  {
    name: 'back',
    description: 'Goes back one page in the browser history.',
    params: z.strictObject({}),
  },
  { name: 'reload', description: 'Reloads the page.', params: z.strictObject({}) },
  // Interaction
  {
    name: 'click',
    description: 'Clicks an element.',
    shorthand: 'target',
    params: z.strictObject({
      target: target(),
      button: z.enum(['left', 'right', 'middle']).optional(),
      clickCount: interpolatable(z.number().int().min(1)).optional(),
      modifiers: z.array(z.enum(['Alt', 'Control', 'Meta', 'Shift'])).optional(),
    }),
  },
  {
    name: 'fill',
    description: 'Clears a field and types a value.',
    params: z.strictObject({ target: target(), value: Text }),
  },
  {
    name: 'select',
    description: 'Selects an option, by value or label, in a select element.',
    params: z.strictObject({ target: target(), option: z.union([Text, z.array(Text).min(1)]) }),
  },
  {
    name: 'check',
    description: 'Checks a checkbox or radio button, or unchecks it with checked: false.',
    shorthand: 'target',
    params: z.strictObject({ target: target(), checked: interpolatable(z.boolean()).optional() }),
  },
  {
    name: 'hover',
    description: 'Moves the mouse over an element.',
    shorthand: 'target',
    params: z.strictObject({ target: target() }),
  },
  {
    name: 'press',
    description: 'Presses a key or chord, on an element or on the focused element.',
    shorthand: 'key',
    params: z.strictObject({ key: z.string().min(1), target: target().optional() }),
  },
  {
    name: 'upload',
    description: 'Sets the files of a file input; paths are relative to the step file.',
    params: z.strictObject({
      target: target(),
      files: z.union([z.string().min(1), z.array(z.string().min(1))]),
    }),
  },
  {
    name: 'drag',
    description: 'Drags one element onto another.',
    params: z.strictObject({ from: target(), to: target() }),
  },
  // Waiting
  {
    name: 'wait.element',
    description: 'Waits until an element is visible, hidden, attached or detached.',
    shorthand: 'target',
    params: z.strictObject({
      target: target(),
      state: z.enum(['visible', 'hidden', 'attached', 'detached']).optional(),
    }),
  },
  {
    name: 'wait.url',
    description: 'Waits until the page URL matches a pattern.',
    shorthand: 'url',
    params: z.strictObject({ url: UrlPattern }),
  },
  {
    name: 'wait.response',
    description:
      'Waits for a response whose URL matches, including one that arrived since the previous step started.',
    shorthand: 'url',
    params: z.strictObject({
      url: UrlPattern,
      method: Method.optional(),
      status: Status.optional(),
      as: VarNameSchema.optional(),
    }),
  },
  // Assertions
  {
    name: 'expect.visible',
    description: 'Checks that an element is visible, or hidden with visible: false.',
    shorthand: 'target',
    params: z.strictObject({ target: target(), visible: interpolatable(z.boolean()).optional() }),
  },
  {
    name: 'expect.text',
    description: "Checks an element's text.",
    params: z
      .strictObject({ target: target(), ...matchers, ignoreCase: z.boolean().optional() })
      .superRefine(oneMatcher),
  },
  {
    name: 'expect.value',
    description: "Checks a field's value.",
    params: z.strictObject({ target: target(), ...matchers }).superRefine(oneMatcher),
  },
  {
    name: 'expect.url',
    description: "Checks the page's URL.",
    shorthand: 'equals',
    params: z.strictObject({ ...matchers }).superRefine(oneMatcher),
  },
  {
    name: 'expect.count',
    description: 'Checks how many elements a target matches.',
    params: z
      .strictObject({
        target: target(),
        equals: Count.optional(),
        min: Count.optional(),
        max: Count.optional(),
      })
      .superRefine((value, ctx) => {
        if (value.equals === undefined && value.min === undefined && value.max === undefined) {
          ctx.addIssue({
            code: 'custom',
            message: 'Give "equals", or "min" and/or "max"',
            params: { diagnostic: 'InvalidParameters' },
          });
        } else if (
          value.equals !== undefined &&
          (value.min !== undefined || value.max !== undefined)
        ) {
          ctx.addIssue({
            code: 'custom',
            message: 'Give either "equals" or "min"/"max", not both',
            params: { diagnostic: 'InvalidParameters' },
          });
        }
      }),
  },
  {
    name: 'expect.response',
    description: 'Checks a response: its status, part of its JSON, or text in its body.',
    params: z.strictObject({
      url: UrlPattern,
      method: Method.optional(),
      status: Status.optional(),
      json: z.unknown().optional(),
      contains: z.string().optional(),
    }),
  },
  // Variables and data
  {
    name: 'set',
    description: 'Sets one or more variables.',
    params: z.record(VarNameSchema, z.unknown()).refine((value) => Object.keys(value).length > 0, {
      error: 'needs at least one variable',
    }),
  },
  {
    name: 'extract',
    description: "Reads an element's text, value or attribute into a variable.",
    params: z
      .strictObject({
        target: target(),
        as: VarNameSchema,
        from: z.enum(['text', 'value', 'attribute']).optional(),
        attribute: z.string().min(1).optional(),
        pattern: ExtractPattern.optional(),
      })
      .superRefine((value, ctx) => {
        if (value.from === 'attribute' && value.attribute === undefined) {
          ctx.addIssue({
            code: 'custom',
            message: 'from: attribute needs "attribute" (the attribute name)',
            params: { diagnostic: 'InvalidParameters' },
          });
        }
        if (value.from !== 'attribute' && value.attribute !== undefined) {
          ctx.addIssue({
            code: 'custom',
            path: ['attribute'],
            message: '"attribute" is used only with from: attribute',
            params: { diagnostic: 'InvalidParameters' },
          });
        }
      }),
  },
  // HTTP
  {
    name: 'api',
    description:
      'Sends an HTTP request signed in like the page, and optionally stores the response.',
    params: z
      .strictObject({
        method: Method.optional(),
        url: Url,
        headers: z.record(z.string(), Text).optional(),
        query: z.record(z.string(), Text).optional(),
        json: z.unknown().optional(),
        form: z.record(z.string(), Text).optional(),
        body: z.string().optional(),
        as: VarNameSchema.optional(),
        status: Status.optional(),
      })
      .superRefine(atMostOne(['json', 'form', 'body'])),
  },
  {
    name: 'mock',
    description: 'Answers matching requests with a fixed response until the test ends.',
    params: z
      .strictObject({
        url: UrlPattern,
        method: Method.optional(),
        status: Status.optional(),
        headers: z.record(z.string(), Text).optional(),
        json: z.unknown().optional(),
        body: z.string().optional(),
        file: z.string().min(1).optional(),
        times: interpolatable(z.number().int().min(1)).optional(),
      })
      .superRefine(atMostOne(['json', 'body', 'file'])),
  },
  // Flows
  {
    name: 'call',
    description: 'Runs a flow with parameters; its outputs become variables.',
    shorthand: 'flow',
    params: z.strictObject({ flow: z.string().min(1), with: AnyMappingSchema.optional() }),
  },
];

/** Built-in specs by name. */
export const BUILTIN_SPECS_BY_NAME: ReadonlyMap<string, ActionSpec> = new Map(
  BUILTIN_SPECS.map((spec) => [spec.name, spec]),
);
