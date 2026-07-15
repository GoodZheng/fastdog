const test = require('node:test');
const assert = require('node:assert');
const { parseRgLine } = require('../lib/jsonParser');

test('parseRgLine: 空行/null 返回 null', () => {
  assert.strictEqual(parseRgLine(''), null);
  assert.strictEqual(parseRgLine('   '), null);
  assert.strictEqual(parseRgLine(null), null);
});

test('parseRgLine: begin 事件', () => {
  const line = JSON.stringify({ type: 'begin', data: { path: { text: '/a/b.cs' } } });
  const ev = parseRgLine(line);
  assert.strictEqual(ev.type, 'fileBegin');
  assert.strictEqual(ev.filePath, '/a/b.cs');
});

test('parseRgLine: match 事件（文本行）', () => {
  const line = JSON.stringify({
    type: 'match',
    data: {
      path: { text: '/a/b.cs' },
      line_number: 12,
      lines: { text: 'const foo = 1;\n' },
      submatches: [{ match: { text: 'foo' }, start: 6, end: 9 }]
    }
  });
  const ev = parseRgLine(line);
  assert.strictEqual(ev.type, 'match');
  assert.strictEqual(ev.filePath, '/a/b.cs');
  assert.strictEqual(ev.lineNumber, 12);
  assert.strictEqual(ev.lineText, 'const foo = 1;\n');
  assert.strictEqual(ev.matchStart, 6);
  assert.strictEqual(ev.matchEnd, 9);
});

test('parseRgLine: match 事件（非 UTF-8 行用 base64）', () => {
  const line = JSON.stringify({
    type: 'match',
    data: {
      path: { text: '/a/b.txt' },
      line_number: 1,
      lines: { bytes: Buffer.from('héllo\n', 'utf8').toString('base64') },
      submatches: [{ match: { text: 'héllo' }, start: 0, end: 6 }]
    }
  });
  const ev = parseRgLine(line);
  assert.strictEqual(ev.lineText, 'héllo\n');
});

test('parseRgLine: summary 事件（统计）', () => {
  const line = JSON.stringify({
    type: 'summary',
    data: {
      elapsed_total: { secs: 0, nanos: 250000000 },
      stats: { matches: 10, matched_lines: 8 }
    }
  });
  const ev = parseRgLine(line);
  assert.strictEqual(ev.type, 'summary');
  assert.strictEqual(ev.totalMatches, 10);
  assert.strictEqual(ev.matchedLines, 8);
  assert.strictEqual(ev.elapsed, '0.25s');
});

test('parseRgLine: 未知 type 返回 null', () => {
  const line = JSON.stringify({ type: 'unknown', data: {} });
  assert.strictEqual(parseRgLine(line), null);
});
