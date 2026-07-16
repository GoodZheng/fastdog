// 移植自 src/FastDog/Services/RipgrepBridge.cs (ParseRgLine / GetTextOrBase64
// / ParseMatch / ParseSummary)。
// rg --json 每行一个 JSON 对象，type ∈ {begin, match, end, summary}。

/**
 * @typedef {Object} RgEvent
 * @property {'fileBegin'|'match'|'fileEnd'|'summary'} type
 * @property {string} filePath
 * @property {number} lineNumber
 * @property {string} lineText
 * @property {number} matchStart  偏移（rg 时代为字节偏移；纯 JS 引擎为字符偏移）
 * @property {number} matchEnd    偏移
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
