const test = require('node:test');
const assert = require('node:assert');
const { buildArgs, buildFileListArgs, normalizeFilePattern } = require('../lib/argumentBuilder');

test('buildArgs: 默认正则、不区分大小写（始终补 !.git）', () => {
  const q = { searchText: 'foo', searchPath: '/tmp', isRegex: true, caseSensitive: false, wholeWord: false, fileFilter: '', excludeDirs: '' };
  const args = buildArgs(q);
  assert.deepStrictEqual(args, ['--json', '--no-heading', '--stats', '-i', '--glob', '!.git', 'foo', '/tmp']);
});

test('buildArgs: 纯文本 + 大小写敏感 + 全词', () => {
  const q = { searchText: 'bar', searchPath: '/tmp', isRegex: false, caseSensitive: true, wholeWord: true, fileFilter: '', excludeDirs: '' };
  const args = buildArgs(q);
  assert.deepStrictEqual(args, ['--json', '--no-heading', '--stats', '-F', '-w', '--glob', '!.git', 'bar', '/tmp']);
});

test('buildArgs: 文件过滤与目录排除（始终补 .git）', () => {
  const q = { searchText: 'x', searchPath: '/tmp', isRegex: true, caseSensitive: false, wholeWord: false, fileFilter: '*.cs;.txt', excludeDirs: 'node_modules' };
  const args = buildArgs(q);
  assert.ok(args.includes('--iglob'));           // 有文件过滤
  assert.ok(args.includes('*.cs'));
  assert.ok(args.includes('*.txt'));
  assert.ok(args.includes('--glob'));
  assert.ok(args.includes('!node_modules'));
  assert.ok(args.includes('!.git'));             // 默认补 .git
});

test('buildArgs: 用户已排除 .git 时不重复添加', () => {
  const q = { searchText: 'x', searchPath: '/tmp', isRegex: true, caseSensitive: false, wholeWord: false, fileFilter: '', excludeDirs: '.git;bin' };
  const args = buildArgs(q);
  const gitCount = args.filter(a => a === '!.git').length;
  assert.strictEqual(gitCount, 1);
});

test('buildFileListArgs: --files + 过滤 + 路径', () => {
  const q = { searchPath: '/tmp', fileFilter: '*.cs', excludeDirs: '' };
  const args = buildFileListArgs(q);
  assert.strictEqual(args[0], '--files');
  assert.ok(args.includes('*.cs'));
  assert.ok(args.includes('!.git'));
  assert.ok(args.includes('/tmp'));
});

test('normalizeFilePattern: 扩展名归一化', () => {
  assert.strictEqual(normalizeFilePattern('.cs'), '*.cs');
  assert.strictEqual(normalizeFilePattern('cs'), '*.cs');
  assert.strictEqual(normalizeFilePattern('*.cs'), '*.cs');
  // 移植自 C# NormalizeFilePattern：不含点 → *.X（Makefile 不含点，归一化为 *.Makefile）
  assert.strictEqual(normalizeFilePattern('Makefile'), '*.Makefile');
  assert.strictEqual(normalizeFilePattern('a?b.txt'), 'a?b.txt');
});
