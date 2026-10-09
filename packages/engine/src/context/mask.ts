// The secret registry and `mask` (ADR 0014): every registered secret is
// remembered in the forms it can take in output (as is, JSON-escaped,
// URL-encoded, form-encoded and HTML-escaped), and `mask` replaces each form
// with "•••", longest first, so a shorter secret inside a longer one cannot
// leave part of the longer one visible.

/** What a masked secret is replaced with. */
export const MASK = '•••';

/** Shortest value that can be a secret; shorter ones would damage ordinary output. */
export const MIN_SECRET_LENGTH = 4;

const HTML_ESCAPES: Readonly<Record<string, string>> = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
};

/**
 * The forms a value can take in the engine's output.
 *
 * @param value - A secret value.
 * @returns The value as is, JSON-escaped (inside quotes), URL-encoded,
 *   form-encoded and HTML-escaped, without duplicates.
 *
 * @example
 * ```ts
 * encodedForms('p@ss "word"');
 * // ['p@ss "word"', 'p@ss \\"word\\"', 'p%40ss%20%22word%22', 'p%40ss+%22word%22', 'p@ss &quot;word&quot;']
 * ```
 */
export function encodedForms(value: string): string[] {
  const forms = [
    value,
    JSON.stringify(value).slice(1, -1),
    encodeURIComponent(value),
    new URLSearchParams({ v: value }).toString().slice(2),
    value.replace(/[&<>"']/g, (char) => HTML_ESCAPES[char] ?? char),
  ];
  return [...new Set(forms)].filter((form) => form.length > 0);
}

/**
 * Remembers secret values and masks them in text. Values can be added at any
 * time, for example a sensitive header value seen while a test runs.
 *
 * @example
 * ```ts
 * const secrets = new SecretRegistry();
 * secrets.register('hunter2-token');
 * secrets.mask('Bearer hunter2-token'); // "Bearer •••"
 * ```
 */
export class SecretRegistry {
  private forms: string[] = [];
  private readonly values = new Set<string>();

  /**
   * Registers a value to be masked from now on.
   *
   * @param value - The secret value.
   * @returns False when the value is shorter than {@link MIN_SECRET_LENGTH}
   *   characters and was therefore not registered.
   */
  register(value: string): boolean {
    if (value.length < MIN_SECRET_LENGTH) {
      return false;
    }
    if (!this.values.has(value)) {
      this.values.add(value);
      const all = new Set([...this.forms, ...encodedForms(value)]);
      // Longest first: a longer secret is replaced before a shorter one inside it.
      this.forms = [...all].sort((a, b) => b.length - a.length);
    }
    return true;
  }

  /** Forgets every registered value, for example when another project is opened. */
  clear(): void {
    this.forms = [];
    this.values.clear();
  }

  /** How many values are registered. */
  get size(): number {
    return this.values.size;
  }

  /**
   * Replaces every registered secret, in every form, with {@link MASK}.
   *
   * @param text - Any text the engine is about to write out.
   * @returns The text with secrets masked.
   */
  mask(text: string): string {
    let result = text;
    for (const form of this.forms) {
      if (result.includes(form)) {
        result = result.split(form).join(MASK);
      }
    }
    return result;
  }

  /**
   * Masks every string inside a JSON-like value (keys too), before it is
   * serialised or truncated, so truncation can never cut a secret in half.
   *
   * @param value - Any JSON-like value.
   * @returns A copy with every string masked.
   */
  maskValue(value: unknown): unknown {
    if (this.forms.length === 0) {
      return value;
    }
    if (typeof value === 'string') {
      return this.mask(value);
    }
    if (Array.isArray(value)) {
      return value.map((item) => this.maskValue(item));
    }
    if (typeof value === 'object' && value !== null) {
      return Object.fromEntries(
        Object.entries(value).map(([key, item]) => [this.mask(key), this.maskValue(item)]),
      );
    }
    return value;
  }
}
