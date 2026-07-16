// 纯 JS 搜索引擎（Worker 多线程版），替代 ripgrepBridge（去二进制以通过 uTools 审核）。
// 接口契约：search(query, handlers) / countFiles(query) / cancel()。
// 主线程负责目录遍历（gitignore/excludeDirs/fileFilter 过滤），文件列表分发给多个 Worker 并行搜索。
// Worker 回传 {filePath, matches[]}，主线程转成 match 事件推给 searchService。
//
// matchStart/matchEnd 为 UTF-8 字节偏移（Worker 内用 TextEncoder 反算，上层零改动）。
// 若 Worker 不可用（uTools 环境限制），fallback 到单线程 searchFileSync。

const fs = require('node:fs/promises');
const path = require('node:path');
const { createGitignoreFilter } = require('./gitignoreFilter');
const { normalizeFilePattern } = require('./argumentBuilder');
// 复用 filePreview 的二进制扩展名集合，在遍历阶段就跳过（不读内容、不搜索），
// 对齐 ripgrep 的行为。.NET 项目含大量 .dll/.pdb/.png，这一步能砍掉近半 IO。
const { isBinaryFile } = require('./filePreview');

// 探测 worker_threads 模块是否存在（uTools Electron 渲染进程可能限制）。
// 注意：require 成功不代表运行时可用——"new Worker()" 可能抛
// "The V8 platform ... does not support creating Workers"。
// 故在首次实际创建时再做一次 try，失败则永久降级单线程。
// FD_NO_WORKER=1 可强制禁用（调试/兼容用）。
let WorkerCtor = null;
if (process.env.FD_NO_WORKER !== '1') {
  try { WorkerCtor = require('node:worker_threads').Worker; } catch { WorkerCtor = null; }
}
let workerRuntimeOk = WorkerCtor !== null; // 运行时是否真正可用，首次 new 失败后置 false

class JsSearchEngine {
  constructor() {
    this._cancelled = false;
    this._workers = [];
  }

  async search(q, handlers) {
    this._cancelled = false;
    const startTime = Date.now();
    let totalMatches = 0;

    try {
      const regex = buildRegex(q);
      const fileFilterGlobs = parseFileFilter(q.fileFilter);
      const gitFilter = createGitignoreFilter(q.searchPath, splitDirs(q.excludeDirs));
      const files = await walkDir(q.searchPath, q.searchPath, fileFilterGlobs, gitFilter, this);

      if (this._cancelled) { handlers.onDone && handlers.onDone(); return; }

      if (workerRuntimeOk && WorkerCtor && files.length > 50) {
        try {
          // 多线程：文件数足够多才值得用 Worker（否则 worker 启动开销 > 收益）
          totalMatches = await this._searchWithWorkers(q, regex, files, handlers);
        } catch (err) {
          // 运行时不支持 Worker（如 uTools Electron 抛 "does not support creating Workers"）
          // 永久降级为单线程，避免后续每次都尝试失败
          workerRuntimeOk = false;
          totalMatches = await this._searchSync(regex, files, handlers);
        }
      } else {
        // 单线程 fallback（Worker 不可用或文件少）
        totalMatches = await this._searchSync(regex, files, handlers);
      }

      if (!this._cancelled) {
        const elapsed = ((Date.now() - startTime) / 1000).toFixed(2) + 's';
        handlers.onEvent({ type: 'summary', totalMatches, matchedLines: 0, elapsed });
      }
      handlers.onDone && handlers.onDone();
    } catch (err) {
      handlers.onError && handlers.onError(err);
    }
  }

  /** 多线程搜索：把 files 均分给 N 个 Worker 并行。 */
  _searchWithWorkers(q, regex, files, handlers) {
    return new Promise((resolve) => {
      const cpuCount = require('node:os').cpus().length;
      const workerCount = Math.min(Math.max(2, cpuCount - 2), 8, files.length);
      const batchSize = Math.ceil(files.length / workerCount);
      const workerPath = path.join(__dirname, 'searchWorker.js');

      let totalMatches = 0;
      let completed = 0;
      let active = 0;
      const self = this;

      // 分批分发：动态分配（一个 worker 做完一批再领下一批），避免长尾
      let nextIdx = 0;
      const BATCH = 200; // 每个 worker 每次领 200 个文件

      function dispatch(worker) {
        if (self._cancelled) { worker.postMessage({ type: 'exit' }); return; }
        const batch = [];
        while (nextIdx < files.length && batch.length < BATCH) {
          batch.push(files[nextIdx++]);
        }
        if (batch.length === 0) {
          // 没活干了，让这个 worker 退出
          worker.postMessage({ type: 'exit' });
          return;
        }
        active++;
        worker.postMessage({ type: 'search', files: batch });
      }

      for (let i = 0; i < workerCount; i++) {
        const w = new WorkerCtor(workerPath, {
          workerData: { regexSource: regex.source, regexFlags: regex.flags },
        });
        self._workers.push(w);

        w.on('message', (msg) => {
          if (msg.type === 'batch') {
            // 把每个文件的匹配转成 match 事件推给 searchService
            for (const fr of msg.results) {
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
              dispatch(w);
            } else {
              w.postMessage({ type: 'exit' });
            }
          }
        });

        w.on('error', (err) => {
          active--;
          // worker 出错不影响其他，继续
          if (!self._cancelled && nextIdx < files.length) dispatch(w);
          else { w.postMessage({ type: 'exit' }); }
        });

        dispatch(w);
      }

      // 检查所有 worker 是否完成
      const checkDone = setInterval(() => {
        if (self._cancelled || (nextIdx >= files.length && active === 0)) {
          clearInterval(checkDone);
          // 确保所有 worker 退出
          self._workers.forEach((w) => { try { w.postMessage({ type: 'exit' }); } catch {} });
          self._workers = [];
          resolve(totalMatches);
        }
      }, 50);
    });
  }

  /** 单线程 fallback（Worker 不可用时）。async + 定期让出事件循环，使 UI 能边搜边渲染。
   *  让出用 setImmediate（比 setTimeout(0) 延迟低一个数量级），频率不能太高否则调度开销压垮搜索。 */
  async _searchSync(regex, files, handlers) {
    let totalMatches = 0;
    // 每 500 个文件让出一次：5.5 万文件约让出 110 次，既能让 UI 周期性刷新，又不会因调度拖慢搜索
    const YIELD_EVERY = 500;
    let sinceYield = 0;
    const yieldLoop = () => new Promise((r) => setImmediate(r));
    for (const filePath of files) {
      if (this._cancelled) break;
      totalMatches += searchFileSync(filePath, regex, handlers, this);
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
    this._workers.forEach((w) => { try { w.terminate(); } catch {} });
    this._workers = [];
  }
}

/** 构建 RegExp。纯文本模式转义特殊字符；全词加 \b；大小写敏感控制。 */
function buildRegex(q) {
  let pattern = q.searchText;
  let flags = 'g';
  if (!q.caseSensitive) flags += 'i';
  if (!q.isRegex) pattern = escapeRegex(pattern);
  if (q.wholeWord) pattern = '\\b' + pattern + '\\b';
  return new RegExp(pattern, flags);
}

function escapeRegex(s) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
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

/** 单线程搜索单文件（fallback 用）。 */
function searchFileSync(filePath, regex, handlers, engine) {
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
  const encoder = new TextEncoder();

  for (let i = 0; i < lines.length; i++) {
    if (engine._cancelled) break;
    const line = lines[i];
    const lineNumber = i + 1;
    if (i === lines.length - 1 && line === '') break;

    regex.lastIndex = 0;
    let m;
    while ((m = regex.exec(line)) !== null) {
      if (engine._cancelled) break;
      const byteStart = encoder.encode(line.slice(0, m.index)).length;
      const byteEnd = byteStart + encoder.encode(line.slice(m.index, m.index + m[0].length)).length;
      handlers.onEvent({
        type: 'match',
        filePath,
        lineNumber,
        lineText: line + '\n',
        matchStart: byteStart,
        matchEnd: byteEnd,
      });
      matchCount++;
      if (m[0] === '') regex.lastIndex++;
    }
  }

  handlers.onEvent({ type: 'fileEnd', filePath });
  return matchCount;
}

module.exports = { JsSearchEngine };
