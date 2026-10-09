// Size limits of protocol messages (docs/protocol.md, "Transport"; ADR 0005).

/**
 * Largest protocol message, in bytes of UTF-8, not counting the newline that
 * ends the line. Applies in both directions.
 */
export const MAX_MESSAGE_BYTES = 4 * 1024 * 1024;

/**
 * Longest a single string field (`message`, `expected`, `actual`, log text) may
 * be, in bytes of UTF-8, before the engine truncates it when a message would
 * otherwise exceed {@link MAX_MESSAGE_BYTES}.
 */
export const TRUNCATED_FIELD_BYTES = 64 * 1024;

/**
 * Builds the marker the engine appends to a truncated string field.
 *
 * @param removedCharacters - How many characters were cut off.
 * @returns The marker, for example `"… [truncated 1200 characters]"`.
 *
 * @example
 * ```ts
 * const shown = text.slice(0, keep) + truncationMarker(text.length - keep);
 * ```
 */
export function truncationMarker(removedCharacters: number): string {
  return `… [truncated ${String(removedCharacters)} characters]`;
}
