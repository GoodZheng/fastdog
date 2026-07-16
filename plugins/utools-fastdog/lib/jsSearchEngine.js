// 纯 JS 搜索引擎，替代 ripgrepBridge（去二进制以通过 uTools 审核）。
// 接口契约与 RipgrepBridge 一致：search(query, handlers) / countFiles(query) / cancel()。
// 产出的 ev 对象严格对齐 jsonParser.RgEvent schema，matchStart/matchEnd 为 UTF-8 字节偏移
// （用 TextEncoder 反算字符→字节，上层 filePreview/highlight 零改动）。

const fs = require('node:fs/promises');
const path = require('node:path');
const { createGitignoreFilter } = require('./gitignoreFilter');
const { normalizeFilePattern } = require('./argumentBuilder');

class JsSearchEngine {
  constructor() {
    this._cancelled = false;
  }

  /**
   * 流式搜索。直接消费 query（不再走 rg 命令行参数）。
   * @param {object} q SearchQuery
   * @param {{onEvent:(ev)=>void, onDone?:()=>void, onError?:(err)=>void}} handlers
   */
  async search(q, handlers) {
    this._cancelled = false;
    const startTime = Date.now();
    let totalMatches = 0;

    try {
      // 构建正则
      const regex = buildRegex(q);
      const fileFilterGlobs = parseFileFilter(q.fileFilter);
      const gitFilter = createGitignoreFilter(q.searchPath, splitDirs(q.excludeDirs));

      // 遍历所有文件
      const files = await walkDir(q.searchPath, q.searchPath, fileFilterGlobs, gitFilter, this);

      // 逐文件搜索
      for (const filePath of files) {
        if (this._cancelled) break;
        totalMatches += await searchFile(filePath, regex, handlers, this);
      }

      // 推送 summary
      if (!this._cancelled) {
        const elapsed = ((Date.now() - startTime) / 1000).toFixed(2) + 's';
        handlers.onEvent({ type: 'summary', totalMatches, matchedLines: 0, elapsed });
      }
      if (!this._cancelled) handlers.onDone && handlers.onDone();
    } catch (err) {
      handlers.onError && handlers.onError(err);
    }
  }

  /**
   * 统计待搜索文件数。
   * @param {object} q SearchQuery
   * @returns {Promise<number>}
   */
  async countFiles(q) {
    this._cancelled = false;
    const fileFilterGlobs = parseFileFilter(q.fileFilter);
    const gitFilter = createGitignoreFilter(q.searchPath, splitDirs(q.excludeDirs));
    const files = await walkDir(q.searchPath, q.searchPath, fileFilterGlobs, gitFilter, this);
    return files.length;
  }

  cancel() {
    this._cancelled = true;
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

/** 解析 fileFilter（"*.cs;*.txt"）为 glob 数组。 */
function parseFileFilter(fileFilter) {
  if (!fileFilter || !fileFilter.trim()) return null;
  return fileFilter.split(';')
    .map((p) => p.trim())
    .filter(Boolean)
    .map(normalizeFilePattern);
}

function splitDirs(excludeDirs) {
  if (!excludeDirs || !excludeDirs.trim()) return [];
  return excludeDirs.split(';').map((d) => d.trim()).filter(Boolean);
}

/** 简单 glob 匹配（支持 * 和 ?，用于文件名过滤）。 */
function matchGlob(filename, glob) {
  // 把 glob 转成正则（仅 * 和 ? 视为通配，其余转义）
  const re = '^' + glob.replace(/[.+^${}()|[\]\\]/g, '\\$&').replace(/\*/g, '.*').replace(/\?/g, '.') + '$';
  return new RegExp(re, 'i').test(filename);
}

function matchAnyGlob(filename, globs) {
  if (!globs) return true; // 无过滤则全部通过
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
    return result; // 无权限等，跳过
  }

  for (const entry of entries) {
    if (engine._cancelled) return result;
    const fullPath = path.join(dir, entry.name);
    const relPath = path.relative(rootPath, fullPath).replace(/\\/g, '/');

    // gitignore / excludeDirs 判定
    if (gitFilter.shouldIgnore(relPath)) continue;

    if (entry.isDirectory()) {
      const sub = await walkDir(rootPath, fullPath, fileFilterGlobs, gitFilter, engine);
      result.push(...sub);
    } else if (entry.isFile()) {
      if (matchAnyGlob(entry.name, fileFilterGlobs)) {
        result.push(fullPath);
      }
    }
  }
  return result;
}

/** 搜索单个文件，返回匹配数。流式推送 match/fileBegin/fileEnd 事件。 */
async function searchFile(filePath, regex, handlers, engine) {
  if (engine._cancelled) return 0;

  let content;
  try {
    content = await fs.readFile(filePath, 'utf8');
  } catch {
    return 0; // 二进制/无权限，跳过
  }

  handlers.onEvent({ type: 'fileBegin', filePath });

  let matchCount = 0;
  const lines = content.split('\n');
  // 注意：split('\n') 后行号从 1 开始；最后一行若因末尾\n产生空串则不算
  for (let i = 0; i < lines.length; i++) {
    if (engine._cancelled) break;
    const line = lines[i];
    const lineNumber = i + 1;
    // 跳过末尾空行（文件以 \n 结尾时 split 产生的最后一个空串）
    if (i === lines.length - 1 && line === '') break;

    regex.lastIndex = 0;
    let m;
    while ((m = regex.exec(line)) !== null) {
      if (engine._cancelled) break;
      const charStart = m.index;
      const charEnd = m.index + m[0].length;
      // 字符偏移 → UTF-8 字节偏移（上层契约要求字节偏移）
      const byteStart = charToByteOffset(line, charStart);
      const byteEnd = charToByteOffset(line, charEnd);
      handlers.onEvent({
        type: 'match',
        filePath,
        lineNumber,
        lineText: line + '\n', // 对齐 rg：lines.text 含结尾换行
        matchStart: byteStart,
        matchEnd: byteEnd,
      });
      matchCount++;
      // 防止零宽匹配死循环（如正则匹配空串）
      if (m[0] === '') regex.lastIndex++;
    }
  }

  handlers.onEvent({ type: 'fileEnd', filePath });
  return matchCount;
}

/** 字符偏移 → UTF-8 字节偏移。 */
function charToByteOffset(text, charOffset) {
  if (charOffset <= 0) return 0;
  const prefix = text.slice(0, charOffset);
  return new TextEncoder().encode(prefix).length;
}

module.exports = { JsSearchEngine };
