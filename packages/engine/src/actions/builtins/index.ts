// The built-in actions that can run, defined with `defineAction` like user
// actions (docs/actions.md, "Built-in actions"). Each takes its spec from
// BUILTIN_SPECS, honours ctx.signal and gives every Playwright call at most
// the step's remaining time. A built-in without a `run` here is a spec only,
// and a step that calls it fails with NotImplemented.

import type { RunnableAction } from '../../sdk/define-action.js';
import type { ActionSpec } from '../action-spec.js';
import { BUILTIN_SPECS } from '../builtin-specs.js';
import { expectCount, expectText, expectUrl, expectValue, expectVisible } from './assertions.js';
import { extract, set } from './data.js';
import { api, expectResponse, mock, waitResponse } from './http.js';
import { check, click, drag, fill, hover, press, select, upload } from './interaction.js';
import { back, goto, reload } from './navigation.js';
import { waitElement, waitUrl } from './waiting.js';

/** The built-in actions that have a `run`, by name. */
const IMPLEMENTED: ReadonlyMap<string, RunnableAction> = new Map(
  [
    goto,
    back,
    reload,
    click,
    fill,
    select,
    check,
    hover,
    press,
    upload,
    drag,
    waitElement,
    waitUrl,
    waitResponse,
    expectVisible,
    expectText,
    expectValue,
    expectUrl,
    expectCount,
    expectResponse,
    set,
    extract,
    api,
    mock,
  ].map((action) => [action.name, action as RunnableAction]),
);

/**
 * Every built-in action in the order of BUILTIN_SPECS: the runnable ones as
 * actions, the others as specs only.
 *
 * @example
 * ```ts
 * const registry = new ActionRegistry(BUILTIN_ACTIONS);
 * ```
 */
export const BUILTIN_ACTIONS: readonly (ActionSpec | RunnableAction)[] = BUILTIN_SPECS.map(
  (spec) => IMPLEMENTED.get(spec.name) ?? spec,
);
