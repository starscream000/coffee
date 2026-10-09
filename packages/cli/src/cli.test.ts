// Unit tests for command-line argument handling.
import { PRODUCT } from '@test-tool/protocol';
import { describe, expect, it } from 'vitest';
import { EXIT_OK, EXIT_USAGE, runCli } from './cli.js';

function capture(): { text: () => string; write: (s: string) => void } {
  let buffer = '';
  return {
    text: () => buffer,
    write: (s: string) => {
      buffer += s;
    },
  };
}

describe('runCli', () => {
  it('prints usage when called without arguments', () => {
    const out = capture();
    expect(runCli([], out, capture())).toBe(EXIT_OK);
    expect(out.text()).toContain(`Usage: ${PRODUCT.command}`);
  });

  it('prints the CLI and protocol versions', () => {
    const out = capture();
    expect(runCli(['--version'], out, capture())).toBe(EXIT_OK);
    expect(out.text()).toMatch(/^\S+ \d+\.\d+\.\d+ \(protocol \d+\.\d+\.\d+\)\n$/);
    expect(out.text().startsWith(`${PRODUCT.command} `)).toBe(true);
  });

  it('rejects unknown arguments with an actionable message', () => {
    const err = capture();
    expect(runCli(['--bogus'], capture(), err)).toBe(EXIT_USAGE);
    expect(err.text()).toContain('Unknown argument "--bogus"');
    expect(err.text()).toContain(`${PRODUCT.command} --help`);
  });
});
