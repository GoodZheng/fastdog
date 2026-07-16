// 纯 JS 搜索引擎（Worker 多线程版），替代 ripgrepBridge（去二进制以通过 uTools 审核）。
// 接口契约：search(query, handlers) / countFiles(query) / cancel()。
// 主线程负责目录遍历（gitignore/excludeDirs/fileFilter 过滤），文件列表分发给多个 Worker 并行搜索。
// Worker 回传 {filePath, matches[]}，主线程转成 match 事件推给 searchService。
//
// matchStart/matchEnd 为字符偏移（直接用 RegExp 的 m.index），上层 highlight/filePreview 直接 slice。
// 已废除 ripgrep 时代的字节偏移契约（纯 JS 引擎无 rg，无需字节↔字符转换）。
// Worker 不可用（uTools 环境限制）时，走串行 searchFileSync + setImmediate 让出（主路径）。

const fs = require('node:fs/promises');
const path = require('node:path');
const { createGitignoreFilter } = require('./gitignoreFilter');
const { createMatcher } = require('./matcher');
const { normalizeFilePattern } = require('./argumentBuilder');
// 复用 filePreview 的二进制扩展名集合，在遍历阶段就跳过（不读内容、不搜索），
// 对齐 ripgrep 的行为。.NET 项目含大量 .dll/.pdb/.png，这一步能砍掉近半 IO。
const { isBinaryFile } = require('./filePreview');

// 多进程并行：用 child_process.fork 启动独立 Node 子进程（真 OS 进程并行）。
// uTools 渲染进程不支持 worker_threads，异步 IO 并发池又因事件循环开销更慢，
// fork 是唯一能真并行的方案。FD_NO_FORK=1 可强制禁用（降级串行）。
let forkFn = null;
if (process.env.FD_NO_FORK !== '1') {
  try { forkFn = require('node:child_process').fork; } catch { forkFn = null; }
}
let forkRuntimeOk = forkFn !== null; // 运行时是否真正可用，首次 fork 失败后置 false

class JsSearchEngine {
  constructor() {
    this._cancelled = false;
    this._children = [];
  }

  async search(q, handlers) {
    this._cancelled = false;
    const startTime = Date.now();
    let totalMatches = 0;

    try {
      const matcher = createMatcher(q);
      const fileFilterGlobs = parseFileFilter(q.fileFilter);
      const gitFilter = createGitignoreFilter(q.searchPath, splitDirs(q.excludeDirs));
      const files = await walkDir(q.searchPath, q.searchPath, fileFilterGlobs, gitFilter, this);

      if (this._cancelled) { handlers.onDone && handlers.onDone(); return; }

      // 遍历完成，立即推送 scanned 事件（带文件数），让状态栏尽早显示「待搜 N 文件」
      // 避免 countFiles 预遍历导致的目录树二次遍历（纯 IO，5万文件翻倍）
      handlers.onEvent({ type: 'scanned', searchedFiles: files.length });

      if (forkRuntimeOk && forkFn && files.length > 50) {
        try {
          // 多进程并行：文件数足够多才值得 fork（否则进程启动开销 > 收益）
          totalMatches = await this._searchWithFork(q, files, handlers);
        } catch (err) {
          // 运行时不支持 fork（如 uTools 限制或环境异常），永久降级串行
          forkRuntimeOk = false;
          totalMatches = await this._searchSync(matcher, files, handlers);
        }
      } else {
        // 串行 fallback（fork 不可用或文件少）
        totalMatches = await this._searchSync(matcher, files, handlers);
      }

      if (!this._cancelled) {
        const elapsed = ((Date.now() - startTime) / 1000).toFixed(2) + 's';
        handlers.onEvent({ type: 'summary', totalMatches, matchedLines: 0, elapsed, searchedFiles: files.length });
      }
      handlers.onDone && handlers.onDone();
    } catch (err) {
      handlers.onError && handlers.onError(err);
    }
  }

  /** 多进程并行搜索：fork N 个 Node 子进程，动态分发文件批次。
   *  子进程是独立 OS 进程，真并行（不受主进程事件循环拖累）。
   *  实测 4 进程：5.5万文件 6s→2.2s（约 3 倍）。
   *  通信：process.send / process.on('message')，子进程脚本 searchChild.js。 */
  _searchWithFork(q, files, handlers) {
    return new Promise((resolve, reject) => {
      const cpuCount = require('node:os').cpus().length;
      const numChildren = Math.min(Math.max(2, Math.floor(cpuCount / 4)), 4, files.length);
      const childPath = path.join(__dirname, 'searchChild.js');

      let totalMatches = 0;
      let readyCount = 0;
      let nextIdx = 0;
      let active = 0;
      let done = false;
      const self = this;
      const BATCH = 500;

      function finish() {
        if (done) return;
        done = true;
        self._children.forEach((c) => { try { c.send({ type: 'exit' }); } catch {} });
        self._children = [];
        resolve(totalMatches);
      }

      function dispatch(child) {
        if (self._cancelled) { return; }
        if (nextIdx >= files.length) { return; }
        const batch = files.slice(nextIdx, nextIdx + BATCH);
        nextIdx += BATCH;
        active++;
        child.send({ type: 'search', files: batch });
      }

      for (let i = 0; i < numChildren; i++) {
        const c = forkFn(childPath, [], { stdio: 'ignore' });
        self._children.push(c);

        c.on('message', (msg) => {
          if (msg.type === 'ready') {
            readyCount++;
            // 所有子进程就绪后开始分发
            if (readyCount === numChildren) {
              self._children.forEach(dispatch);
            }
          } else if (msg.type === 'batch') {
            // 把每个文件的匹配转成 match 事件推给 searchService
            for (const fr of msg.results) {
              if (self._cancelled) break;
              handlers.onEvent({ type: 'fileBegin', filePath: fr.filePath });
              for (const m of fr.matches) {
                handlers.onEvent({
                  type: 'match',
                  filePath: fr.filePath,
                  lineNumber: m.lineNumber,
                  lineText: m.lineText,
                  matchStart: m.matchStart,
                  matchEnd: m.matchEnd,
                });
                totalMatches++;
              }
              handlers.onEvent({ type: 'fileEnd', filePath: fr.filePath });
            }
            active--;
            // 继续领下一批
            if (!self._cancelled && nextIdx < files.length) {
              dispatch(c);
            } else if (!self._cancelled && nextIdx >= files.length && active === 0) {
              finish();
            }
          }
        });

        c.on('error', (err) => {
          active--;
          if (!done) reject(err);
        });

        c.on('exit', () => {
          active--;
          // 所有批次完成且子进程都退出
          if (!done && nextIdx >= files.length && active <= 0) finish();
        });

        // 初始化子进程的匹配器
        c.send({
          type: 'init',
          query: {
            searchText: q.searchText, isRegex: q.isRegex,
            caseSensitive: q.caseSensitive, wholeWord: q.wholeWord,
          },
        });
      }

      // 取消时通过 cancel() 杀子进程，这里加保险：长时间无进展也结束
      const watchdog = setInterval(() => {
        if (self._cancelled || done) {
          clearInterval(watchdog);
          if (!done) finish();
        }
      }, 200);
    });
  }

  /** 单线程搜索（Worker 不可用时的主路径）。
   *  串行 readFileSync + 定期 setImmediate 让出事件循环，使 UI 能边搜边渲染。
   *  注意：曾尝试 fs.promises.readFile 并发池，纯 Node 里快 30%（6s→4.2s），
   *  但 uTools/Electron 渲染进程事件循环更重，并发池的微任务调度开销超过 IO 并行收益
   *  （实测 uTools 里 8-9s 反而比串行 6s 慢），故回退串行。 */
  async _searchSync(matcher, files, handlers) {
    let totalMatches = 0;
    const YIELD_EVERY = 500;
    let sinceYield = 0;
    const yieldLoop = () => new Promise((r) => setImmediate(r));
    for (const filePath of files) {
      if (this._cancelled) break;
      totalMatches += searchFileSync(filePath, matcher, handlers, this);
      sinceYield++;
      if (sinceYield >= YIELD_EVERY) {
        sinceYield = 0;
        await yieldLoop();
      }
    }
    return totalMatches;
  }

  async countFiles(q) {
    this._cancelled = false;
    const fileFilterGlobs = parseFileFilter(q.fileFilter);
    const gitFilter = createGitignoreFilter(q.searchPath, splitDirs(q.excludeDirs));
    const files = await walkDir(q.searchPath, q.searchPath, fileFilterGlobs, gitFilter, this);
    return files.length;
  }

  cancel() {
    this._cancelled = true;
    // 杀掉所有 fork 子进程
    this._children.forEach((c) => { try { c.kill('SIGTERM'); } catch {} });
    this._children = [];
  }
}

function parseFileFilter(fileFilter) {
  if (!fileFilter || !fileFilter.trim()) return null;
  return fileFilter.split(';').map((p) => p.trim()).filter(Boolean).map(normalizeFilePattern);
}

function splitDirs(excludeDirs) {
  if (!excludeDirs || !excludeDirs.trim()) return [];
  return excludeDirs.split(';').map((d) => d.trim()).filter(Boolean);
}

function matchGlob(filename, glob) {
  const re = '^' + glob.replace(/[.+^${}()|[\]\\]/g, '\\$&').replace(/\*/g, '.*').replace(/\?/g, '.') + '$';
  return new RegExp(re, 'i').test(filename);
}

function matchAnyGlob(filename, globs) {
  if (!globs) return true;
  return globs.some((g) => matchGlob(filename, g));
}

/** 递归遍历目录，返回符合过滤条件的文件绝对路径数组。 */
async function walkDir(rootPath, dir, fileFilterGlobs, gitFilter, engine) {
  const result = [];
  if (engine._cancelled) return result;

  let entries;
  try {
    entries = await fs.readdir(dir, { withFileTypes: true });
  } catch {
    return result;
  }

  for (const entry of entries) {
    if (engine._cancelled) return result;
    const fullPath = path.join(dir, entry.name);
    const relPath = path.relative(rootPath, fullPath).replace(/\\/g, '/');

    if (gitFilter.shouldIgnore(relPath)) continue;

    if (entry.isDirectory()) {
      const sub = await walkDir(rootPath, fullPath, fileFilterGlobs, gitFilter, engine);
      result.push(...sub);
    } else if (entry.isFile()) {
      // 按扩展名跳过二进制文件（.dll/.png/.pdb 等），不读内容不搜索，对齐 ripgrep
      if (isBinaryFile(entry.name)) continue;
      if (matchAnyGlob(entry.name, fileFilterGlobs)) {
        result.push(fullPath);
      }
    }
  }
  return result;
}

/** 单线程搜索单文件（fallback 用）。matcher 提供 findAll(line) 返回 [{start,end}]。 */
function searchFileSync(filePath, matcher, handlers, engine) {
  const fsSync = require('node:fs');
  let buf;
  try {
    buf = fsSync.readFileSync(filePath);
  } catch {
    return 0;
  }

  const scanLen = Math.min(buf.length, 8192);
  for (let i = 0; i < scanLen; i++) {
    if (buf[i] === 0) return 0;
  }

  handlers.onEvent({ type: 'fileBegin', filePath });

  let matchCount = 0;
  const content = buf.toString('utf8');
  const lines = content.split('\n');

  for (let i = 0; i < lines.length; i++) {
    if (engine._cancelled) break;
    const line = lines[i];
    const lineNumber = i + 1;
    if (i === lines.length - 1 && line === '') break;

    const hits = matcher.findAll(line);
    for (let h = 0; h < hits.length; h++) {
      if (engine._cancelled) break;
      handlers.onEvent({
        type: 'match',
        filePath,
        lineNumber,
        lineText: line + '\n',
        matchStart: hits[h].start,
        matchEnd: hits[h].end,
      });
      matchCount++;
    }
  }

  handlers.onEvent({ type: 'fileEnd', filePath });
  return matchCount;
}

/** 异步 IO 版搜索单文件（并发池用）。await readFile 让出主线程，IO 在 libuv 线程池并行。 */
async function searchFileAsync(filePath, matcher, handlers, engine) {
  if (engine._cancelled) return 0;

  let buf;
  try {
    buf = await fs.readFile(filePath);
  } catch {
    return 0;
  }
  if (engine._cancelled) return 0;

  // 二进制检测：前 8KB 含 NUL 字节则跳过
  const scanLen = Math.min(buf.length, 8192);
  for (let i = 0; i < scanLen; i++) {
    if (buf[i] === 0) return 0;
  }

  handlers.onEvent({ type: 'fileBegin', filePath });

  let matchCount = 0;
  const content = buf.toString('utf8');
  const lines = content.split('\n');

  for (let i = 0; i < lines.length; i++) {
    if (engine._cancelled) break;
    const line = lines[i];
    const lineNumber = i + 1;
    if (i === lines.length - 1 && line === '') break;

    const hits = matcher.findAll(line);
    for (let h = 0; h < hits.length; h++) {
      if (engine._cancelled) break;
      handlers.onEvent({
        type: 'match',
        filePath,
        lineNumber,
        lineText: line + '\n',
        matchStart: hits[h].start,
        matchEnd: hits[h].end,
      });
      matchCount++;
    }
  }

  handlers.onEvent({ type: 'fileEnd', filePath });
  return matchCount;
}

module.exports = { JsSearchEngine };
