// Unit tests for the names the recorder makes: target names, page names, the
// placeholder variable of a password, and the escaping of `${`.
import { describe, expect, it } from 'vitest';
import {
  camelCase,
  elementSlug,
  literal,
  pageSlug,
  placeholderVariable,
  uniqueName,
} from './names.js';

describe('names', () => {
  it('writes text in lower camel case, at most four words, a hyphenated word as one', () => {
    expect(camelCase('New to-do')).toBe('newTodo');
    expect(camelCase('Sign in')).toBe('signIn');
    expect(camelCase('Subscribe to the newsletter today')).toBe('subscribeToTheNewsletter');
    expect(camelCase("Café's menu")).toBe('cafesMenu');
    expect(camelCase('…')).toBe('');
  });

  it('takes the first path segment of a page, or home', () => {
    expect(pageSlug('http://localhost:4310/todos')).toBe('todos');
    expect(pageSlug('http://localhost:4310/frames/payment')).toBe('frames');
    expect(pageSlug('http://localhost:4310/')).toBe('home');
    expect(pageSlug('http://localhost:4310/123')).toBe('home');
    expect(pageSlug('not a url')).toBe('home');
  });

  it('names an element after the first text that has a letter or digit', () => {
    expect(elementSlug([undefined, '', 'Card number', 'card'])).toBe('cardNumber');
    expect(elementSlug(['42'])).toBe('n42');
    expect(elementSlug([])).toBe('element');
  });

  it('numbers a name that is taken', () => {
    const taken = new Set(['todos.add', 'todos.add2']);
    expect(uniqueName('todos.add', (name) => taken.has(name))).toBe('todos.add3');
    expect(uniqueName('todos.clear', (name) => taken.has(name))).toBe('todos.clear');
  });

  it('makes a variable name for a password placeholder', () => {
    expect(placeholderVariable('login.password')).toBe('loginPassword');
    expect(placeholderVariable('...')).toBe('recordedPassword');
  });

  it('escapes ${ so that recorded text is never interpolated', () => {
    expect(literal('costs ${price} and ${tax}')).toBe('costs $${price} and $${tax}');
    expect(literal('plain $ text')).toBe('plain $ text');
  });
});
