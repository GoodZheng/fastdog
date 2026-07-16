// 匹配文本高亮渲染：把行文本按字符偏移区间用 <mark> 包裹。
// 注意：纯 JS 引擎产出的 matchStart/matchEnd 已是字符偏移（m.index），
// 直接 slice 即可，无需字节↔字符转换（已废除 ripgrep 时代的字节偏移契约）。

/**
 * 把一行文本渲染成 HTML，匹配区间加 <mark class="match-hl">。
 * @param {string} text 行文本
 * @param {number} start 字符偏移起点
 * @param {number} end 字符偏移终点
 * @returns {string} HTML 字符串（已转义）
 */
function highlightLine(text, start, end) {
  return escapeHtml(text.slice(0, start))
    + '<mark class="match-hl">' + escapeHtml(text.slice(start, end)) + '</mark>'
    + escapeHtml(text.slice(end));
}

function escapeHtml(s) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

window.FastDogHighlight = { highlightLine, escapeHtml };
