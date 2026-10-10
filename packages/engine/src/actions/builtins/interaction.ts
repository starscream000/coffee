// The interaction actions (docs/actions.md, "Interaction"): click, fill,
// select, check, hover, press, upload and drag. Each finds its target with
// ctx.locate and gives Playwright at most the step's remaining time.

import { dirname, resolve } from 'node:path';
import { remainingMs, stepFileOf } from '../../runner/context.js';
import { StepError } from '../../runner/errors.js';
import type { TargetValue } from '../../schema/targets.js';
import { defineAction } from '../../sdk/define-action.js';
import { specOf } from './shared.js';

// The parameters below were checked against each action's spec before `run`
// is called, so reading them with these shapes is safe.

type Text = string | number | boolean;

/** `click`: clicks the element a target resolves to. */
export const click = defineAction({
  ...specOf('click'),
  async run(ctx, params) {
    const { target, button, clickCount, modifiers } = params as unknown as {
      target: TargetValue;
      button?: 'left' | 'right' | 'middle';
      clickCount?: number;
      modifiers?: ('Alt' | 'Control' | 'Meta' | 'Shift')[];
    };
    const locator = await ctx.locate(target);
    await locator.click({
      ...(button === undefined ? {} : { button }),
      ...(clickCount === undefined ? {} : { clickCount }),
      ...(modifiers === undefined ? {} : { modifiers }),
      timeout: remainingMs(ctx),
    });
  },
});

/** `fill`: clears a field and types a value. */
export const fill = defineAction({
  ...specOf('fill'),
  async run(ctx, params) {
    const { target, value } = params as unknown as { target: TargetValue; value: Text };
    const locator = await ctx.locate(target);
    await locator.fill(String(value), { timeout: remainingMs(ctx) });
  },
});

/**
 * `select`: selects options of a select element. Each value matches an
 * option's value or its label; a list selects several in a multi-select.
 */
export const select = defineAction({
  ...specOf('select'),
  async run(ctx, params) {
    const { target, option } = params as unknown as { target: TargetValue; option: Text | Text[] };
    const locator = await ctx.locate(target);
    const options = (Array.isArray(option) ? option : [option]).map(String);
    await locator.selectOption(options, { timeout: remainingMs(ctx) });
  },
});

/** `check`: checks a checkbox or radio button, or unchecks it with `checked: false`. */
export const check = defineAction({
  ...specOf('check'),
  async run(ctx, params) {
    const { target, checked } = params as unknown as { target: TargetValue; checked?: boolean };
    const locator = await ctx.locate(target);
    await locator.setChecked(checked ?? true, { timeout: remainingMs(ctx) });
  },
});

/** `hover`: moves the mouse over an element. */
export const hover = defineAction({
  ...specOf('hover'),
  async run(ctx, params) {
    const { target } = params as unknown as { target: TargetValue };
    const locator = await ctx.locate(target);
    await locator.hover({ timeout: remainingMs(ctx) });
  },
});

/** `press`: presses a key or a chord such as `Control+A`, on a target or the focused element. */
export const press = defineAction({
  ...specOf('press'),
  async run(ctx, params) {
    const { key, target } = params as unknown as { key: string; target?: TargetValue };
    if (target === undefined) {
      await ctx.page.keyboard.press(key);
      return;
    }
    const locator = await ctx.locate(target);
    await locator.press(key, { timeout: remainingMs(ctx) });
  },
});

/** `upload`: sets the files of a file input; paths are relative to the step file, `[]` clears. */
export const upload = defineAction({
  ...specOf('upload'),
  async run(ctx, params) {
    const { target, files } = params as unknown as {
      target: TargetValue;
      files: string | string[];
    };
    const stepFile = stepFileOf(ctx);
    if (stepFile === undefined) {
      throw new StepError(
        'UploadUnavailable',
        'This step has no file to resolve upload paths from.',
      );
    }
    const paths = (Array.isArray(files) ? files : [files]).map((file) =>
      resolve(dirname(stepFile), file),
    );
    const locator = await ctx.locate(target);
    await locator.setInputFiles(paths, { timeout: remainingMs(ctx) });
  },
});

/** `drag`: drags one element onto another. */
export const drag = defineAction({
  ...specOf('drag'),
  async run(ctx, params) {
    const { from, to } = params as unknown as { from: TargetValue; to: TargetValue };
    const source = await ctx.locate(from);
    const destination = await ctx.locate(to);
    await source.dragTo(destination, { timeout: remainingMs(ctx) });
  },
});
