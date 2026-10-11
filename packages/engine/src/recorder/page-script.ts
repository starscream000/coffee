// The script the recorder adds to every frame of the recording browser
// (ADR 0022). It listens in the capture phase, before the page's own
// listeners, and reports each interaction to the engine through a binding:
//
// - focus: a text field got the focus; the engine checks its candidates now;
// - fill: a text field's text is final (it lost the focus, or Enter, Tab or
//   Escape was pressed, or another interaction is reported);
// - press: Enter, Tab or Escape in a text field;
// - click: held until the engine has checked the element, then replayed with
//   element.click(), so an element a click removes is still there to check;
// - fieldClick: a click into a text field, which is no step if it is typed into;
// - select, check: from the change event of a select, checkbox or radio;
// - notice: something the recorder sees but does not record.
//
// Each reported element gets a marker attribute with an id the engine finds
// it by. The script is plain JavaScript in a string: it runs in the page, not
// in Node, so it has no access to the engine's types.

/** What the page script needs to know. */
export interface PageScriptOptions {
  /** The name of the binding it reports to. */
  readonly binding: string;
  /** The marker attribute it gives reported elements. */
  readonly attribute: string;
  /** The project's test ID attribute, such as `data-testid`. */
  readonly testIdAttribute: string;
}

/**
 * The page script's source, for `BrowserContext.addInitScript`.
 *
 * @param options - The binding, marker attribute and test ID attribute.
 * @returns JavaScript source that installs the listeners once per document.
 */
export function pageScript(options: PageScriptOptions): string {
  return `(() => {
  const BINDING = ${JSON.stringify(options.binding)};
  const ATTR = ${JSON.stringify(options.attribute)};
  const TEST_ID = ${JSON.stringify(options.testIdAttribute)};
  const INSTALLED = Symbol.for(BINDING);
  if (window[INSTALLED]) return;
  window[INSTALLED] = true;

  const TEXT_TYPES = new Set(['', 'text', 'email', 'password', 'search', 'tel', 'url', 'number', 'date', 'time', 'datetime-local', 'month', 'week']);
  const ACTIVE = 'a[href], button, summary, [role=button], [role=link], [role=menuitem], [role=menuitemcheckbox], [role=menuitemradio], [role=tab], [role=option], [role=treeitem], [role=switch], input[type=submit], input[type=button], input[type=reset], input[type=image]';
  const CONTAINERS = 'li, tr, [role=listitem], [role=row], form, dialog, [role=dialog], [role=alertdialog], section, [role=region], fieldset, article';
  const MODIFIERS = new Set(['Shift', 'Control', 'Alt', 'Meta', 'AltGraph', 'CapsLock', 'NumLock', 'ScrollLock', 'Fn', 'OS']);
  let counter = 0;
  let enterAt = -Infinity;
  const committed = new WeakMap();
  const noticed = new WeakSet();

  const report = (event) => {
    const send = window[BINDING];
    return typeof send === 'function' ? send(event) : Promise.resolve();
  };
  const markOf = (element) => {
    let mark = element.getAttribute(ATTR);
    if (!mark) {
      counter += 1;
      mark = Math.random().toString(36).slice(2, 8) + String(counter);
      element.setAttribute(ATTR, mark);
    }
    return mark;
  };
  const norm = (text) => (text || '').replace(/\\s+/g, ' ').trim();
  const escapeId = (id) => (window.CSS && CSS.escape ? CSS.escape(id) : id);
  const cssPath = (element) => {
    const parts = [];
    let node = element;
    while (node && node.nodeType === 1 && node !== document.documentElement) {
      if (node.id && document.querySelectorAll('#' + escapeId(node.id)).length === 1) {
        parts.unshift('#' + escapeId(node.id));
        return parts.join(' > ');
      }
      let part = node.localName;
      const parent = node.parentElement;
      if (parent) {
        const same = Array.from(parent.children).filter((child) => child.localName === node.localName);
        if (same.length > 1) part += ':nth-of-type(' + String(same.indexOf(node) + 1) + ')';
      }
      parts.unshift(part);
      node = parent;
    }
    return parts.join(' > ');
  };
  const isTextField = (element) =>
    element instanceof HTMLTextAreaElement ||
    (element instanceof HTMLInputElement && TEXT_TYPES.has((element.getAttribute('type') || '').toLowerCase()));
  const isToggle = (element) =>
    element instanceof HTMLInputElement && (element.type === 'checkbox' || element.type === 'radio');
  const isSelect = (element) => element instanceof HTMLSelectElement;
  const isFile = (element) => element instanceof HTMLInputElement && element.type === 'file';
  const isField = (element) => isTextField(element) || isToggle(element) || isSelect(element) || isFile(element);
  const describe = (element) => norm(element.getAttribute('aria-label') || element.innerText || element.value || element.localName).slice(0, 60);
  // The first piece of text in a container that is not inside the element, such as a row's first cell.
  const labelOf = (container, element) => {
    const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      if (element.contains(node)) continue;
      const parent = node.parentElement;
      if (parent && (parent.closest('script, style') || !parent.checkVisibility())) continue;
      const text = norm(node.textContent);
      if (text) return text.slice(0, 80);
    }
    return undefined;
  };
  const containerOf = (element) => {
    const container = element.parentElement && element.parentElement.closest(CONTAINERS);
    if (!container) return undefined;
    return {
      mark: markOf(container),
      tag: container.localName,
      label: labelOf(container, element),
      text: norm(container.innerText).slice(0, 200) || undefined,
      testId: container.getAttribute(TEST_ID) || undefined,
    };
  };
  const factsOf = (element) => {
    const attr = (name) => element.getAttribute(name) || undefined;
    return {
      container: containerOf(element),
      tag: element.localName,
      type: attr('type'),
      id: element.id || undefined,
      placeholder: attr('placeholder'),
      testId: attr(TEST_ID),
      text: isField(element) ? undefined : norm(element.innerText).slice(0, 200) || undefined,
      field: isField(element),
      css: cssPath(element),
    };
  };
  const notice = (kind, message, element) =>
    report({ kind: 'notice', notice: kind, message, ...(element ? { mark: markOf(element), facts: factsOf(element) } : {}) });

  // A text field's text is final: report it once, unless it is unchanged.
  const flush = (field) => {
    if (!field || !isTextField(field)) return;
    const value = field.value;
    if (committed.get(field) === value) return;
    committed.set(field, value);
    report({ kind: 'fill', mark: markOf(field), facts: factsOf(field), value, password: field.type === 'password' });
  };
  const flushActive = () => {
    const active = document.activeElement;
    if (active && committed.has(active)) flush(active);
  };

  document.addEventListener('focusin', (event) => {
    const element = event.target;
    if (!(element instanceof Element) || !isTextField(element)) return;
    if (!committed.has(element)) committed.set(element, element.value);
    report({ kind: 'focus', mark: markOf(element), facts: factsOf(element) });
  }, true);

  document.addEventListener('click', (event) => {
    if (!event.isTrusted || !(event.target instanceof Element)) return;
    const origin = event.target;
    let element = origin;
    const label = origin.closest('label');
    if (label && label.control) element = label.control;
    // A select, checkbox, radio or file field is recorded from its change event.
    if (isToggle(element) || isSelect(element) || isFile(element)) return;
    if (isTextField(element)) {
      report({ kind: 'fieldClick', mark: markOf(element), facts: factsOf(element) });
      return;
    }
    // The click a browser makes on a form's submit button when Enter is pressed in a field.
    if (event.detail === 0 && performance.now() - enterAt < 1000) return;
    element = origin.closest(ACTIVE) || origin;
    if (element.isContentEditable) return;
    if (element === document.documentElement || element === document.body) {
      notice('background', 'A click on the page background (on no element) is not recorded.');
      return;
    }
    event.preventDefault();
    event.stopImmediatePropagation();
    flushActive();
    report({ kind: 'click', mark: markOf(element), facts: factsOf(element) }).finally(() => {
      origin.click();
    });
  }, true);

  document.addEventListener('change', (event) => {
    const element = event.target;
    if (!(element instanceof Element)) return;
    if (isTextField(element)) {
      flush(element);
    } else if (isSelect(element)) {
      flushActive();
      const options = Array.from(element.options);
      report({
        kind: 'select',
        mark: markOf(element),
        facts: factsOf(element),
        selected: options.filter((option) => option.selected).map((option) => ({ value: option.value, label: norm(option.label) })),
        labels: options.map((option) => norm(option.label)),
      });
    } else if (isToggle(element)) {
      flushActive();
      report({ kind: 'check', mark: markOf(element), facts: factsOf(element), checked: element.checked });
    } else if (isFile(element)) {
      notice('fileChooser', 'A file chosen in the file field "' + describe(element) + '" is not recorded yet.', element);
    }
  }, true);

  document.addEventListener('keydown', (event) => {
    if (!event.isTrusted || !(event.target instanceof Element)) return;
    const element = event.target;
    const key = event.key;
    if (MODIFIERS.has(key)) return;
    if (isTextField(element)) {
      if (key === 'Enter' || key === 'Tab' || key === 'Escape') {
        flush(element);
        if (key === 'Enter') enterAt = performance.now();
        report({ kind: 'press', mark: markOf(element), facts: factsOf(element), key });
      }
      // Other keys and shortcuts in a text field only change its text, which the fill holds.
      return;
    }
    if (element.isContentEditable || isToggle(element) || isSelect(element)) return;
    const chord = [event.ctrlKey && 'Control', event.altKey && 'Alt', event.metaKey && 'Meta', event.shiftKey && 'Shift'].filter(Boolean);
    if (chord.length > 0 && !(chord.length === 1 && event.shiftKey)) {
      notice('shortcut', 'The keyboard shortcut ' + [...chord, key].join('+') + ' is not recorded yet.');
      return;
    }
    // Enter and Space on a button or link make a click, which is recorded.
    if ((key === 'Enter' || key === ' ') && element.closest(ACTIVE)) return;
    notice('key', 'The key ' + (key === ' ' ? 'Space' : key) + ' pressed outside a text field is not recorded yet.');
  }, true);

  document.addEventListener('input', (event) => {
    const element = event.target;
    if (element instanceof Element && element.isContentEditable && !isTextField(element)) {
      const root = element.closest('[contenteditable]') || element;
      if (noticed.has(root)) return;
      noticed.add(root);
      notice('contentEditable', 'Typing into the editable element "' + describe(root) + '" is not recorded yet.', root);
    }
  }, true);

  document.addEventListener('dragstart', (event) => {
    if (event.isTrusted && event.target instanceof Element) {
      notice('drag', 'A drag of "' + describe(event.target) + '" is not recorded yet.', event.target);
    }
  }, true);
  document.addEventListener('contextmenu', (event) => {
    if (event.isTrusted && event.target instanceof Element) {
      notice('contextMenu', 'A right-click on "' + describe(event.target) + '" is not recorded yet.', event.target);
    }
  }, true);
  document.addEventListener('dblclick', (event) => {
    if (event.isTrusted && event.target instanceof Element) {
      notice('doubleClick', 'A double-click on "' + describe(event.target) + '" was recorded as two clicks; double-clicks are not recorded yet.');
    }
  }, true);

  if (window === window.top) {
    window.addEventListener('popstate', () => {
      notice('history', 'Going back or forward in the browser is not recorded yet.');
    });
    const entry = performance.getEntriesByType('navigation')[0];
    if (entry && entry.type === 'back_forward') {
      notice('history', 'Going back or forward in the browser is not recorded yet.');
    }
  }
})();`;
}
