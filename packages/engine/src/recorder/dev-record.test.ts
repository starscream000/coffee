// Unit tests for the command line of `pnpm record`: the options it takes, and
// errors that name what is wrong (review 0009, finding 3).
import { describe, expect, it } from 'vitest';
import { parseArguments } from './dev-record.js';

describe('parseArguments', () => {
  it('reads the options, also after pnpm`s "--"', () => {
    expect(
      parseArguments([
        '--',
        '--project',
        'examples/demo-app',
        '--url',
        '/todos',
        '--file',
        'tests/a.test.yaml',
        '--login',
        'customer',
      ]),
    ).toEqual({
      project: 'examples/demo-app',
      url: '/todos',
      file: 'tests/a.test.yaml',
      env: undefined,
      login: 'customer',
    });
  });

  it('names an argument it does not know', () => {
    expect(() =>
      parseArguments(['--project', 'p', '--url', '/login', 'secret-typed-here', '--file', 'f']),
    ).toThrow(
      /^Unknown argument "secret-typed-here"\. Each option is a flag followed by one value/,
    );
  });

  it('names an option without a value, and the missing options', () => {
    expect(() => parseArguments(['--project'])).toThrow(/^The option --project needs a value\./);
    expect(() => parseArguments(['--project', 'p'])).toThrow(/^Missing --url, --file\./);
  });
});
