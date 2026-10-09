// Which protocol fields secret masking may touch (ADR 0014; review 0004,
// finding 1). Masking works on the message's structure, field by field, never
// on keys or on the serialised text, so a masked message is still valid JSON,
// still matches its schema, and keeps every identifier. Kept next to the
// schemas: masking-rules.test.ts fails when a schema field is not in this table.

/**
 * What masking does with a field's value:
 * - `keep`: left exactly as it is (identifiers, fixed values, paths, numbers);
 * - `mask`: every string inside it is masked (free text and user data);
 * - `walk`: a container; each of its fields is looked up in this table.
 */
export type MaskRule = 'keep' | 'mask' | 'walk';

/**
 * The rule for each field, by name. An entry `parent.field` applies only
 * inside a field named `parent`, and wins over the plain `field` entry. A
 * field in no entry is masked, the safe default for anything new.
 *
 * Identifiers and paths come from files in the repository, which never hold
 * secret values, so keeping them cannot leak a secret.
 */
export const MASK_RULES: Readonly<Record<string, MaskRule>> = {
  // JSON-RPC envelope
  jsonrpc: 'keep',
  id: 'keep',
  method: 'keep',
  result: 'walk',
  error: 'walk',
  'error.data': 'walk',
  // Identifiers and fixed values
  runId: 'keep',
  testId: 'keep',
  stepId: 'keep',
  parentStepId: 'keep',
  seq: 'keep',
  section: 'keep',
  action: 'keep',
  page: 'keep',
  status: 'keep',
  level: 'keep',
  severity: 'keep',
  code: 'keep',
  state: 'keep',
  kind: 'keep',
  name: 'keep',
  version: 'keep',
  protocolVersion: 'keep',
  clientProtocolVersion: 'keep',
  engineProtocolVersion: 'keep',
  engineVersion: 'keep',
  env: 'keep',
  environments: 'keep',
  defaultEnvironment: 'keep',
  logins: 'keep',
  browser: 'keep',
  browsers: 'keep',
  tags: 'keep',
  skip: 'keep',
  reason: 'keep',
  target: 'keep',
  param: 'keep',
  variable: 'keep',
  shorthand: 'keep',
  locale: 'keep',
  timezone: 'keep',
  startedAt: 'keep',
  // Files and folders
  file: 'keep',
  files: 'keep',
  path: 'keep',
  root: 'keep',
  configFile: 'keep',
  resultsDir: 'keep',
  screenshot: 'keep',
  // Numbers and booleans
  line: 'keep',
  column: 'keep',
  endLine: 'keep',
  endColumn: 'keep',
  width: 'keep',
  height: 'keep',
  durationMs: 'keep',
  rows: 'keep',
  row: 'keep',
  matches: 'keep',
  candidateIndex: 'keep',
  passed: 'keep',
  failed: 'keep',
  cancelled: 'keep',
  skipped: 'keep',
  automatic: 'keep',
  headed: 'keep',
  refreshLogins: 'keep',
  // Containers
  params: 'mask',
  // A message's own params (an event, or a request's parameters) are walked;
  // `params` deeper down are a step's parameters, which are user data.
  'envelope.params': 'walk',
  diagnostics: 'walk',
  actions: 'walk',
  tests: 'walk',
  locators: 'walk',
  candidates: 'walk',
  capabilities: 'walk',
  client: 'walk',
  engine: 'walk',
  options: 'walk',
  content: 'walk',
  settings: 'walk',
  viewport: 'walk',
  totals: 'walk',
  snapshot: 'walk',
  location: 'walk',
  frame: 'walk',
  within: 'walk',
  source: 'walk',
  // Free text and user data
  message: 'mask',
  hint: 'mask',
  expected: 'mask',
  actual: 'mask',
  candidate: 'mask',
  data: 'mask',
  title: 'mask',
  description: 'mask',
  paramsSchema: 'mask',
  text: 'mask',
  'snapshot.reason': 'mask',
};

/**
 * The rule for a field.
 *
 * @param field - The field's name.
 * @param parent - The name of the field that contains it, if any.
 * @returns The `parent.field` rule, else the `field` rule, else `mask`.
 *
 * @example
 * ```ts
 * maskRuleFor('data', 'error'); // "walk"
 * maskRuleFor('data', 'params'); // "mask"
 * ```
 */
export function maskRuleFor(field: string, parent?: string): MaskRule {
  return (
    (parent === undefined ? undefined : MASK_RULES[`${parent}.${field}`]) ??
    MASK_RULES[field] ??
    'mask'
  );
}

/**
 * Applies the masking rules to a protocol message: masks the strings of
 * `mask` fields with `maskText`, leaves `keep` fields and every key untouched,
 * and walks `walk` containers.
 *
 * @param message - A JSON-RPC message (request, response or notification).
 * @param maskText - Masks secrets in one string.
 * @returns A masked copy; the input is not changed.
 *
 * @example
 * ```ts
 * maskMessage(message, (text) => registry.mask(text));
 * ```
 */
export function maskMessage(message: unknown, maskText: (text: string) => string): unknown {
  const maskAll = (value: unknown): unknown => {
    if (typeof value === 'string') return maskText(value);
    if (Array.isArray(value)) return value.map(maskAll);
    if (typeof value === 'object' && value !== null) {
      return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, maskAll(item)]));
    }
    return value;
  };
  const apply = (value: unknown, rule: MaskRule, name: string): unknown => {
    if (rule === 'keep') return value;
    if (rule === 'mask') return maskAll(value);
    if (Array.isArray(value)) return value.map((item) => apply(item, rule, name));
    if (typeof value === 'object' && value !== null) {
      return Object.fromEntries(
        Object.entries(value).map(([key, item]) => [key, apply(item, maskRuleFor(key, name), key)]),
      );
    }
    return maskAll(value);
  };
  if (typeof message !== 'object' || message === null || Array.isArray(message)) {
    return maskAll(message);
  }
  // The message itself is the envelope: its fields use `envelope.field` rules.
  return Object.fromEntries(
    Object.entries(message).map(([key, item]) => [
      key,
      apply(item, maskRuleFor(key, 'envelope'), key),
    ]),
  );
}
