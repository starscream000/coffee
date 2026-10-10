// Unit tests for the parts of the candidate check that need no browser:
// reading role and name from an ARIA snapshot, proposing candidates in order,
// and choosing the ones to write.
import { describe, expect, it } from 'vitest';
import {
  chooseCandidates,
  parseAriaLine,
  proposeCandidates,
  type ElementFacts,
} from './candidates.js';

const button: ElementFacts = { tag: 'button', field: false, text: 'Add', css: '#add' };

describe('parseAriaLine', () => {
  it('reads role and name from the first line', () => {
    expect(parseAriaLine('- button "Add"')).toEqual({ role: 'button', name: 'Add' });
    expect(parseAriaLine('- textbox "Card number": "4242"')).toEqual({
      role: 'textbox',
      name: 'Card number',
    });
    expect(parseAriaLine('- checkbox "Done" [checked]')).toEqual({
      role: 'checkbox',
      name: 'Done',
    });
    expect(parseAriaLine('- link "Open receipt":\n  - /url: /receipt')).toEqual({
      role: 'link',
      name: 'Open receipt',
    });
    expect(parseAriaLine('- button "Say \\"hi\\""')).toEqual({ role: 'button', name: 'Say "hi"' });
  });

  it('gives an empty name, or nothing, when the snapshot has none', () => {
    expect(parseAriaLine('- generic')).toEqual({ role: 'generic', name: '' });
    expect(parseAriaLine('')).toBeUndefined();
    expect(parseAriaLine(': [')).toBeUndefined();
  });
});

describe('proposeCandidates', () => {
  it('proposes role, label, placeholder, test ID, text and CSS in that order', () => {
    const field: ElementFacts = {
      tag: 'input',
      field: true,
      placeholder: 'Email',
      testId: 'email',
      css: '#email',
    };
    expect(proposeCandidates(field, { role: 'textbox', name: 'Email address' })).toEqual([
      { role: 'textbox', name: 'Email address' },
      { label: 'Email address' },
      { placeholder: 'Email' },
      { testId: 'email' },
      { css: '#email' },
    ]);
    expect(proposeCandidates(button, { role: 'button', name: 'Add' })).toEqual([
      { role: 'button', name: 'Add' },
      { text: 'Add' },
      { css: '#add' },
    ]);
  });

  it('leaves out a role without a name and a text that is too long', () => {
    const long: ElementFacts = { tag: 'div', field: false, text: 'x'.repeat(81), css: 'div' };
    expect(proposeCandidates(long, { role: 'generic', name: '' })).toEqual([{ css: 'div' }]);
  });
});

describe('chooseCandidates', () => {
  it('keeps the first two that are not CSS, then CSS', () => {
    expect(
      chooseCandidates([
        { role: 'button', name: 'Add' },
        { testId: 'add' },
        { text: 'Add' },
        { css: '#add' },
      ]),
    ).toEqual({
      candidates: [{ role: 'button', name: 'Add' }, { testId: 'add' }, { css: '#add' }],
      cssOnly: false,
    });
  });

  it('says when only CSS identifies the element', () => {
    expect(chooseCandidates([{ css: 'tr > button' }])).toEqual({
      candidates: [{ css: 'tr > button' }],
      cssOnly: true,
    });
    expect(chooseCandidates([])).toEqual({ candidates: [], cssOnly: false });
  });
});
