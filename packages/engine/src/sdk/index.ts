// The SDK for user actions, published as `@cfe/engine/sdk` (docs/actions.md).
// User action files import it; when the engine bundles them, every import of
// this module is redirected to the running engine's own copy (ADR 0008).

export { z } from 'zod';
export { target } from '../actions/action-spec.js';
export { defineAction, type ActionDefinition, type RunnableAction } from './define-action.js';
export {
  ActionError,
  AssertionError,
  type ActionErrorOptions,
  type AssertionErrorOptions,
} from './errors.js';
export type {
  APIRequestContext,
  ActionContext,
  Environment,
  Locator,
  Logger,
  Page,
  Secrets,
  TargetRef,
  Variables,
} from './context.js';
