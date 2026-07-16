// 匹配器工厂。
// 注意：曾尝试纯文本用 indexOf 快车道，但实测中文场景比 RegExp 慢 2 倍
// （大小写不敏感时每行 toLowerCase() 对中文是纯开销，而 V8 的 RegExp(i) 有 Unicode 优化）。
// 故统一走 RegExp，仅在保留接口以便未来按需分流。

/**
 * 创建匹配器。
 * @param {object} q SearchQuery（用 searchText/isRegex/caseSensitive/wholeWord）
 * @returns {{findAll:(line:string)=>Array<{start:number,end:number}>}}
 */
function createMatcher(q) {
  let pattern = q.searchText;
  let flags = 'g';
  if (!q.caseSensitive) flags += 'i';
  if (!q.isRegex) pattern = escapeRegex(pattern);
  if (q.wholeWord) pattern = '\\b' + pattern + '\\b';
  const regex = new RegExp(pattern, flags);

  return {
    findAll(line) {
      const results = [];
      regex.lastIndex = 0;
      let m;
      while ((m = regex.exec(line)) !== null) {
        results.push({ start: m.index, end: m.index + m[0].length });
        if (m[0] === '') regex.lastIndex++; // 防零宽死循环
      }
      return results;
    },
  };
}

function escapeRegex(s) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

module.exports = { createMatcher };
