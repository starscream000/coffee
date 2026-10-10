// The flow action (docs/actions.md, "Flows"): call. The runner runs a call
// itself (runner/step-runner.ts), because the flow's steps are steps of their
// own with events; this definition gives `call` its spec and marks it as
// runnable.

import { StepError } from '../../runner/errors.js';
import { defineAction } from '../../sdk/define-action.js';
import { specOf } from './shared.js';

/** `call`: runs a flow with parameters; the runner runs it, never this `run`. */
export const call = defineAction({
  ...specOf('call'),
  run() {
    return Promise.reject(
      new StepError('CallNotRunnable', 'Only the runner can run "call", as a step of its own.'),
    );
  },
});
