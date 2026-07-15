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
