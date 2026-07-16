const test = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { JsSearchEngine } = require('../lib/jsSearchEngine');

// 辅助：在临时目录建文件
function setupTmpDir(files) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'fdtest-'));
  for (const [name, content] of Object.entries(files)) {
    const fullPath = path.join(dir, name);
    fs.mkdirSync(path.dirname(fullPath), { recursive: true });
    fs.writeFileSync(fullPath, content);
  }
  return dir;
}

function runSearch(engine, query) {
  return new Promise((resolve) => {
    const events = [];
    engine.search(query, {
      onEvent: (ev) => events.push(ev),
      onDone: () => resolve(events),
    });
  });
}

test('search: 基础纯文本匹配', async () => {
  const dir = setupTmpDir({ 'a.txt': 'hello world\nfoo bar\n' });
  const engine = new JsSearchEngine();
  const events = await runSearch(engine, {
    searchText: 'foo', searchPath: dir, isRegex: false, caseSensitive: false, wholeWord: false, fileFilter: '', excludeDirs: '',
  });
  const matches = events.filter((e) => e.type === 'match');
  const summary = events.find((e) => e.type === 'summary');
  assert.strictEqual(matches.length, 1);
  assert.strictEqual(matches[0].lineNumber, 2);
  assert.strictEqual(matches[0].matchStart, 0); // 'foo' 在 'foo bar' 行首，字节偏移 0
  assert.strictEqual(matches[0].matchEnd, 3);
  assert.strictEqual(summary.totalMatches, 1);
  fs.rmSync(dir, { recursive: true });
});

test('search: 大小写敏感', async () => {
  const dir = setupTmpDir({ 'a.txt': 'Foo foo FOO\n' });
  const engine = new JsSearchEngine();
  const events = await runSearch(engine, {
    searchText: 'foo', searchPath: dir, isRegex: false, caseSensitive: true, wholeWord: false, fileFilter: '', excludeDirs: '',
  });
  const matches = events.filter((e) => e.type === 'match');
  assert.strictEqual(matches.length, 1); // 只匹配小写 foo
  fs.rmSync(dir, { recursive: true });
});

test('search: 正则模式', async () => {
  const dir = setupTmpDir({ 'a.txt': 'abc123\ndef456\n' });
  const engine = new JsSearchEngine();
  const events = await runSearch(engine, {
    searchText: '\\d+', searchPath: dir, isRegex: true, caseSensitive: false, wholeWord: false, fileFilter: '', excludeDirs: '',
  });
  const matches = events.filter((e) => e.type === 'match');
  assert.strictEqual(matches.length, 2);
  fs.rmSync(dir, { recursive: true });
});

test('search: 全词匹配', async () => {
  const dir = setupTmpDir({ 'a.txt': 'foo foobar xfoo\n' });
  const engine = new JsSearchEngine();
  const events = await runSearch(engine, {
    searchText: 'foo', searchPath: dir, isRegex: false, caseSensitive: false, wholeWord: true, fileFilter: '', excludeDirs: '',
  });
  const matches = events.filter((e) => e.type === 'match');
  assert.strictEqual(matches.length, 1); // 只匹配独立的 foo，不含 foobar/xfoo
  fs.rmSync(dir, { recursive: true });
});

test('search: 字节偏移（中文多字节）', async () => {
  const dir = setupTmpDir({ 'a.txt': '你好world\n' });
  const engine = new JsSearchEngine();
  const events = await runSearch(engine, {
    searchText: 'world', searchPath: dir, isRegex: false, caseSensitive: false, wholeWord: false, fileFilter: '', excludeDirs: '',
  });
  const m = events.find((e) => e.type === 'match');
  // '你好' = 6 字节，world 字节偏移起点应为 6
  assert.strictEqual(m.matchStart, 6);
  assert.strictEqual(m.matchEnd, 11);
  fs.rmSync(dir, { recursive: true });
});

test('search: excludeDirs 排除目录', async () => {
  const dir = setupTmpDir({
    'main.txt': 'target\n',
    'node_modules/lib.txt': 'target\n',
  });
  const engine = new JsSearchEngine();
  const events = await runSearch(engine, {
    searchText: 'target', searchPath: dir, isRegex: false, caseSensitive: false, wholeWord: false, fileFilter: '', excludeDirs: 'node_modules',
  });
  const matches = events.filter((e) => e.type === 'match');
  assert.strictEqual(matches.length, 1); // 只搜到 main.txt，node_modules 被排除
  assert.ok(matches[0].filePath.endsWith('main.txt'));
  fs.rmSync(dir, { recursive: true });
});

test('search: fileFilter 文件名过滤', async () => {
  const dir = setupTmpDir({
    'a.cs': 'target\n',
    'b.txt': 'target\n',
  });
  const engine = new JsSearchEngine();
  const events = await runSearch(engine, {
    searchText: 'target', searchPath: dir, isRegex: false, caseSensitive: false, wholeWord: false, fileFilter: '*.cs', excludeDirs: '',
  });
  const matches = events.filter((e) => e.type === 'match');
  assert.strictEqual(matches.length, 1);
  assert.ok(matches[0].filePath.endsWith('a.cs'));
  fs.rmSync(dir, { recursive: true });
});

test('search: .gitignore 解析', async () => {
  const dir = setupTmpDir({
    '.gitignore': 'dist\n*.log\n',
    'main.txt': 'target\n',
    'dist/out.txt': 'target\n',
    'app.log': 'target\n',
  });
  const engine = new JsSearchEngine();
  const events = await runSearch(engine, {
    searchText: 'target', searchPath: dir, isRegex: false, caseSensitive: false, wholeWord: false, fileFilter: '', excludeDirs: '',
  });
  const matches = events.filter((e) => e.type === 'match');
  assert.strictEqual(matches.length, 1); // 只剩 main.txt（dist/ 和 *.log 被忽略）
  assert.ok(matches[0].filePath.endsWith('main.txt'));
  fs.rmSync(dir, { recursive: true });
});

test('countFiles: 统计待搜索文件数', async () => {
  const dir = setupTmpDir({
    'a.txt': 'x', 'b.txt': 'x', 'c.cs': 'x',
    'node_modules/d.txt': 'x',
  });
  const engine = new JsSearchEngine();
  const count = await engine.countFiles(
    { searchPath: dir, fileFilter: '', excludeDirs: 'node_modules' }
  );
  assert.strictEqual(count, 3); // a/b/c，排除 node_modules
  fs.rmSync(dir, { recursive: true });
});

test('cancel: 中止搜索', async () => {
  // 建很多文件
  const files = {};
  for (let i = 0; i < 200; i++) files['f' + i + '.txt'] = 'target\n';
  const dir = setupTmpDir(files);
  const engine = new JsSearchEngine();
  let matchCount = 0;
  const promise = new Promise((resolve, reject) => {
    engine.search(
      { searchText: 'target', searchPath: dir, isRegex: false, caseSensitive: false, wholeWord: false, fileFilter: '', excludeDirs: '' },
      {
        onEvent: (e) => {
          if (e.type === 'match') {
            matchCount++;
            if (matchCount === 5) engine.cancel();
          }
        },
        onDone: resolve, onError: reject,
      }
    );
  });
  await promise;
  assert.ok(matchCount < 200, '取消后不应继续匹配所有 200 个文件');
  fs.rmSync(dir, { recursive: true });
});
