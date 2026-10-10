// URL patterns at run time (owner decision of 2026-10-10; docs/actions.md,
// "Built-in actions"): a pattern that starts with `regex:` is a regular
// expression tested against the whole URL; anything else is a glob, where `*`
// matches any characters but `/`, `**` any characters, and `{a,b}` either
// alternative. A glob that starts with `/` is relative to the environment's
// base URL, so `/orders/*` means the orders of the site under test.

import { REGEX_PREFIX } from '../builtin-specs.js';

function escapeRegExp(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/** The regular expression of a glob, matching a whole URL. */
function globToRegExp(glob: string): RegExp {
  let source = '';
  for (let index = 0; index < glob.length; index++) {
    const char = glob[index] ?? '';
    if (char === '*') {
      if (glob[index + 1] === '*') {
        source += '.*';
        index += 1;
      } else {
        source += '[^/]*';
      }
    } else if (char === '{') {
      const end = glob.indexOf('}', index);
      if (end === -1) {
        source += escapeRegExp(char);
      } else {
        source += `(?:${glob
          .slice(index + 1, end)
          .split(',')
          .map(escapeRegExp)
          .join('|')})`;
        index = end;
      }
    } else {
      source += escapeRegExp(char);
    }
  }
  return new RegExp(`^${source}$`);
}

/**
 * Builds the test of a URL pattern.
 *
 * @param pattern - `regex:<expression>`, or a glob.
 * @param baseUrl - The environment's base URL, for globs that start with `/`.
 * @returns A function that tells whether a URL matches.
 *
 * @example
 * ```ts
 * urlMatcher('/orders/*', 'http://localhost:4310')('http://localhost:4310/orders/12'); // true
 * urlMatcher('regex:/orders/\\d+$', '')('http://shop/orders/12'); // true
 * ```
 */
export function urlMatcher(pattern: string, baseUrl: string): (url: string) => boolean {
  if (pattern.startsWith(REGEX_PREFIX)) {
    const regex = new RegExp(pattern.slice(REGEX_PREFIX.length));
    return (url) => regex.test(url);
  }
  let glob = pattern;
  if (glob.startsWith('/') && baseUrl !== '') {
    glob = `${baseUrl.replace(/\/+$/, '')}${glob}`;
  }
  const regex = globToRegExp(glob);
  return (url) => regex.test(url);
}
