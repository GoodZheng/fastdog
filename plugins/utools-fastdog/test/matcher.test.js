const test = require('node:test');
const assert = require('node:assert');
const { createMatcher } = require('../lib/matcher');

test('matcher: 纯文本 indexOf 快车道', () => {
  const m = createMatcher({ searchText: 'foo', isRegex: false, caseSensitive: false, wholeWord: false });
  const hits = m.findAll('foo bar foo');
  assert.strictEqual(hits.length, 2);
  assert.deepStrictEqual(hits[0], { start: 0, end: 3 });
  assert.deepStrictEqual(hits[1], { start: 8, end: 11 });
});

test('matcher: 纯文本大小写敏感', () => {
  const m = createMatcher({ searchText: 'foo', isRegex: false, caseSensitive: true, wholeWord: false });
  const hits = m.findAll('Foo foo FOO');
  assert.strictEqual(hits.length, 1); // 只匹配小写 foo
});

test('matcher: 正则模式', () => {
  const m = createMatcher({ searchText: '\\d+', isRegex: true, caseSensitive: false, wholeWord: false });
  const hits = m.findAll('abc123def456');
  assert.strictEqual(hits.length, 2);
  assert.deepStrictEqual(hits[0], { start: 3, end: 6 });
  assert.deepStrictEqual(hits[1], { start: 9, end: 12 });
});

test('matcher: 全词匹配', () => {
  const m = createMatcher({ searchText: 'foo', isRegex: false, caseSensitive: false, wholeWord: true });
  const hits = m.findAll('foo foobar xfoo');
  assert.strictEqual(hits.length, 1); // 只匹配独立 foo
  assert.deepStrictEqual(hits[0], { start: 0, end: 3 });
});

test('matcher: 无匹配返回空数组', () => {
  const m = createMatcher({ searchText: 'xyz', isRegex: false, caseSensitive: false, wholeWord: false });
  assert.strictEqual(m.findAll('abc').length, 0);
});

test('matcher: 字符偏移（中文）', () => {
  const m = createMatcher({ searchText: 'world', isRegex: false, caseSensitive: false, wholeWord: false });
  const hits = m.findAll('你好world');
  // 字符偏移：你好=2字符，world 起点 2
  assert.deepStrictEqual(hits[0], { start: 2, end: 7 });
});
