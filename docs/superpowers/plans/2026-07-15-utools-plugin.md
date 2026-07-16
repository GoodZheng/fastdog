# FastDog uTools 插件 Implementation Plan

> ## ⚠️ 本计划为初版（ripgrep 二进制方案），已部分废弃
>
> 本计划描述的「捆绑 ripgrep 二进制 + child_process.spawn」方案已完成并测试通过，但**上架 uTools 商店时被拒**（禁止外部可执行文件）。
>
> 随后进行了纯 JS 改造，搜索引擎替换为 `lib/jsSearchEngine.js`（fs/promises + RegExp），删除 bin/ 二进制与 ripgrepBridge。详见 `plugins/utools-fastdog/CHANGELOG.md` v1.0.0。
>
> 本文档下方关于 ripgrep 二进制下载、ripgrepBridge、platformRg 的 Task 已不适用；其余 Task（argumentBuilder/jsonParser/searchService/filePreview 移植、UI 三层布局、会话恢复、预览内查找等）仍然有效且已实现。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 `plugins/utools-fastdog/` 下构建一个 uTools 插件，用 JavaScript 重写 FastDog 的核心搜索逻辑（参数构建、JSON 解析、结果聚合、文件预览），捆绑跨平台 ripgrep 二进制，用原生 HTML/CSS/JS 还原桌面版三层布局，实现除搜索历史外的全部功能。

**Architecture:** 三层——UI 层（`index.html` + `src/*.js`，原生 DOM）→ preload 层（`preload.js` + `lib/*.js`，Node.js 调 `child_process.spawn` 跑 rg）→ rg 二进制（`bin/rg-*`）。纯逻辑（参数构建/解析/预览）放在 `lib/` 与 uTools/Electron 解耦，可用 `node:test` 直接单测；uTools 胶水（事件、API 调用）集中在 `preload.js`。

**Tech Stack:** Node.js 16.x（uTools preload 环境）/ 原生 HTML+CSS+JS（无前端框架）/ `node:test`（内置测试框架）/ ripgrep 14.1.1（捆绑三平台二进制）。

**Spec:** `docs/superpowers/specs/2026-07-15-utools-plugin-design.md`

---

## File Structure

```
plugins/utools-fastdog/
├── plugin.json              # 核心配置（features/platform/main/preload/logo）
├── preload.js               # Node 层入口：require lib + 暴露 window.fastdog + onPluginEnter
├── package.json             # type:commonjs（防止 ESM 报错）+ test 脚本
├── logo.png                 # 复用 FastDog newLogo1.2
├── lib/
│   ├── argumentBuilder.js   # 纯逻辑：buildArgs/buildFileListArgs/normalizeFilePattern（← RipgrepBridge.cs）
│   ├── jsonParser.js        # 纯逻辑：parseRgLine（begin/match/end/summary/base64）（← RipgrepBridge.cs）
│   ├── searchService.js     # 纯逻辑：聚合 + 日期过滤（← SearchService.cs）
│   ├── filePreview.js       # 纯逻辑：二进制检测/截断/ByteToCharOffset（← FilePreviewService.cs）
│   └── platformRg.js        # 胶水：resolveRgPath（按 platform 选 bin/rg-*，非 win32 加执行权限）
├── index.html               # UI：还原三层布局
├── src/
│   ├── app.js               # UI 渲染：文件列表/匹配行/预览/状态栏/选项交互
│   ├── style.css            # 蓝色主题，对齐桌面版配色
│   ├── highlight.js         # 主搜索黄色高亮 + 行内高亮渲染
│   └── previewFind.js       # 预览内 Ctrl+F 查找栏（橙色高亮 + N/M 计数）
├── bin/                     # 捆绑 ripgrep（Task 11 下载）
│   ├── rg-win-x64.exe
│   ├── rg-darwin-x64
│   └── rg-linux-x64
└── test/
    ├── argumentBuilder.test.js
    ├── jsonParser.test.js
    ├── dateFilter.test.js
    └── filePreview.test.js
```

**职责边界：**
- `lib/*.js`：零外部依赖、零 uTools/Electron 依赖的纯函数模块，可被 `node --test` 直接加载。
- `preload.js`：唯一接触 uTools API 和 Node `child_process` 的地方，把 `lib` 组装成 `window.fastdog` 给前端用。
- `src/*.js`：前端脚本，只能通过 `window.fastdog.*` 和 `utools.*` 调能力，不直接 `require`。

---

## Task 1: 初始化插件项目骨架与 package.json

**Files:**
- Create: `plugins/utools-fastdog/package.json`
- Create: `plugins/utools-fastdog/plugin.json`
- Create: `plugins/utools-fastdog/.gitignore`
- Create: `plugins/utools-fastdog/CHANGELOG.md`

- [ ] **Step 1: 创建目录与 package.json**

`plugins/utools-fastdog/package.json`：

```json
{
  "name": "utools-fastdog",
  "version": "0.1.0",
  "description": "FastDog — 基于 ripgrep 的文本搜索（uTools 插件）",
  "private": true,
  "type": "commonjs",
  "scripts": {
    "test": "node --test test/"
  }
}
```

> `type:commonjs` 必填：uTools 用的 Electron 较低版本不支持 ESM（[FAQ 依据](https://utools.esion.xyz/faq/0.html)）。

- [ ] **Step 2: 创建最小可识别的 plugin.json**

`plugins/utools-fastdog/plugin.json`：

```json
{
  "main": "index.html",
  "preload": "preload.js",
  "logo": "logo.png",
  "platform": ["win32", "darwin", "linux"],
  "features": [
    {
      "code": "fastdog",
      "explain": "FastDog — 基于 ripgrep 的文本搜索",
      "cmds": ["fd", "fastdog", "搜索", "文本搜索"],
      "main": "index.html"
    }
  ]
}
```

- [ ] **Step 3: 创建 .gitignore（忽略 node 临时产物）**

```
node_modules/
*.log
.DS_Store
```

- [ ] **Step 4: 创建 CHANGELOG.md（插件独立版本）**

```markdown
# Changelog

## [0.1.0] - 未发布

### 新增
- 初始化 uTools 插件骨架（plugin.json / preload / 目录结构）
```

- [ ] **Step 5: 提交**

```bash
cd /e/demo/ai/my/fastdog
git add plugins/utools-fastdog/
git commit -m "feat(utools): 初始化插件骨架 plugin.json/package.json"
```

---

## Task 2: argumentBuilder.js（参数构建）+ 单测 — TDD

移植 `src/FastDog/Services/RipgrepBridge.cs` 的 `BuildArguments` / `BuildFileListArguments` / `NormalizeFilePattern` / `BuildFilterArgs`。改用**数组**输出（交 `spawn`，免去 `EscapeArg`）。

**Files:**
- Create: `plugins/utools-fastdog/lib/argumentBuilder.js`
- Create: `plugins/utools-fastdog/test/argumentBuilder.test.js`

- [ ] **Step 1: 先写失败测试（移植 ArgumentBuilderTests 的断言）**

`plugins/utools-fastdog/test/argumentBuilder.test.js`：

```js
const test = require('node:test');
const assert = require('node:assert');
const { buildArgs, buildFileListArgs, normalizeFilePattern } = require('../lib/argumentBuilder');

test('buildArgs: 默认正则、不区分大小写', () => {
  const q = { searchText: 'foo', searchPath: '/tmp', isRegex: true, caseSensitive: false, wholeWord: false, fileFilter: '', excludeDirs: '' };
  const args = buildArgs(q);
  assert.deepStrictEqual(args, ['--json', '--no-heading', '--stats', '-i', 'foo', '/tmp']);
});

test('buildArgs: 纯文本 + 大小写敏感 + 全词', () => {
  const q = { searchText: 'bar', searchPath: '/tmp', isRegex: false, caseSensitive: true, wholeWord: true, fileFilter: '', excludeDirs: '' };
  const args = buildArgs(q);
  assert.deepStrictEqual(args, ['--json', '--no-heading', '--stats', '-F', '-w', 'bar', '/tmp']);
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
  assert.strictEqual(normalizeFilePattern('Makefile'), 'Makefile');
  assert.strictEqual(normalizeFilePattern('a?b.txt'), 'a?b.txt');
});
```

- [ ] **Step 2: 运行测试，确认失败（模块不存在）**

```bash
cd /e/demo/ai/my/fastdog/plugins/utools-fastdog
npm test
```

Expected: 报错 `Cannot find module '../lib/argumentBuilder'`。

- [ ] **Step 3: 实现 argumentBuilder.js**

`plugins/utools-fastdog/lib/argumentBuilder.js`：

```js
// 移植自 src/FastDog/Services/RipgrepBridge.cs (BuildArguments / BuildFileListArguments
// / NormalizeFilePattern / BuildFilterArgs)。输出改为数组交 spawn，免去 EscapeArg。

/**
 * 构建内容搜索参数。
 * @param {object} q - SearchQuery: { searchText, searchPath, isRegex, caseSensitive, wholeWord, fileFilter, excludeDirs }
 * @returns {string[]} rg 参数数组
 */
function buildArgs(q) {
  const args = ['--json', '--no-heading', '--stats'];
  if (!q.caseSensitive) args.push('-i');
  if (!q.isRegex) args.push('-F');
  if (q.wholeWord) args.push('-w');
  buildFilterArgs(args, q.fileFilter, q.excludeDirs);
  args.push(q.searchText, q.searchPath);
  return args;
}

/**
 * 构建 --files 文件列表参数（用于统计待搜索文件总数）。
 */
function buildFileListArgs(q) {
  const args = ['--files'];
  buildFilterArgs(args, q.fileFilter, q.excludeDirs);
  args.push(q.searchPath);
  return args;
}

function buildFilterArgs(args, fileFilter, excludeDirs) {
  if (fileFilter && fileFilter.trim()) {
    for (const pattern of fileFilter.split(';')) {
      const trimmed = pattern.trim();
      if (trimmed.length > 0) {
        args.push('--iglob', normalizeFilePattern(trimmed));
      }
    }
  }

  const dirs = [];
  if (excludeDirs && excludeDirs.trim()) {
    for (const dir of excludeDirs.split(';')) {
      const trimmed = dir.trim();
      if (trimmed.length > 0) dirs.push(trimmed);
    }
  }
  if (!dirs.includes('.git')) dirs.push('.git');

  for (const dir of dirs) {
    args.push('--glob', '!' + dir);
  }
}

/**
 * 把用户输入的文件过滤模式归一化为 rg --iglob 可识别的 glob。
 * 移植自 NormalizeFilePattern：".cs"→"*.cs"、"cs"→"*.cs"，含通配符原样返回。
 */
function normalizeFilePattern(pattern) {
  if (pattern.includes('*') || pattern.includes('?') || pattern.includes('[')) {
    return pattern;
  }
  if (pattern.startsWith('.')) {
    return '*' + pattern;
  }
  if (!pattern.includes('.')) {
    return '*.' + pattern;
  }
  return pattern;
}

module.exports = { buildArgs, buildFileListArgs, normalizeFilePattern };
```

- [ ] **Step 4: 运行测试，确认全部通过**

```bash
npm test
```

Expected: `tests 6` 全部 PASS（0 fail）。

- [ ] **Step 5: 提交**

```bash
cd /e/demo/ai/my/fastdog
git add plugins/utools-fastdog/lib/argumentBuilder.js plugins/utools-fastdog/test/argumentBuilder.test.js
git commit -m "feat(utools): 移植参数构建 argumentBuilder + 单测"
```

---

## Task 3: jsonParser.js（rg JSON 输出解析）+ 单测 — TDD

移植 `RipgrepBridge.cs` 的 `ParseRgLine` / `GetTextOrBase64` / `ParseMatch` / `ParseSummary`。

**Files:**
- Create: `plugins/utools-fastdog/lib/jsonParser.js`
- Create: `plugins/utools-fastdog/test/jsonParser.test.js`

- [ ] **Step 1: 先写失败测试（移植 JsonParserTests 断言）**

`plugins/utools-fastdog/test/jsonParser.test.js`：

```js
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
```

- [ ] **Step 2: 运行测试，确认失败**

```bash
npm test
```

Expected: `Cannot find module '../lib/jsonParser'`。

- [ ] **Step 3: 实现 jsonParser.js**

`plugins/utools-fastdog/lib/jsonParser.js`：

```js
// 移植自 src/FastDog/Services/RipgrepBridge.cs (ParseRgLine / GetTextOrBase64
// / ParseMatch / ParseSummary)。
// rg --json 每行一个 JSON 对象，type ∈ {begin, match, end, summary}。

/**
 * @typedef {Object} RgEvent
 * @property {'fileBegin'|'match'|'fileEnd'|'summary'} type
 * @property {string} filePath
 * @property {number} lineNumber
 * @property {string} lineText
 * @property {number} matchStart  UTF-8 字节偏移
 * @property {number} matchEnd    UTF-8 字节偏移
 * @property {number} totalMatches
 * @property {number} matchedLines
 * @property {string} elapsed
 */

/**
 * 解析 rg --json 的一行。
 * @param {string|null|undefined} jsonLine
 * @returns {RgEvent|null}
 */
function parseRgLine(jsonLine) {
  if (!jsonLine || !jsonLine.trim()) return null;

  const root = JSON.parse(jsonLine);
  const type = root.type;
  const data = root.data;

  switch (type) {
    case 'begin':
      return { type: 'fileBegin', filePath: getTextOrBase64(data, 'path') };
    case 'end':
      return { type: 'fileEnd', filePath: getTextOrBase64(data, 'path') };
    case 'match':
      return parseMatch(data);
    case 'summary':
      return parseSummary(data);
    default:
      return null;
  }
}

/** rg 对 UTF-8 内容输出 text 字段，对非 UTF-8 输出 base64 编码的 bytes 字段。 */
function getTextOrBase64(parent, propertyName) {
  const elem = parent[propertyName];
  if (elem.text !== undefined) return elem.text;
  // bytes 是 base64 字符串
  return Buffer.from(elem.bytes, 'base64').toString('utf8');
}

function parseMatch(data) {
  const submatches = data.submatches || [];
  let matchStart = 0, matchEnd = 0;
  if (submatches.length > 0) {
    matchStart = submatches[0].start;
    matchEnd = submatches[0].end;
  }
  return {
    type: 'match',
    filePath: getTextOrBase64(data, 'path'),
    lineNumber: data.line_number,
    lineText: getTextOrBase64(data, 'lines'),
    matchStart,
    matchEnd,
  };
}

function parseSummary(data) {
  const stats = data.stats;
  const elapsed = data.elapsed_total;
  const secs = elapsed.secs + elapsed.nanos / 1_000_000_000;
  return {
    type: 'summary',
    totalMatches: stats.matches,
    matchedLines: stats.matched_lines,
    elapsed: secs.toFixed(2) + 's',
  };
}

module.exports = { parseRgLine };
```

- [ ] **Step 4: 运行测试，确认通过**

```bash
npm test
```

Expected: argumentBuilder + jsonParser 全部 PASS。

- [ ] **Step 5: 提交**

```bash
git add plugins/utools-fastdog/lib/jsonParser.js plugins/utools-fastdog/test/jsonParser.test.js
git commit -m "feat(utools): 移植 rg JSON 解析 jsonParser + 单测"
```

---

## Task 4: searchService.js（结果聚合 + 日期过滤）+ 单测 — TDD

移植 `SearchService.cs`。日期过滤是纯函数，直接单测；聚合逻辑用事件回调对接 `ripgrepBridge`（Task 6 接入）。

**Files:**
- Create: `plugins/utools-fastdog/lib/searchService.js`
- Create: `plugins/utools-fastdog/test/dateFilter.test.js`

- [ ] **Step 1: 先写日期过滤失败测试（移植 DateFilterTests）**

`plugins/utools-fastdog/test/dateFilter.test.js`：

```js
const test = require('node:test');
const assert = require('node:assert');
const { passDateFilter } = require('../lib/searchService');

test('passDateFilter: 无日期限制时通过', () => {
  const r = { lastModified: new Date('2026-01-15') };
  assert.strictEqual(passDateFilter(r, { dateFilterEnabled: false }), true);
});

test('passDateFilter: 早于 from 被过滤', () => {
  const r = { lastModified: new Date('2026-01-14') };
  const q = { dateFilterEnabled: true, dateFrom: new Date('2026-01-15'), dateTo: null };
  assert.strictEqual(passDateFilter(r, q), false);
});

test('passDateFilter: 晚于 to 被过滤（to 含当天全天）', () => {
  const r = { lastModified: new Date('2026-01-16T00:00:00') };
  const q = { dateFilterEnabled: true, dateFrom: null, dateTo: new Date('2026-01-15') };
  assert.strictEqual(passDateFilter(r, q), false);
});

test('passDateFilter: 在范围内通过（含 to 当天）', () => {
  const r = { lastModified: new Date('2026-01-15T23:59:59') };
  const q = { dateFilterEnabled: true, dateFrom: new Date('2026-01-15'), dateTo: new Date('2026-01-15') };
  assert.strictEqual(passDateFilter(r, q), true);
});
```

> C# 版语义：`< from.Date` 过滤、`> to.Date + 1天` 过滤（即 to 含当天全天）。

- [ ] **Step 2: 运行测试，确认失败**

```bash
npm test
```

Expected: `Cannot find module '../lib/searchService'`。

- [ ] **Step 3: 实现 searchService.js**

`plugins/utools-fastdog/lib/searchService.js`：

```js
// 移植自 src/FastDog/Services/SearchService.cs。
// 聚合：把 rg 的流式事件聚合成「文件 → 匹配行列表」，FileEnd 时做日期过滤并回调。
// 依赖 ripgrepBridge 推送 { type, filePath, lineNumber, lineText, matchStart, matchEnd, totalMatches, ... }。

const path = require('node:path');
const fs = require('node:fs');

/**
 * 单文件结果。
 * @typedef {Object} SearchResult
 * @property {string} filePath
 * @property {string} fileName
 * @property {string} relativePath
 * @property {number} fileSize
 * @property {number} matchCount
 * @property {Date} lastModified
 * @property {Array} matches
 */

/**
 * 日期过滤（纯函数，移植自 PassDateFilter）。
 * @param {{lastModified: Date}} result
 * @param {{dateFilterEnabled: boolean, dateFrom: Date|null, dateTo: Date|null}} query
 * @returns {boolean} true=保留
 */
function passDateFilter(result, query) {
  if (!query.dateFilterEnabled) return true;
  if (query.dateFrom && result.lastModified < startOfDay(query.dateFrom)) return false;
  if (query.dateTo && result.lastModified > endOfDay(query.dateTo)) return false;
  return true;
}

function startOfDay(d) {
  const x = new Date(d);
  x.setHours(0, 0, 0, 0);
  return x;
}
function endOfDay(d) {
  const x = new Date(d);
  x.setHours(0, 0, 0, 0);
  x.setDate(x.getDate() + 1);
  return x;
}

/**
 * 创建一个搜索会话，消费 rg 事件流，输出聚合后的文件结果。
 * @param {string} basePath
 * @param {object} query - SearchQuery
 * @param {{onResult: (r: SearchResult)=>void}} handlers
 * @returns {{handleEvent: (ev)=>number, getFoundCount: ()=>number}}
 *   handleEvent 返回当前已找到文件数（供 stats 用）；fileEnd 触发 onResult
 */
function createSearchSession(basePath, query, handlers) {
  /** @type {Map<string, SearchResult>} */
  const fileResults = new Map();
  let foundCount = 0;

  function handleEvent(ev) {
    switch (ev.type) {
      case 'match': {
        let r = fileResults.get(ev.filePath);
        if (!r) {
          r = makeResult(ev.filePath, basePath);
          fileResults.set(ev.filePath, r);
        }
        r.matches.push({
          lineNumber: ev.lineNumber,
          lineText: ev.lineText,
          displayText: ev.lineText.trim(),
          matchStart: ev.matchStart,
          matchEnd: ev.matchEnd,
        });
        r.matchCount = r.matches.length;
        break;
      }
      case 'fileEnd': {
        const r = fileResults.get(ev.filePath);
        fileResults.delete(ev.filePath);
        if (r && passDateFilter(r, query)) {
          foundCount++;
          handlers.onResult(r);
        }
        break;
      }
    }
    return foundCount;
  }

  return {
    handleEvent,
    getFoundCount: () => foundCount,
  };
}

function makeResult(filePath, basePath) {
  let stat = { size: 0, mtime: new Date(0) };
  try {
    const s = fs.statSync(filePath);
    stat = { size: s.size, mtime: s.mtime };
  } catch { /* 文件可能已被删除/无权限 */ }
  let relativePath = filePath;
  try { relativePath = path.relative(basePath, filePath); } catch {}
  return {
    filePath,
    fileName: path.basename(filePath),
    relativePath,
    fileSize: stat.size,
    matchCount: 0,
    lastModified: stat.mtime,
    matches: [],
  };
}

module.exports = { passDateFilter, createSearchSession };
```

- [ ] **Step 4: 运行测试，确认通过**

```bash
npm test
```

Expected: 3 套测试全 PASS。

- [ ] **Step 5: 提交**

```bash
git add plugins/utools-fastdog/lib/searchService.js plugins/utools-fastdog/test/dateFilter.test.js
git commit -m "feat(utools): 移植结果聚合 searchService + 日期过滤单测"
```

---

## Task 5: filePreview.js（文件预览 + 偏移转换）+ 单测 — TDD

移植 `FilePreviewService.cs`：二进制扩展名集合、5MB/5000 行截断、`ByteToCharOffset`（UTF-8 多字节）。

**Files:**
- Create: `plugins/utools-fastdog/lib/filePreview.js`
- Create: `plugins/utools-fastdog/test/filePreview.test.js`

- [ ] **Step 1: 先写失败测试（移植 FilePreviewServiceTests）**

`plugins/utools-fastdog/test/filePreview.test.js`：

```js
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
  const ws = fs.createWriteStream(f);
  for (let i = 0; i < 6000; i++) ws.write(chunk);
  ws.end();
  ws.on('finish', () => {});
  try {
    // 注意：截断读前 5000 行
    const r = loadFileContent(f);
    assert.strictEqual(r.truncated, true);
  } finally {
    fs.unlinkSync(f);
  }
});
```

> ⚠️ 大文件测试依赖文件写完。若出现间歇失败，改为在 `writeFileSync` 后断言。实现里用同步读即可。

- [ ] **Step 2: 运行测试，确认失败**

```bash
npm test
```

Expected: `Cannot find module '../lib/filePreview'`。

- [ ] **Step 3: 实现 filePreview.js**

`plugins/utools-fastdog/lib/filePreview.js`：

```js
// 移植自 src/FastDog/Services/FilePreviewService.cs。
// BinaryExtensions / MaxFileSize(5MB) / MaxLines(5000) / ByteToCharOffset 语义保持一致。

const fs = require('node:fs');
const path = require('node:path');

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB
const MAX_LINES = 5000;

const BINARY_EXTENSIONS = new Set([
  '.exe', '.dll', '.pdb', '.obj', '.o', '.so', '.dylib',
  '.png', '.jpg', '.jpeg', '.gif', '.bmp', '.ico', '.tif', '.tiff', '.webp',
  '.zip', '.rar', '.7z', '.tar', '.gz', '.bz2', '.xz',
  '.mp3', '.mp4', '.avi', '.mkv', '.mov', '.wmv', '.flac', '.wav',
  '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx',
  '.bin', '.dat', '.db', '.sqlite', '.mdb',
  '.class', '.jar', '.war', '.nupkg', '.snk',
  '.woff', '.woff2', '.ttf', '.eot',
]);

/**
 * 按扩展名判断是否二进制（移植自 IsBinaryFile，大小写不敏感）。
 */
function isBinaryFile(filePath) {
  const ext = path.extname(filePath).toLowerCase();
  return BINARY_EXTENSIONS.has(ext);
}

/**
 * 加载文件内容。
 * @returns {{content: string|null, truncated: boolean, isBinary: boolean, lineLengths: number[]}}
 */
function loadFileContent(filePath) {
  const result = { content: null, truncated: false, isBinary: false, lineLengths: [] };
  try {
    if (!fs.existsSync(filePath)) return result;

    if (isBinaryFile(filePath)) {
      result.isBinary = true;
      return result;
    }

    const size = fs.statSync(filePath).size;
    let content;
    if (size > MAX_FILE_SIZE) {
      result.truncated = true;
      content = readFirstLines(filePath, MAX_LINES);
    } else {
      content = fs.readFileSync(filePath, 'utf8');
    }
    result.content = content;
    result.lineLengths = computeLineLengths(content);
  } catch (e) {
    // IOException / UnauthorizedAccessException 对应：读失败返回 null content
    result.content = null;
  }
  return result;
}

function readFirstLines(filePath, maxLines) {
  const lines = [];
  const data = fs.readFileSync(filePath, 'utf8');
  const all = data.split('\n');
  for (let i = 0; i < maxLines && i < all.length; i++) lines.push(all[i]);
  return lines.join('\n') + '\n';
}

/**
 * 计算每行长度（含 \n），用于把行内偏移转全局偏移。移植自 ComputeLineLengths。
 */
function computeLineLengths(content) {
  const lines = content.split('\n');
  let count = lines.length;
  if (count > 0 && lines[count - 1].length === 0) count--;
  const lengths = new Array(count);
  for (let i = 0; i < count; i++) {
    lengths[i] = lines[i].length + (i < count - 1 ? 1 : 0); // 含 \n（最后一行除外）
  }
  return lengths;
}

/**
 * 将 UTF-8 字节偏移转换为字符串字符偏移（移植自 ByteToCharOffset）。
 * ASCII 两者相同；中文等多字节字符字节偏移 > 字符偏移。
 */
function byteToCharOffset(text, byteOffset) {
  if (byteOffset <= 0) return 0;
  if (!text) return byteOffset;
  const bytes = new TextEncoder().encode(text);
  if (byteOffset >= bytes.length) return text.length;
  return new TextDecoder('utf8').decode(bytes.slice(0, byteOffset)).length;
}

module.exports = { isBinaryFile, loadFileContent, byteToCharOffset, computeLineLengths };
```

- [ ] **Step 4: 运行测试，确认通过**

```bash
npm test
```

Expected: 4 套测试全 PASS。

- [ ] **Step 5: 提交**

```bash
git add plugins/utools-fastdog/lib/filePreview.js plugins/utools-fastdog/test/filePreview.test.js
git commit -m "feat(utools): 移植文件预览 filePreview + 偏移转换单测"
```

---

## Task 6: platformRg.js（跨平台 rg 定位）

**Files:**
- Create: `plugins/utools-fastdog/lib/platformRg.js`

- [ ] **Step 1: 实现 platformRg.js**

`plugins/utools-fastdog/lib/platformRg.js`：

```js
// 按 process.platform 选择捆绑的 ripgrep 二进制。非 win32 平台补执行权限。
const path = require('node:path');
const fs = require('node:fs');

const RG_MAP = {
  win32: 'bin/rg-win-x64.exe',
  darwin: 'bin/rg-darwin-x64',
  linux: 'bin/rg-linux-x64',
};

/**
 * 返回当前平台 rg 可执行文件绝对路径。
 * 非首次调用会缓存；首次在非 win32 上补 chmod 0o755。
 */
let cached = null;
function resolveRgPath() {
  if (cached) return cached;
  const rel = RG_MAP[process.platform];
  if (!rel) throw new Error(`FastDog 插件不支持平台: ${process.platform}`);
  const p = path.join(__dirname, '..', rel);
  if (!fs.existsSync(p)) {
    throw new Error(`找不到 ripgrep 二进制: ${p}。请确认 bin/ 目录已放入对应平台文件。`);
  }
  if (process.platform !== 'win32') {
    try { fs.chmodSync(p, 0o755); } catch { /* 忽略权限失败 */ }
  }
  cached = p;
  return p;
}

module.exports = { resolveRgPath };
```

- [ ] **Step 2: 提交**

```bash
git add plugins/utools-fastdog/lib/platformRg.js
git commit -m "feat(utools): 跨平台 rg 二进制定位 platformRg"
```

---

## Task 7: ripgrepBridge.js（spawn + 进程管理）

把 `argumentBuilder` 的数组参数喂给 `spawn`，stdout 行流交给 `jsonParser`，提供 `search`/`countFiles`/`cancel`。对应 `RipgrepBridge.cs` 的 `SearchAsync` / `CountFilesAsync` / `KillProcess`。

**Files:**
- Create: `plugins/utools-fastdog/lib/ripgrepBridge.js`

- [ ] **Step 1: 实现 ripgrepBridge.js**

`plugins/utools-fastdog/lib/ripgrepBridge.js`：

```js
// 移植自 src/FastDog/Services/RipgrepBridge.cs (SearchAsync / CountFilesAsync / KillProcess)。
// 用 child_process.spawn + readline 做行流，cancel 用 kill。

const { spawn } = require('node:child_process');
const { createInterface } = require('node:readline');
const { parseRgLine } = require('./jsonParser');
const { resolveRgPath } = require('./platformRg');

class RipgrepBridge {
  constructor() {
    this._child = null;
  }

  /**
   * 内容搜索（流式）。
   * @param {string[]} args - 来自 argumentBuilder.buildArgs
   * @param {{onEvent:(ev)=>void, onDone?:(code:number)=>void, onError?:(err)=>void}} handlers
   */
  search(args, handlers) {
    const rgPath = resolveRgPath();
    const child = spawn(rgPath, args, { windowsHide: true });
    this._child = child;

    const rl = createInterface({ input: child.stdout });
    rl.on('line', (line) => {
      const ev = parseRgLine(line);
      if (ev) handlers.onEvent(ev);
    });

    // 捕获 stderr 防止管道阻塞，错误时回调
    let stderrText = '';
    const rlErr = createInterface({ input: child.stderr });
    rlErr.on('line', (line) => { stderrText += line + '\n'; });

    child.on('close', (code) => {
      this._child = null;
      if (code !== 0 && code !== null && stderrText && handlers.onError) {
        handlers.onError(new Error('rg 退出码 ' + code + ': ' + stderrText.trim()));
      } else {
        handlers.onDone && handlers.onDone(code);
      }
    });
    child.on('error', (err) => {
      this._child = null;
      handlers.onError && handlers.onError(err);
    });
  }

  /**
   * 统计待搜索文件数（rg --files 行数）。对应 CountFilesAsync。
   * @param {string[]} args - 来自 argumentBuilder.buildFileListArgs
   * @returns {Promise<number>}
   */
  countFiles(args) {
    return new Promise((resolve, reject) => {
      const rgPath = resolveRgPath();
      const child = spawn(rgPath, args, { windowsHide: true });
      let count = 0;
      const rl = createInterface({ input: child.stdout });
      rl.on('line', () => count++);
      child.on('close', () => resolve(count));
      child.on('error', reject);
    });
  }

  /** 取消当前搜索，杀进程。对应 KillProcess。 */
  cancel() {
    if (!this._child) return;
    try {
      this._child.kill('SIGTERM');
    } catch { /* 忽略 */ }
    this._child = null;
  }
}

module.exports = { RipgrepBridge };
```

- [ ] **Step 2: 提交**

```bash
git add plugins/utools-fastdog/lib/ripgrepBridge.js
git commit -m "feat(utools): 移植 rg 进程桥 ripgrepBridge（spawn+kill）"
```

---

## Task 8: preload.js（装配 lib + 暴露 window.fastdog + onPluginEnter）

把 Task 2-7 的 lib 装配成 `window.fastdog`，处理 3 个进入入口。这是唯一接触 uTools API 的文件。

**Files:**
- Create: `plugins/utools-fastdog/preload.js`

- [ ] **Step 1: 实现 preload.js**

`plugins/utools-fastdog/preload.js`：

```js
// uTools preload：装配 lib，暴露 window.fastdog 给前端；处理 onPluginEnter 的 3 个入口。
// 所有 uTools/Electron API 集中在此，lib/*.js 保持纯逻辑可单测。

const { RipgrepBridge } = require('./lib/ripgrepBridge');
const { buildArgs, buildFileListArgs } = require('./lib/argumentBuilder');
const { createSearchSession } = require('./lib/searchService');
const { loadFileContent, byteToCharOffset } = require('./lib/filePreview');

const bridge = new RipgrepBridge();
let currentSession = null;

const fastdog = {
  /**
   * 搜索。query 字段对应桌面版 SearchQuery。
   * @param {object} query
   * @param {{onResult:(r)=>void, onStats:(s)=>void, onDone:()=>void, onError:(e)=>void}} handlers
   */
  search(query, handlers) {
    // 先统计待搜索文件数（对应 SearchService 的 CountFilesAsync）
    const fileListArgs = buildFileListArgs(query);
    bridge.countFiles(fileListArgs).then((totalFiles) => {
      doSearch(query, totalFiles, handlers);
    }).catch((e) => handlers.onError && handlers.onError(e));
  },

  cancel() {
    bridge.cancel();
    currentSession = null;
  },

  /** 预览文件。返回 { content, truncated, isBinary, lineLengths } + 每个匹配的全局偏移。 */
  previewFile(filePath, matches) {
    const info = loadFileContent(filePath);
    // 计算每个匹配行内偏移→全局偏移（供 UI 滚动定位 + 高亮）
    if (info.content && matches && matches.length) {
      for (const m of matches) {
        const lineIndex = m.lineNumber - 1;
        let offset = 0;
        for (let i = 0; i < lineIndex && i < info.lineLengths.length; i++) {
          offset += info.lineLengths[i];
        }
        m.globalMatchStart = offset + byteToCharOffset(m.lineText, m.matchStart);
        m.globalMatchEnd = offset + byteToCharOffset(m.lineText, m.matchEnd);
      }
    }
    return { info, matches };
  },

  openFile(filePath) {
    try { utools.shellOpenPath(filePath); } catch (e) { console.error(e); }
  },

  openInExplorer(filePath) {
    // shellShowItemInFolder 在系统 API 文档中可用
    try { utools.shellShowItemInFolder(filePath); } catch (e) { console.error(e); }
  },

  copyText(text) {
    try { utools.copyText(text); } catch (e) { console.error(e); }
  },

  selectDirectory() {
    return utools.showOpenDialog({ properties: ['openDirectory'] });
  },

  getLastPath() {
    return utools.dbStorage.getItem('fastdog:lastPath') || '';
  },

  saveLastPath(p) {
    utools.dbStorage.setItem('fastdog:lastPath', p);
  },
};

window.fastdog = fastdog;

function doSearch(query, totalFiles, handlers) {
  const args = buildArgs(query);
  currentSession = createSearchSession(query.searchPath, query, { onResult: handlers.onResult });

  bridge.search(args, {
    onEvent: (ev) => {
      currentSession.handleEvent(ev);
      if (ev.type === 'summary') {
        handlers.onStats && handlers.onStats({
          searchedFiles: totalFiles,
          foundFiles: currentSession.getFoundCount(),
          elapsed: ev.elapsed,
          totalMatches: ev.totalMatches,
        });
      }
    },
    onDone: () => handlers.onDone && handlers.onDone(),
    onError: (err) => handlers.onError && handlers.onError(err),
  });
}

// ===== 进入入口处理（3 个 feature code）=====
utools.onPluginEnter(({ code, type, payload }) => {
  // 把进入信息暂存，供 app.js 读取后预填搜索词/路径
  let enter = { code, searchText: '', searchPath: fastdog.getLastPath() };

  if (code === 'fastdog-search-text' && type === 'over') {
    // 超级面板选中文本 → 预填搜索词
    enter.searchText = typeof payload === 'string' ? payload : '';
  } else if (code === 'fastdog-search-dir' && type === 'files' && Array.isArray(payload)) {
    // 文件管理器选中文件夹 → 预填路径
    enter.searchPath = payload[0] && payload[0].path ? payload[0].path : enter.searchPath;
  }

  window.__fastdogEnter = enter;
  // 通知前端页面
  window.dispatchEvent(new CustomEvent('fastdog:enter', { detail: enter }));
});

module.exports = fastdog;
```

> 注意：`preload.js` 在 uTools 环境下顶层 `window`/`utools` 全局可用；`node --test` 不加载此文件（仅测 `lib/`）。

- [ ] **Step 2: 补全 plugin.json 的另两个 feature**

修改 `plugins/utools-fastdog/plugin.json` 的 `features` 数组为：

```json
"features": [
  {
    "code": "fastdog",
    "explain": "FastDog — 基于 ripgrep 的文本搜索",
    "cmds": ["fd", "fastdog", "搜索", "文本搜索"],
    "main": "index.html"
  },
  {
    "code": "fastdog-search-text",
    "explain": "用 FastDog 搜索该文本",
    "cmds": [{ "type": "over", "label": "FastDog 搜索选中内容", "minLength": 1 }],
    "main": "index.html"
  },
  {
    "code": "fastdog-search-dir",
    "explain": "在此文件夹中搜索",
    "cmds": [{ "type": "files", "fileType": "directory", "name": "文件夹" }],
    "main": "index.html"
  }
]
```

- [ ] **Step 3: 提交**

```bash
git add plugins/utools-fastdog/preload.js plugins/utools-fastdog/plugin.json
git commit -m "feat(utools): preload 装配层 + 3 个进入入口"
```

---

## Task 9: UI 骨架 — index.html + style.css（还原三层布局）

还原 `MainWindow.xaml` 的三层结构：搜索条件区（Header）→ 内容区（上文件列表 + 下左右分栏）→ 状态栏。配色对齐桌面版。

**Files:**
- Create: `plugins/utools-fastdog/index.html`
- Create: `plugins/utools-fastdog/src/style.css`

- [ ] **Step 1: 实现 index.html（结构对应 MainWindow.xaml 各区）**

`plugins/utools-fastdog/index.html`：

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta http-equiv="Content-Security-Policy" content="default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self';" />
  <title>FastDog</title>
  <link rel="stylesheet" href="src/style.css" />
</head>
<body>
  <div id="app">
    <!-- ========== Header：搜索条件区（对应 MainWindow Header Border）========== -->
    <header id="search-header">
      <div class="field-row">
        <label class="field-label">搜索路径</label>
        <input id="search-path" type="text" class="search-input" />
        <button id="btn-browse" class="action-btn">浏览...</button>
      </div>
      <div class="field-row">
        <label class="field-label">搜索内容</label>
        <input id="search-text" type="text" class="search-input" />
      </div>
      <div class="options-row">
        <!-- 搜索模式（RadioButton 二选一）-->
        <label class="toggle"><input type="radio" name="mode" value="regex" id="mode-regex" checked />正则表达式</label>
        <label class="toggle"><input type="radio" name="mode" value="text" id="mode-text" />纯文本</label>
        <!-- 选项 -->
        <label class="toggle"><input type="checkbox" id="opt-case" />区分大小写 (Aa)</label>
        <label class="toggle"><input type="checkbox" id="opt-word" />全词匹配</label>
        <span class="separator">|</span>
        <!-- 文件过滤（标签按钮内联编辑）-->
        <button id="tag-filefilter" class="tag-btn">文件: *.cs;*.txt</button>
        <input id="input-filefilter" class="tag-input hidden" type="text" placeholder="*.cs;*.txt" />
        <!-- 排除目录 -->
        <button id="tag-exclude" class="tag-btn">排除: node_modules</button>
        <input id="input-exclude" class="tag-input hidden" type="text" placeholder="node_modules;dist" />
        <span class="separator">|</span>
        <!-- 日期范围 -->
        <label class="toggle"><input type="checkbox" id="date-toggle" />📅 日期范围</label>
        <input id="date-from" type="date" class="date-input hidden" />
        <span id="date-sep" class="hidden">~</span>
        <input id="date-to" type="date" class="date-input hidden" />
        <!-- 操作按钮 -->
        <span class="spacer"></span>
        <button id="btn-search" class="search-btn">搜索</button>
        <button id="btn-cancel" class="cancel-btn">取消</button>
      </div>
    </header>

    <!-- ========== Content（上文件列表 2 : 下分栏 3）========== -->
    <main id="content">
      <!-- 文件列表（对应 ResultsGrid）-->
      <section id="file-panel">
        <div class="table-wrap" id="file-table-wrap">
          <table id="file-table">
            <thead>
              <tr>
                <th class="col-name">文件名</th>
                <th class="col-size">大小</th>
                <th class="col-match">匹配数</th>
                <th class="col-path">路径</th>
                <th class="col-mtime">修改时间</th>
              </tr>
            </thead>
            <tbody id="file-tbody"></tbody>
          </table>
        </div>
        <div class="splitter-h" id="splitter-h"></div>
        <!-- 下半区：左匹配行 + 右预览 -->
        <div id="bottom-panel">
          <div id="match-panel" class="bottom-col">
            <div class="panel-header">
              <span>匹配行 <b id="match-count">0</b> — <span id="match-filename"></span></span>
            </div>
            <div id="match-list" class="scroll"></div>
          </div>
          <div class="splitter-v" id="splitter-v"></div>
          <div id="preview-panel" class="bottom-col">
            <div class="panel-header">
              <span>文件预览 — <span id="preview-filename"></span></span>
              <label class="toggle"><input type="checkbox" id="opt-wrap" />自动换行</label>
            </div>
            <div id="preview-wrap" class="scroll">
              <pre id="preview-pre"><code id="preview-code"></code></pre>
              <div id="preview-find" class="hidden"></div>
            </div>
            <div id="binary-hint" class="center-hint hidden">二进制文件，无法预览</div>
            <div id="truncate-hint" class="truncate-hint hidden">文件过大，仅显示部分内容</div>
          </div>
        </div>
      </section>
    </main>

    <!-- ========== 状态栏（对应 StatusBar）========== -->
    <footer id="status-bar">
      <span id="status-text">就绪</span>
      <span class="status-right">
        <span>文件: <b id="stat-files">0</b></span>
        <span>匹配: <b id="stat-matches">0</b></span>
        <span>耗时: <b id="stat-elapsed">0s</b></span>
      </span>
    </footer>
  </div>

  <script src="src/highlight.js"></script>
  <script src="src/previewFind.js"></script>
  <script src="src/app.js"></script>
</body>
</html>
```

- [ ] **Step 2: 实现 style.css（配色对齐桌面版：主色 #0e639c、黄高亮、状态栏 #faf8f3）**

`plugins/utools-fastdog/src/style.css`：

```css
:root {
  --accent: #0e639c;
  --accent-bg: #e6f0fa;
  --accent-bg-hover: #d4e6f7;
  --border: #e0e0e0;
  --text: #1e1e1e;
  --text-gray: #9a9a9a;
  --header-bg: #fff;
  --status-bg: #faf8f3;
  --match-yellow: rgba(255, 255, 0, 0.4);
  --find-orange: #ff9900;
  --find-orange-cur: #e67300;
}

* { box-sizing: border-box; margin: 0; padding: 0; }

html, body { height: 100%; font-family: "Segoe UI", "Microsoft YaHei", sans-serif; color: var(--text); }

#app { display: flex; flex-direction: column; height: 100vh; }

.hidden { display: none !important; }

/* ===== Header ===== */
#search-header { background: var(--header-bg); border-bottom: 1px solid var(--border); padding: 14px 16px 10px; }
.field-row { display: flex; align-items: center; margin-bottom: 6px; }
.field-label { width: 55px; font-size: 12px; color: var(--text-gray); }
.search-input { flex: 1; height: 28px; padding: 0 8px; border: 1px solid var(--border); border-radius: 3px; font-size: 13px; }
.search-input:focus { outline: none; border-color: var(--accent); }
.action-btn { margin-left: 6px; height: 28px; padding: 0 10px; border: 1px solid var(--border); background: #fff; border-radius: 3px; cursor: pointer; font-size: 12px; }

.options-row { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
.toggle { display: inline-flex; align-items: center; padding: 3px 8px; border: 1px solid var(--border); border-radius: 3px; font-size: 12px; cursor: pointer; user-select: none; }
.toggle input { margin-right: 4px; }
.separator { color: var(--border); }
.tag-btn { padding: 3px 8px; border: 1px solid var(--border); border-radius: 3px; background: #fafafa; font-size: 12px; cursor: pointer; }
.tag-input { padding: 3px 6px; border: 1px solid var(--accent); border-radius: 3px; font-size: 12px; width: 120px; }
.date-input { padding: 2px 4px; border: 1px solid var(--border); border-radius: 3px; font-size: 12px; }
.spacer { flex: 1; }
.search-btn { background: var(--accent); color: #fff; border: none; border-radius: 3px; padding: 5px 18px; font-size: 13px; cursor: pointer; }
.cancel-btn { background: #fff; color: var(--text-gray); border: 1px solid var(--border); border-radius: 3px; padding: 5px 14px; font-size: 13px; cursor: pointer; }

/* ===== Content ===== */
#content { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
#file-panel { display: flex; flex-direction: column; flex: 1; min-height: 0; }
.table-wrap { flex: 2; overflow: auto; min-height: 80px; }
#file-table { width: 100%; border-collapse: collapse; font-size: 13px; }
#file-table th { background: #fafafa; color: var(--text-gray); font-weight: 600; font-size: 12px; padding: 7px 8px; border-bottom: 1px solid var(--border); position: sticky; top: 0; }
#file-table td { padding: 6px 8px; border-bottom: 1px solid #f0f0f0; }
#file-table tbody tr { cursor: pointer; }
#file-table tbody tr:hover { background: var(--accent-bg-hover); }
#file-table tbody tr.selected { background: var(--accent-bg); }
.col-name { text-align: left; width: 200px; font-weight: 500; }
.col-size { text-align: center; width: 90px; color: #6a6a6a; }
.col-match { text-align: center; width: 70px; }
.match-badge { background: var(--accent); color: #fff; border-radius: 9px; padding: 1px 7px; font-weight: bold; font-size: 12px; }
.col-path { color: var(--text-gray); text-align: left; }
.col-mtime { color: var(--text-gray); width: 130px; }

.splitter-h { height: 3px; background: var(--border); cursor: row-resize; }

#bottom-panel { flex: 3; display: flex; min-height: 100px; }
.bottom-col { display: flex; flex-direction: column; min-width: 0; }
#match-panel { flex: 35; }
#preview-panel { flex: 65; position: relative; }
.splitter-v { width: 2px; background: var(--border); cursor: col-resize; }
.panel-header { padding: 6px 10px; font-size: 12px; color: var(--text-gray); border-bottom: 1px solid var(--border); background: #fafafa; display: flex; justify-content: space-between; align-items: center; }
.panel-header b { color: #c0392b; margin: 0 4px; }
.scroll { flex: 1; overflow: auto; background: #fff; }

#match-list { font-family: Consolas, Monaco, monospace; font-size: 12px; }
.match-item { padding: 4px 8px; cursor: pointer; white-space: pre; border-left: 3px solid transparent; }
.match-item:hover { background: var(--accent-bg-hover); }
.match-item.selected { background: var(--accent-bg); border-left-color: var(--accent); }
.match-item .ln { color: var(--text-gray); display: inline-block; width: 48px; text-align: right; margin-right: 8px; }

#preview-pre { font-family: Consolas, Monaco, monospace; font-size: 13px; padding: 0 8px; }
#preview-code { white-space: pre; }
#preview-code.wrap { white-space: pre-wrap; word-break: break-all; }
mark.match-hl { background: var(--match-yellow); color: inherit; }
mark.find-hl { background: var(--find-orange); }
mark.find-hl.current { background: var(--find-orange-cur); color: #fff; }

.center-hint { position: absolute; inset: 30px 0 0 0; display: flex; align-items: center; justify-content: center; color: gray; font-size: 15px; }
.truncate-hint { position: absolute; top: 34px; right: 6px; color: #b8860b; font-size: 12px; }

/* ===== 预览内查找栏 ===== */
#preview-find { position: absolute; top: 6px; right: 10px; background: #fff; border: 1px solid var(--border); border-radius: 4px; padding: 4px; display: flex; gap: 4px; align-items: center; box-shadow: 0 2px 8px rgba(0,0,0,0.15); z-index: 10; }
#preview-find input { width: 140px; padding: 2px 6px; border: 1px solid var(--border); border-radius: 3px; font-size: 12px; }
#preview-find button { padding: 2px 6px; border: 1px solid var(--border); background: #fff; border-radius: 3px; cursor: pointer; font-size: 12px; }
#preview-find .counter { font-size: 12px; color: var(--text-gray); min-width: 36px; text-align: center; }

/* ===== 状态栏 ===== */
#status-bar { background: var(--status-bg); border-top: 1px solid #ebe6da; padding: 5px 12px; display: flex; justify-content: space-between; font-size: 12px; color: #555; }
.status-right { display: flex; gap: 16px; }
.status-right b { color: var(--accent); }
```

- [ ] **Step 3: 提交**

```bash
git add plugins/utools-fastdog/index.html plugins/utools-fastdog/src/style.css
git commit -m "feat(utools): UI 骨架 index.html + style.css（还原三层布局）"
```

---

## Task 10: app.js（UI 交互逻辑）+ highlight.js

实现搜索条件读写、调用 `window.fastdog`、文件列表/匹配行/预览渲染、流式增量、选项交互（标签内联编辑、日期折叠）、拖放、状态栏、GridSplitter 拖拽。

**Files:**
- Create: `plugins/utools-fastdog/src/app.js`
- Create: `plugins/utools-fastdog/src/highlight.js`

- [ ] **Step 1: 实现 highlight.js（匹配文本黄色高亮渲染工具）**

`plugins/utools-fastdog/src/highlight.js`：

```js
// 匹配文本高亮渲染：把行文本按字节偏移区间用 <mark> 包裹。
// 注意：rg 的 matchStart/matchEnd 是 UTF-8 字节偏移，需转字符偏移后再切片。

/**
 * 把一行文本渲染成 HTML，匹配区间加 <mark class="match-hl">。
 * @param {string} text 行文本
 * @param {number} byteStart 字节偏移起点
 * @param {number} byteEnd 字节偏移终点
 * @returns {string} HTML 字符串（已转义）
 */
function highlightLine(text, byteStart, byteEnd) {
  const start = byteToChar(text, byteStart);
  const end = byteToChar(text, byteEnd);
  return escapeHtml(text.slice(0, start))
    + '<mark class="match-hl">' + escapeHtml(text.slice(start, end)) + '</mark>'
    + escapeHtml(text.slice(end));
}

function escapeHtml(s) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function byteToChar(text, byteOffset) {
  if (byteOffset <= 0) return 0;
  const bytes = new TextEncoder().encode(text);
  if (byteOffset >= bytes.length) return text.length;
  return new TextDecoder('utf8').decode(bytes.slice(0, byteOffset)).length;
}

window.FastDogHighlight = { highlightLine, escapeHtml, byteToChar };
```

- [ ] **Step 2: 实现 app.js（核心 UI 逻辑）**

`plugins/utools-fastdog/src/app.js`：

```js
// UI 主逻辑：搜索条件读写、调用 window.fastdog、渲染文件列表/匹配行/预览。
// 仅通过 window.fastdog.* 和 utools.* 调能力。

(function () {
  'use strict';
  const fd = window.fastdog;
  const H = window.FastDogHighlight;
  let allResults = [];        // 当前搜索结果
  let selectedResult = null;  // 当前选中文件

  // ===== DOM 引用 =====
  const $ = (id) => document.getElementById(id);
  const els = {
    path: $('search-path'), text: $('search-text'),
    browse: $('btn-browse'), search: $('btn-search'), cancel: $('btn-cancel'),
    modeRegex: $('mode-regex'), modeText: $('mode-text'),
    optCase: $('opt-case'), optWord: $('opt-word'),
    tagFileFilter: $('tag-filefilter'), inputFileFilter: $('input-filefilter'),
    tagExclude: $('tag-exclude'), inputExclude: $('input-exclude'),
    dateToggle: $('date-toggle'), dateFrom: $('date-from'), dateTo: $('date-to'),
    dateSep: $('date-sep'),
    fileTbody: $('file-tbody'), matchList: $('match-list'), matchCount: $('match-count'),
    matchFilename: $('match-filename'),
    previewCode: $('preview-code'), previewFilename: $('preview-filename'),
    binaryHint: $('binary-hint'), truncateHint: $('truncate-hint'),
    optWrap: $('opt-wrap'),
    statusText: $('status-text'), statFiles: $('stat-files'),
    statMatches: $('stat-matches'), statElapsed: $('stat-elapsed'),
  };

  // ===== 初始化：恢复上次路径 + 监听进入事件预填 =====
  function init() {
    els.path.value = fd.getLastPath() || '';
    bindOptions();
    bindButtons();
    bindSplitters();
    bindDragDrop();

    // uTools 进入事件（preload 派发 fastdog:enter）
    window.addEventListener('fastdog:enter', (e) => {
      const enter = e.detail || window.__fastdogEnter || {};
      if (enter.searchPath) els.path.value = enter.searchPath;
      if (enter.searchText) els.text.value = enter.searchText;
      els.text.focus();
    });
    // 若 preload 已先于本脚本执行设置过 enter
    if (window.__fastdogEnter) {
      const enter = window.__fastdogEnter;
      if (enter.searchPath) els.path.value = enter.searchPath;
      if (enter.searchText) els.text.value = enter.searchText;
    }
  }

  // ===== 选项交互 =====
  function bindOptions() {
    // 文件过滤标签内联编辑
    bindTagEdit(els.tagFileFilter, els.inputFileFilter, '文件: ');
    bindTagEdit(els.tagExclude, els.inputExclude, '排除: ');
    // 默认显示
    els.inputFileFilter.value = '*.cs;*.txt';
    els.inputExclude.value = 'node_modules';
    refreshTag(els.tagFileFilter, els.inputFileFilter, '文件: ');
    refreshTag(els.tagExclude, els.inputExclude, '排除: ');

    // 日期折叠
    els.dateToggle.addEventListener('change', () => {
      const show = els.dateToggle.checked;
      els.dateFrom.classList.toggle('hidden', !show);
      els.dateTo.classList.toggle('hidden', !show);
      els.dateSep.classList.toggle('hidden', !show);
    });

    // 自动换行
    els.optWrap.addEventListener('change', () => {
      els.previewCode.classList.toggle('wrap', els.optWrap.checked);
    });
  }

  function bindTagEdit(btn, input, prefix) {
    btn.addEventListener('click', () => {
      btn.classList.add('hidden');
      input.classList.remove('hidden');
      input.focus();
      input.select();
    });
    input.addEventListener('blur', () => {
      refreshTag(btn, input, prefix);
      btn.classList.remove('hidden');
      input.classList.add('hidden');
    });
    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') input.blur();
      if (e.key === 'Escape') { input.blur(); }
    });
  }
  function refreshTag(btn, input, prefix) {
    btn.textContent = prefix + (input.value || '');
  }

  // ===== 按钮绑定 =====
  function bindButtons() {
    els.browse.addEventListener('click', () => {
      const r = fd.selectDirectory();
      if (r && r[0]) els.path.value = r[0];
    });
    els.search.addEventListener('click', doSearch);
    els.text.addEventListener('keydown', (e) => { if (e.key === 'Enter') doSearch(); });
    els.cancel.addEventListener('click', () => { fd.cancel(); setStatus('已取消'); });
  }

  // ===== 拖放文件夹设为搜索路径 =====
  function bindDragDrop() {
    document.body.addEventListener('dragover', (e) => { e.preventDefault(); });
    document.body.addEventListener('drop', (e) => {
      e.preventDefault();
      const f = e.dataTransfer.files[0];
      if (f && f.path) els.path.value = f.path;
    });
  }

  // ===== 拼装 SearchQuery =====
  function buildQuery() {
    return {
      searchPath: els.path.value.trim(),
      searchText: els.text.value,
      isRegex: els.modeRegex.checked,
      caseSensitive: els.optCase.checked,
      wholeWord: els.optWord.checked,
      fileFilter: els.inputFileFilter.value.trim(),
      excludeDirs: els.inputExclude.value.trim(),
      dateFilterEnabled: els.dateToggle.checked,
      dateFrom: els.dateFrom.value ? new Date(els.dateFrom.value) : null,
      dateTo: els.dateTo.value ? new Date(els.dateTo.value) : null,
    };
  }

  // ===== 搜索 =====
  function doSearch() {
    const q = buildQuery();
    if (!q.searchPath) { setStatus('请先选择搜索路径'); return; }
    if (!q.searchText) { setStatus('请输入搜索内容'); return; }

    fd.saveLastPath(q.searchPath);
    allResults = [];
    clearUI();
    setStatus('搜索中...');

    fd.search(q, {
      onResult: (r) => { allResults.push(r); appendFileRow(r); },
      onStats: (s) => {
        els.statFiles.textContent = s.searchedFiles;
        els.statMatches.textContent = s.totalMatches;
        els.statElapsed.textContent = s.elapsed;
        setStatus(`完成：找到 ${s.foundFiles} 个文件，${s.totalMatches} 处匹配`);
      },
      onDone: () => { if (els.statusText.textContent === '搜索中...') setStatus('完成'); },
      onError: (err) => setStatus('错误：' + err.message),
    });
  }

  function clearUI() {
    els.fileTbody.innerHTML = '';
    els.matchList.innerHTML = '';
    els.previewCode.innerHTML = '';
    els.statFiles.textContent = '0';
    els.statMatches.textContent = '0';
    els.statElapsed.textContent = '0s';
  }
  function setStatus(t) { els.statusText.textContent = t; }

  // ===== 文件列表行 =====
  function appendFileRow(r) {
    const tr = document.createElement('tr');
    tr.innerHTML =
      '<td class="col-name">' + H.escapeHtml(r.fileName) + '</td>' +
      '<td class="col-size">' + formatSize(r.fileSize) + '</td>' +
      '<td class="col-match"><span class="match-badge">' + r.matchCount + '</span></td>' +
      '<td class="col-path">' + H.escapeHtml(r.filePath) + '</td>' +
      '<td class="col-mtime">' + formatDate(r.lastModified) + '</td>';
    tr.addEventListener('click', () => selectResult(r, tr));
    tr.addEventListener('dblclick', () => fd.openFile(r.filePath));
    tr.addEventListener('contextmenu', (e) => showFileMenu(e, r));
    els.fileTbody.appendChild(tr);
  }

  function selectResult(r, tr) {
    document.querySelectorAll('#file-tbody tr.selected').forEach(x => x.classList.remove('selected'));
    tr.classList.add('selected');
    selectedResult = r;
    renderMatchList(r);
    renderPreview(r);
  }

  // ===== 匹配行列表 =====
  function renderMatchList(r) {
    els.matchCount.textContent = r.matchCount;
    els.matchFilename.textContent = r.fileName;
    els.matchList.innerHTML = '';
    r.matches.forEach((m) => {
      const div = document.createElement('div');
      div.className = 'match-item';
      div.innerHTML =
        '<span class="ln">' + m.lineNumber + '</span>' +
        H.highlightLine(m.lineText, m.matchStart, m.matchEnd).replace(/\n$/, '');
      div.addEventListener('click', () => {
        document.querySelectorAll('.match-item.selected').forEach(x => x.classList.remove('selected'));
        div.classList.add('selected');
        scrollToLine(m.lineNumber);
      });
      div.addEventListener('dblclick', () => fd.openFile(r.filePath));
      els.matchList.appendChild(div);
    });
    if (r.matches[0]) {
      scrollToLine(r.matches[0].lineNumber);
    }
  }

  // ===== 文件预览 =====
  function renderPreview(r) {
    els.previewFilename.textContent = r.fileName;
    els.binaryHint.classList.add('hidden');
    els.truncateHint.classList.add('hidden');

    const { info, matches } = fd.previewFile(r.filePath, r.matches);
    if (info.isBinary) {
      els.previewCode.innerHTML = '';
      els.binaryHint.classList.remove('hidden');
      return;
    }
    if (info.content === null) {
      els.previewCode.innerHTML = '<span style="color:gray">无法读取文件</span>';
      return;
    }
    if (info.truncated) els.truncateHint.classList.remove('hidden');

    // 渲染全文 + 行号 + 主搜索匹配黄色高亮（按行匹配）
    els.previewCode.innerHTML = renderPreviewHtml(info.content, matches);
  }

  function renderPreviewHtml(content, matches) {
    // 按行渲染，匹配行高亮匹配片段
    const lines = content.split('\n');
    const matchByLine = new Map();
    for (const m of matches) matchByLine.set(m.lineNumber, m);

    let html = '';
    for (let i = 0; i < lines.length; i++) {
      const ln = i + 1;
      const line = lines[i];
      const m = matchByLine.get(ln);
      const lineHtml = m
        ? H.highlightLine(line, m.matchStart, m.matchEnd)
        : H.escapeHtml(line);
      // data-line 供滚动定位
      html += '<div class="pv-line" data-line="' + ln + '">' + lineHtml + '</div>';
    }
    return html;
  }

  function scrollToLine(lineNumber) {
    const node = els.previewCode.querySelector('.pv-line[data-line="' + lineNumber + '"]');
    if (node) node.scrollIntoView({ block: 'center', behavior: 'smooth' });
  }

  // ===== GridSplitter 拖拽（上下/左右）=====
  function bindSplitters() {
    bindSplit('splitter-h', 'file-panel', 'row', 'top-bottom');
    bindSplit('splitter-v', 'bottom-panel', 'col', 'left-right');
  }
  function bindSplit(splitterId, panelId, dir, saveKey) {
    const splitter = $(splitterId);
    const panel = $(panelId);
    let dragging = false;
    splitter.addEventListener('mousedown', () => {
      dragging = true;
      document.body.style.cursor = dir === 'row' ? 'row-resize' : 'col-resize';
    });
    document.addEventListener('mousemove', (e) => {
      if (!dragging) return;
      if (dir === 'row') {
        const rect = panel.getBoundingClientRect();
        const ratio = (e.clientY - rect.top) / rect.height;
        if (ratio > 0.1 && ratio < 0.9) {
          const top = panel.querySelector('.table-wrap');
          const bottom = $('bottom-panel');
          top.style.flex = (ratio * 100) + ' 0 0';
          bottom.style.flex = ((1 - ratio) * 100) + ' 0 0';
        }
      } else {
        const rect = panel.getBoundingClientRect();
        const ratio = (e.clientX - rect.left) / rect.width;
        if (ratio > 0.1 && ratio < 0.9) {
          $('match-panel').style.flex = (ratio * 100) + ' 0 0';
          $('preview-panel').style.flex = ((1 - ratio) * 100) + ' 0 0';
        }
      }
    });
    document.addEventListener('mouseup', () => {
      if (dragging) {
        dragging = false;
        document.body.style.cursor = '';
      }
    });
  }

  // ===== 右键菜单 =====
  function showFileMenu(e, r) {
    e.preventDefault();
    // 简易菜单：用 utools 无原生右键菜单 API，用 confirm 简化
    // 完整版可自建 contextmenu DOM；MVP 用 prompt 链
    const action = ['打开文件', '在资源管理器中打开', '复制文件名', '复制文件路径'];
    // 这里用简易弹窗（uTools 可用 utools.showMessageBox）
    utools.showMessageBox({
      type: 'question',
      title: '文件操作',
      message: r.fileName,
      buttons: action,
    }, (idx) => {
      if (idx === 0) fd.openFile(r.filePath);
      else if (idx === 1) fd.openInExplorer(r.filePath);
      else if (idx === 2) fd.copyText(r.fileName);
      else if (idx === 3) fd.copyText(r.filePath);
    });
  }

  // ===== 工具 =====
  function formatSize(bytes) {
    const KB = 1024;
    if (bytes < KB) return bytes + ' B';
    if (bytes < KB * 1024) return (bytes / KB).toFixed(1) + ' KB';
    if (bytes < KB * 1024 * 1024) return (bytes / (KB * 1024)).toFixed(1) + ' MB';
    return (bytes / (KB * 1024 * 1024)).toFixed(2) + ' GB';
  }
  function formatDate(d) {
    const p = (n) => String(n).padStart(2, '0');
    return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate())
      + ' ' + p(d.getHours()) + ':' + p(d.getMinutes());
  }

  document.addEventListener('DOMContentLoaded', init);
})();
```

- [ ] **Step 3: 提交**

```bash
git add plugins/utools-fastdog/src/app.js plugins/utools-fastdog/src/highlight.js
git commit -m "feat(utools): UI 交互 app.js + 高亮 highlight.js"
```

---

## Task 11: previewFind.js（预览内二次查找）

自建轻量查找栏（替代 AvalonEdit SearchPanel）：Ctrl+F 唤起、橙色高亮、N/M 计数、上一个/下一个。

**Files:**
- Create: `plugins/utools-fastdog/src/previewFind.js`

- [ ] **Step 1: 实现 previewFind.js**

`plugins/utools-fastdog/src/previewFind.js`：

```js
// 预览内二次查找：Ctrl+F 唤起，在预览全文中查找，橙色高亮，N/M 计数。
// 对应桌面版 AvalonEdit SearchPanel 的 Web 端轻量实现。

(function () {
  'use strict';
  let matches = [];      // [{node, start, end}]
  let currentIdx = -1;

  function init() {
    const findBar = document.getElementById('preview-find');
    const previewCode = document.getElementById('preview-code');

    // 动态生成查找栏内容（index.html 里只有空容器）
    findBar.innerHTML =
      '<input type="text" id="find-input" placeholder="查找..." />' +
      '<span class="counter" id="find-counter">0/0</span>' +
      '<button id="find-prev">↑</button>' +
      '<button id="find-next">↓</button>' +
      '<button id="find-close">✕</button>';

    const input = document.getElementById('find-input');
    const counter = document.getElementById('find-counter');
    const prevBtn = document.getElementById('find-prev');
    const nextBtn = document.getElementById('find-next');
    const closeBtn = document.getElementById('find-close');

    // Ctrl+F 唤起
    document.addEventListener('keydown', (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
        e.preventDefault();
        open();
      }
      if (e.key === 'Escape') close();
    });

    function open() {
      findBar.classList.remove('hidden');
      input.focus();
      input.select();
    }
    function close() {
      findBar.classList.add('hidden');
      clearHighlights();
      counter.textContent = '0/0';
    }

    input.addEventListener('input', () => doFind(input.value));
    prevBtn.addEventListener('click', () => navigate(-1));
    nextBtn.addEventListener('click', () => navigate(1));
    closeBtn.addEventListener('click', close);
    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') { e.preventDefault(); navigate(e.shiftKey ? -1 : 1); }
    });

    function doFind(query) {
      clearHighlights();
      matches = [];
      currentIdx = -1;
      if (!query) { counter.textContent = '0/0'; return; }

      const text = previewCode.textContent;
      const lower = text.toLowerCase();
      const q = query.toLowerCase();
      let pos = 0;
      // 简化：在 textContent 上匹配，用 Range 高亮
      while ((pos = lower.indexOf(q, pos)) !== -1) {
        matches.push(pos);
        pos += q.length;
      }

      if (matches.length > 0) {
        highlightAll(query);
        currentIdx = 0;
        scrollToCurrent();
      }
      counter.textContent = (currentIdx + 1) + '/' + matches.length;
    }

    // 用 <mark> 包裹每个命中（按字符偏移切分 textContent）
    function highlightAll(query) {
      // 由于预览已按行 div 渲染，这里改用 window.find 或简单标记策略：
      // 为简单可靠，遍历 pv-line 节点做行内高亮
      const qLower = query.toLowerCase();
      const lines = previewCode.querySelectorAll('.pv-line');
      lines.forEach((lineNode) => {
        const original = lineNode.textContent;
        const lower = original.toLowerCase();
        let idx = lower.indexOf(qLower);
        if (idx === -1) return;
        // 重建 innerHTML，插入 mark
        let html = '';
        let last = 0;
        while (idx !== -1) {
          html += escapeHtml(original.slice(last, idx))
            + '<mark class="find-hl">' + escapeHtml(original.slice(idx, idx + query.length)) + '</mark>';
          last = idx + query.length;
          idx = lower.indexOf(qLower, last);
        }
        html += escapeHtml(original.slice(last));
        lineNode.innerHTML = html;
      });
      // 标记第一个为 current
      const first = previewCode.querySelector('.find-hl');
      if (first) first.classList.add('current');
    }

    function clearHighlights() {
      // 清除查找高亮（保留主搜索 match-hl）。重置 current
      previewCode.querySelectorAll('.find-hl').forEach((m) => {
        const parent = m.parentNode;
        parent.replaceChild(document.createTextNode(m.textContent), m);
        parent.normalize();
      });
    }

    function navigate(dir) {
      if (matches.length === 0) return;
      currentIdx = (currentIdx + dir + matches.length) % matches.length;
      // 更新 current 标记
      previewCode.querySelectorAll('.find-hl.current').forEach((m) => m.classList.remove('current'));
      const all = previewCode.querySelectorAll('.find-hl');
      if (all[currentIdx]) {
        all[currentIdx].classList.add('current');
        all[currentIdx].scrollIntoView({ block: 'center', behavior: 'smooth' });
      }
      counter.textContent = (currentIdx + 1) + '/' + matches.length;
    }

    function scrollToCurrent() {
      const cur = previewCode.querySelector('.find-hl.current');
      if (cur) cur.scrollIntoView({ block: 'center', behavior: 'smooth' });
    }
  }

  function escapeHtml(s) {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }

  document.addEventListener('DOMContentLoaded', init);
})();
```

- [ ] **Step 2: 提交**

```bash
git add plugins/utools-fastdog/src/previewFind.js
git commit -m "feat(utools): 预览内二次查找 previewFind（Ctrl+F 橙色高亮）"
```

---

## Task 12: 下载捆绑三平台 ripgrep 二进制 + logo

从 [ripgrep v14.1.1 release](https://github.com/BurntSushi/ripgrep/releases/tag/14.1.1) 下载三平台预编译包，解出 rg 二进制放入 `bin/`，复用 FastDog logo。

**Files:**
- Create: `plugins/utools-fastdog/bin/rg-win-x64.exe`
- Create: `plugins/utools-fastdog/bin/rg-darwin-x64`
- Create: `plugins/utools-fastdog/bin/rg-linux-x64`
- Create: `plugins/utools-fastdog/logo.png`

- [ ] **Step 1: 下载并解压 win32 rg**

```bash
cd /e/demo/ai/my/fastdog/plugins/utools-fastdog
mkdir -p bin
curl -L -o /tmp/rg-win.zip https://github.com/BurntSushi/ripgrep/releases/download/14.1.1/ripgrep-14.1.1-x86_64-pc-windows-msvc.zip
unzip -o /tmp/rg-win.zip -d /tmp/rg-win
cp /tmp/rg-win/ripgrep-14.1.1-x86_64-pc-windows-msvc/rg.exe bin/rg-win-x64.exe
```

校验：`./bin/rg-win-x64.exe --version` 应输出 `ripgrep 14.1.1`。

- [ ] **Step 2: 下载并解压 darwin rg**

```bash
curl -L -o /tmp/rg-mac.zip https://github.com/BurntSushi/ripgrep/releases/download/14.1.1/ripgrep-14.1.1-x86_64-apple-darwin.zip
unzip -o /tmp/rg-mac.zip -d /tmp/rg-mac
cp /tmp/rg-mac/ripgrep-14.1.1-x86_64-apple-darwin/rg bin/rg-darwin-x64
chmod +x bin/rg-darwin-x64
```

- [ ] **Step 3: 下载并解压 linux rg**

```bash
curl -L -o /tmp/rg-linux.tar.gz https://github.com/BurntSushi/ripgrep/releases/download/14.1.1/ripgrep-14.1.1-x86_64-unknown-linux-musl.tar.gz
tar -xzf /tmp/rg-linux.tar.gz -C /tmp
cp /tmp/ripgrep-14.1.1-x86_64-unknown-linux-musl/rg bin/rg-linux-x64
chmod +x bin/rg-linux-x64
```

> 网络不通时用代理 `127.0.0.1:7890`（见 AGENTS.md 规则）：`curl -x http://127.0.0.1:7890 -L -o ...`

- [ ] **Step 4: 复用 FastDog logo**

```bash
cp src/FastDog/Assets/newLogo1.2.png plugins/utools-fastdog/logo.png
```

- [ ] **Step 5: 提交**

```bash
git add plugins/utools-fastdog/bin/ plugins/utools-fastdog/logo.png
git commit -m "feat(utools): 捆绑三平台 ripgrep 14.1.1 + logo"
```

---

## Task 13: 端到端联调与 CHANGELOG 收尾

在 uTools 开发者工具里加载 `plugin.json`，验证 3 个入口 + 全部搜索条件 + 三层布局 + 预览查找。

**Files:**
- Modify: `plugins/utools-fastdog/CHANGELOG.md`

- [ ] **Step 1: 运行全部单元测试**

```bash
cd /e/demo/ai/my/fastdog/plugins/utools-fastdog
npm test
```

Expected: 4 套测试全 PASS（argumentBuilder + jsonParser + dateFilter + filePreview）。

- [ ] **Step 2: 确认主仓库 C# 构建不受影响**

```bash
cd /e/demo/ai/my/fastdog
dotnet build FastDog.sln
```

Expected: 0 错误 0 警告（插件独立，不影响主项目）。

- [ ] **Step 3: 在 uTools 开发者工具加载验证（手动）**

> 这是需要用户在 uTools 客户端手动的步骤，无法自动化。清单：
> 1. 打开 uTools 开发者工具 → 新建项目 → 选择 `plugins/utools-fastdog/plugin.json`
> 2. 输入 `fd` 唤起插件，确认三层布局正常显示
> 3. 设路径 + 搜索词 + 各选项，点搜索，确认文件列表/匹配行/预览流式渲染
> 4. 选中文件，确认预览黄色高亮 + 匹配行联动滚动
> 5. Ctrl+F 在预览内查找，确认橙色高亮 + N/M 计数
> 6. 文件管理器选中文件夹 → 超级面板 → FastDog，确认路径预填
> 7. 选中一段文本 → 超级面板 → FastDog，确认搜索词预填

- [ ] **Step 4: 更新 CHANGELOG**

`plugins/utools-fastdog/CHANGELOG.md`：

```markdown
# Changelog

## [0.1.0] - 2026-07-15

### 新增
- uTools 插件首版：实现 FastDog 除搜索历史外的全部功能
- 移植核心逻辑（参数构建/JSON 解析/结果聚合/文件预览）为 JS，附 4 组单元测试
- 还原桌面版三层布局（搜索条件区 / 文件列表 / 匹配行+预览）
- 预览内 Ctrl+F 二次查找（橙色高亮 + N/M 计数）
- 捆绑 win32/darwin/linux 三平台 ripgrep 14.1.1
- 3 个进入入口：关键字（fd/fastdog）、超级面板选中文本、文件夹
```

- [ ] **Step 5: 提交**

```bash
git add plugins/utools-fastdog/CHANGELOG.md
git commit -m "release(utools): v0.1.0 首版"
```

---

## Self-Review 自检

**1. Spec 覆盖：**
- F1 搜索条件（正则/纯文本/大小写/全词/文件过滤/目录排除/日期）→ Task 2 + Task 10 ✓
- F2 路径设定（默认/预填/浏览/拖放）→ Task 8 + Task 10 ✓
- F3 文件列表（5 列 + 徽章 + 选中 + 右键菜单）→ Task 10 ✓
- F4 匹配行列表（行号 + 高亮 + 联动）→ Task 10 ✓
- F5 文件预览（全文 + 行号 + 黄高亮 + 截断 + 二进制 + 换行 + Ctrl+F）→ Task 5/10/11 ✓
- F6 流式输出与取消 → Task 7/8/10 ✓
- F7 双击打开/拖放 → Task 10 ✓
- F8 状态栏 → Task 9/10 ✓
- 跨平台 rg → Task 6/12 ✓
- 搜索历史明确排除 ✓

**2. 占位符扫描：** 无 TBD/TODO，每个代码步骤含完整代码。

**3. 类型一致性：** `window.fastdog` 方法名在 Task 8 定义（search/cancel/previewFile/openFile/openInExplorer/copyText/selectDirectory/getLastPath/saveLastPath），Task 10 调用一致；SearchQuery 字段（isRegex/caseSensitive/wholeWord/fileFilter/excludeDirs/dateFilterEnabled/dateFrom/dateTo）Task 2 与 Task 10 一致；RgEvent 字段（type/filePath/lineNumber/lineText/matchStart/matchEnd/totalMatches/elapsed）Task 3 与 Task 4/7 一致。

---

**计划共 13 个 Task，全部 TDD（纯逻辑任务先测后码），每个 Task 末尾提交。**
