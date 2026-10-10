// Unit tests for the text of a recorded test file: shorthand where an action
// has one, parameters on one line, page and opens, targets in both forms, and
// review comments.
import { describe, expect, it } from 'vitest';
import { renderRecording, stepValue } from './document.js';

describe('stepValue', () => {
  it('uses the shorthand when only that parameter is given', () => {
    expect(stepValue({ action: 'click', params: { target: 'todos.add' } })).toEqual({
      click: 'todos.add',
    });
    expect(stepValue({ action: 'check', params: { target: 'x', checked: false } })).toEqual({
      check: { target: 'x', checked: false },
    });
    expect(
      stepValue({ action: 'click', params: { target: 'a' }, page: 'receipt', opens: 'help' }),
    ).toEqual({ click: 'a', page: 'receipt', opens: 'help' });
  });
});

describe('renderRecording', () => {
  it('writes the file as a person would', () => {
    const text = renderRecording({
      name: 'Sign in',
      login: 'customer',
      vars: new Map([['loginPassword', '']]),
      targets: new Map([
        [
          'login.username',
          { candidates: [{ role: 'textbox', name: 'Username' }, { css: '#username' }] },
        ],
        ['frames.pay', { frame: 'frames.checkoutFrame', candidates: [{ css: 'body > button' }] }],
      ]),
      steps: [
        { action: 'goto', params: { url: '/login' } },
        { action: 'fill', params: { target: 'login.username', value: 'it costs $${price}' } },
        {
          action: 'fill',
          params: { target: 'login.password', value: '${vars.loginPassword}' },
          review: 'typed into a password field',
        },
        { action: 'click', params: { target: 'frames.pay' }, opens: 'receipt' },
      ],
    });
    expect(text).toBe(
      [
        'version: 1',
        'name: Sign in',
        'login: customer',
        'vars:',
        "  loginPassword: ''",
        'targets:',
        '  login.username:',
        '    - role: textbox',
        '      name: Username',
        "    - css: '#username'",
        '  frames.pay:',
        '    frame: frames.checkoutFrame',
        '    candidates:',
        '      - css: body > button',
        'steps:',
        '  - goto: /login',
        "  - fill: { target: login.username, value: 'it costs $${price}' }",
        '  # review: typed into a password field',
        "  - fill: { target: login.password, value: '${vars.loginPassword}' }",
        '  - click: frames.pay',
        '    opens: receipt',
        '',
      ].join('\n'),
    );
  });
});
