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
 * 语义：from 取当天 00:00，to 取次日 00:00（半开区间 [from, to+1)），含 to 当天全天。
 * @param {{lastModified: Date}} result
 * @param {{dateFilterEnabled: boolean, dateFrom: Date|null, dateTo: Date|null}} query
 * @returns {boolean} true=保留
 */
function passDateFilter(result, query) {
  if (!query.dateFilterEnabled) return true;
  if (query.dateFrom && result.lastModified < startOfDay(query.dateFrom)) return false;
  if (query.dateTo && result.lastModified > nextDayStart(query.dateTo)) return false;
  return true;
}

function startOfDay(d) {
  const x = new Date(d);
  x.setHours(0, 0, 0, 0);
  return x;
}
function nextDayStart(d) {
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
