const test = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { isBinaryFile, byteToCharOffset, loadFileContent } = require('../lib/filePreview');

test('isBinaryFile: 常见二进制扩展名', () => {
  assert.strictEqual(isBinaryFile('a.exe'), true);
  assert.strictEqual(isBinaryFile('a.png'), true);
  assert.strictEqual(isBinaryFile('a.zip'), true);
  assert.strictEqual(isBinaryFile('a.cs'), false);
  assert.strictEqual(isBinaryFile('a.txt'), false);
  assert.strictEqual(isBinaryFile('README'), false);
});

test('byteToCharOffset: ASCII 不变', () => {
  assert.strictEqual(byteToCharOffset('hello', 2), 2);
});

test('byteToCharOffset: 中文多字节（每字 3 字节）', () => {
  // "你好" = 6 字节，"你" 占前 3 字节 → 字符偏移 1
  assert.strictEqual(byteToCharOffset('你好', 3), 1);
  assert.strictEqual(byteToCharOffset('你好', 6), 2);
});

test('byteToCharOffset: 边界', () => {
  assert.strictEqual(byteToCharOffset('abc', 0), 0);
  assert.strictEqual(byteToCharOffset('abc', -1), 0);
  assert.strictEqual(byteToCharOffset('abc', 999), 3);  // 超界回退到末尾
});

test('loadFileContent: 正常文本', () => {
  const f = path.join(os.tmpdir(), 'fd_test_' + Date.now() + '.txt');
  fs.writeFileSync(f, 'line1\nline2\n');
  try {
    const r = loadFileContent(f);
    assert.strictEqual(r.content, 'line1\nline2\n');
    assert.strictEqual(r.truncated, false);
    assert.strictEqual(r.isBinary, false);
  } finally {
    fs.unlinkSync(f);
  }
});

test('loadFileContent: 大文件截断标记', () => {
  // 写 > 5MB 触发截断
  const f = path.join(os.tmpdir(), 'fd_big_' + Date.now() + '.txt');
  const chunk = 'x'.repeat(1000) + '\n';
  // 同步写满 > 5MB（6000 行 × 1001 字节 ≈ 6MB）
  fs.writeFileSync(f, chunk.repeat(6000));
  try {
    const r = loadFileContent(f);
    assert.strictEqual(r.truncated, true);
    assert.strictEqual(r.isBinary, false);
  } finally {
    fs.unlinkSync(f);
  }
});

test('loadFileContent: 二进制文件', () => {
  const f = path.join(os.tmpdir(), 'fd_bin_' + Date.now() + '.exe');
  fs.writeFileSync(f, Buffer.from([0x4d, 0x5a, 0x90, 0x00]));
  try {
    const r = loadFileContent(f);
    assert.strictEqual(r.isBinary, true);
    assert.strictEqual(r.content, null);
  } finally {
    fs.unlinkSync(f);
  }
});
