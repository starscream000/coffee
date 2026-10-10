// Names the recorder makes (docs/recording.md, "Names of targets"): target
// names `<page>.<element>` in lower camel case, page names for new tabs, the
// placeholder variable of a password, and the escaping of recorded text so
// that `${` is never read as interpolation.

/** The words of a text: letters and digits only, at most `max` of them. */
function words(text: string, max: number): string[] {
  return (
    (
      text
        .normalize('NFKD')
        .replace(/[̀-ͯ]/g, '')
        // A word joined by a hyphen or an apostrophe stays one word: "to-do" → "todo".
        .replace(/([A-Za-z0-9])['’-](?=[A-Za-z0-9])/g, '$1')
        .match(/[A-Za-z0-9]+/g) ?? []
    ).slice(0, max)
  );
}

/**
 * A text in lower camel case, letters and digits only, at most four words.
 *
 * @param text - Any text, such as an accessible name.
 * @returns Such as `newTodo` for "New to-do"; empty when the text has no
 *   letter or digit.
 * @example
 * ```ts
 * camelCase('Sign in'); // 'signIn'
 * ```
 */
export function camelCase(text: string): string {
  return words(text, 4)
    .map((word, index) => {
      const lower = word.toLowerCase();
      return index === 0 ? lower : lower.charAt(0).toUpperCase() + lower.slice(1);
    })
    .join('');
}

/**
 * The page part of a target name: the first segment of a URL's path.
 *
 * @param url - The URL of the tab's top page.
 * @returns Such as `todos` for `/todos/12`, or `home` for `/`.
 */
export function pageSlug(url: string): string {
  let path: string;
  try {
    path = new URL(url).pathname;
  } catch {
    path = '/';
  }
  const first = path.split('/').find((segment) => segment !== '') ?? '';
  const slug = camelCase(decodeURIComponent(first));
  return slug === '' || /^[0-9]/.test(slug) ? 'home' : slug;
}

/**
 * The element part of a target name, from the first of the given texts that
 * has a letter or digit.
 *
 * @param texts - Accessible name, placeholder, test ID, text, tag name, in that order.
 * @returns Such as `add`; `element` when none of them helps.
 */
export function elementSlug(texts: readonly (string | undefined)[]): string {
  for (const text of texts) {
    if (text === undefined) continue;
    const slug = camelCase(text);
    if (slug !== '') return /^[0-9]/.test(slug) ? `n${slug}` : slug;
  }
  return 'element';
}

/**
 * A name that is not taken yet: the base, or the base with 2, 3, … appended.
 *
 * @param base - The wanted name.
 * @param taken - Whether a name is taken.
 * @returns A free name.
 * @example
 * ```ts
 * uniqueName('todos.add', (name) => name === 'todos.add'); // 'todos.add2'
 * ```
 */
export function uniqueName(base: string, taken: (name: string) => boolean): string {
  if (!taken(base)) return base;
  for (let suffix = 2; ; suffix++) {
    const name = `${base}${String(suffix)}`;
    if (!taken(name)) return name;
  }
}

/**
 * The name of the placeholder variable for a password typed into a target.
 *
 * @param targetName - Such as `login.password`.
 * @returns Such as `loginPassword`, a valid variable name.
 */
export function placeholderVariable(targetName: string): string {
  const name = camelCase(targetName.replace(/\./g, ' '));
  return name === '' || /^[0-9]/.test(name) ? 'recordedPassword' : name;
}

/**
 * Escapes recorded text for a step file, where `${` starts interpolation:
 * `${` becomes `$${`, which the engine reads as a literal `${`.
 *
 * @param text - Text the person typed or the page shows.
 * @returns The text as a step file must hold it.
 */
export function literal(text: string): string {
  // A function, because "$$" in a replacement string means one "$".
  return text.replace(/\$\{/g, () => '$${');
}
