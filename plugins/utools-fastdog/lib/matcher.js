// 匹配器工厂：按搜索模式分流。
// 纯文本模式（isRegex=false && !wholeWord）用 indexOf 循环——V8 的 indexOf 走优化路径，
// 比 RegExp.exec 快得多（ripgrep 对字面量用 memchr/teddy，JS 里 indexOf 是最接近的）。
// 正则模式 / 全词匹配仍用 RegExp。

/**
 * 创建匹配器。
 * @param {object} q SearchQuery（用 searchText/isRegex/caseSensitive/wholeWord）
 * @returns {{findAll:(line:string)=>Array<{start:number,end:number}>}}
 */
function createMatcher(q) {
  const plain = !q.isRegex && !q.wholeWord;

  if (plain) {
    // 纯文本 indexOf 快车道
    const needle = q.caseSensitive ? q.searchText : q.searchText.toLowerCase();
    return {
      findAll(line) {
        const hay = q.caseSensitive ? line : line.toLowerCase();
        const results = [];
        let from = 0;
        let idx;
        while ((idx = hay.indexOf(needle, from)) !== -1) {
          results.push({ start: idx, end: idx + needle.length });
          from = idx + needle.length;
          // 防零宽（needle 不可能为空，但保险）
          if (needle.length === 0) break;
        }
        return results;
      },
    };
  }

  // 正则 / 全词：构造 RegExp
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
