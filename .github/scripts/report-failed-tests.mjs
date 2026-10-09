// Turns the failed tests of `dotnet test --logger trx` result files into error
// annotations, so each failing test and its message show on the pull request.
// Usage: node .github/scripts/report-failed-tests.mjs <results folder>
// Uses only Node's own modules; the result files are read with regular
// expressions, which is enough for the few fields needed here.
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

/** Most annotations GitHub keeps for one step. */
const LIMIT = 9;

/** Finds every result file under a folder. */
function resultFiles(folder) {
  try {
    return readdirSync(folder, { recursive: true })
      .filter((name) => String(name).endsWith('.trx'))
      .map((name) => join(folder, String(name)));
  } catch {
    return [];
  }
}

/** Reverses XML escaping. */
function unescapeXml(text) {
  return text
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&#x([0-9a-fA-F]+);/g, (_, hex) => String.fromCodePoint(parseInt(hex, 16)))
    .replace(/&#(\d+);/g, (_, dec) => String.fromCodePoint(Number(dec)))
    .replace(/&amp;/g, '&');
}

/** Escapes text for a workflow command (`::error title=…::…`). */
function escapeCommand(text, isProperty) {
  let result = text.replace(/%/g, '%25').replace(/\r/g, '%0D').replace(/\n/g, '%0A');
  if (isProperty) result = result.replace(/:/g, '%3A').replace(/,/g, '%2C');
  return result;
}

function report(title, message) {
  console.log(`::error title=${escapeCommand(title, true)}::${escapeCommand(message, false)}`);
}

const folder = process.argv[2] ?? '.';
const files = resultFiles(folder);
if (files.length === 0) {
  report(
    'No test results',
    `No result file was found under ${folder}; a test process may have crashed.`,
  );
}
const failures = [];
for (const file of files) {
  const text = readFileSync(file, 'utf8');
  for (const match of text.matchAll(
    /<UnitTestResult\b([^>]*?)(?:\/>|>([\s\S]*?)<\/UnitTestResult>)/g,
  )) {
    const [, attributes = '', body = ''] = match;
    if (!/\boutcome="Failed"/.test(attributes)) continue;
    const name = unescapeXml(/\btestName="([^"]*)"/.exec(attributes)?.[1] ?? '(unnamed test)');
    const message = unescapeXml(/<Message>([\s\S]*?)<\/Message>/.exec(body)?.[1] ?? '');
    const stack = unescapeXml(/<StackTrace>([\s\S]*?)<\/StackTrace>/.exec(body)?.[1] ?? '');
    failures.push({ name, text: `${message}\n${stack}`.trim().slice(0, 3000) });
  }
}
for (const failure of failures.slice(0, LIMIT)) report(failure.name, failure.text);
if (failures.length > LIMIT) {
  report(
    'More failures',
    `${String(failures.length)} tests failed; the first ${String(LIMIT)} are shown: ${failures
      .slice(LIMIT)
      .map((failure) => failure.name)
      .join(', ')
      .slice(0, 3000)}`,
  );
}
console.log(`${String(failures.length)} failed test(s) in ${String(files.length)} result file(s).`);
