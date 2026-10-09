// Errors a user action throws to fail a step with a clear message
// (docs/actions.md, "Failing an action"). The engine recognises them by their
// `sdkError` tag, not by `instanceof`, so the tag still works across module
// and bundling boundaries.

/** Options shared by the SDK errors. */
export interface ActionErrorOptions {
  /** What the tester can do about it. */
  readonly hint?: string;
}

/** Options of {@link AssertionError}. */
export interface AssertionErrorOptions extends ActionErrorOptions {
  /** The value the step expected. */
  readonly expected?: unknown;
  /** The value the step found. */
  readonly actual?: unknown;
}

/**
 * Fails a step because the action could not do its job.
 *
 * @example
 * ```ts
 * throw new ActionError('The product is out of stock', { hint: 'Use a product with stock.' });
 * ```
 */
export class ActionError extends Error {
  /** Tag the engine uses to recognise SDK errors. */
  readonly sdkError: 'ActionError' | 'AssertionError' = 'ActionError';
  /** What the tester can do about it. */
  readonly hint: string | undefined;

  /**
   * @param message - What went wrong, for the tester.
   * @param options - An optional hint.
   */
  constructor(message: string, options: ActionErrorOptions = {}) {
    super(message);
    this.name = 'ActionError';
    this.hint = options.hint;
  }
}

/**
 * Fails a step because something it checked was not as expected.
 *
 * @example
 * ```ts
 * throw new AssertionError('Cart total is wrong', { expected: '€20.00', actual: total });
 * ```
 */
export class AssertionError extends ActionError {
  override readonly sdkError = 'AssertionError';
  /** The value the step expected. */
  readonly expected: unknown;
  /** The value the step found. */
  readonly actual: unknown;

  /**
   * @param message - What did not match, for the tester.
   * @param options - Expected and actual values, and an optional hint.
   */
  constructor(message: string, options: AssertionErrorOptions = {}) {
    super(message, options);
    this.name = 'AssertionError';
    this.expected = options.expected;
    this.actual = options.actual;
  }
}

/** The fields of an SDK error, as the engine reads them. */
export interface SdkErrorFields {
  /** Which SDK error it is. */
  readonly sdkError: 'ActionError' | 'AssertionError';
  /** The error's message. */
  readonly message: string;
  /** What the tester can do about it. */
  readonly hint?: unknown;
  /** Expected value of an assertion. */
  readonly expected?: unknown;
  /** Actual value of an assertion. */
  readonly actual?: unknown;
}

/**
 * Tells whether a thrown value is an SDK error, by its `sdkError` tag.
 *
 * @param value - Anything a user action threw.
 * @returns True for an `ActionError` or `AssertionError` from any copy of the SDK.
 */
export function isSdkError(value: unknown): value is SdkErrorFields {
  return (
    typeof value === 'object' &&
    value !== null &&
    'sdkError' in value &&
    (value.sdkError === 'ActionError' || value.sdkError === 'AssertionError') &&
    'message' in value &&
    typeof value.message === 'string'
  );
}
