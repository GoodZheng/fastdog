// Worker 线程脚本：接收一批文件路径 + 搜索参数，并行搜索后回传每个文件的匹配结果。
// 主线程（jsSearchEngine.js）负责目录遍历，把文件列表分发给多个 Worker。
//
// 注意：Worker 内 require 的模块路径相对于本文件。matcher 自带 indexOf 快车道。

const { workerData, parentPort } = require('node:worker_threads');
const fs = require('node:fs');
const { createMatcher } = require('./matcher');

// workerData 传入搜索参数，用 matcher 工厂创建匹配器（纯文本走 indexOf，正则走 RegExp）
const matcher = createMatcher(workerData);

parentPort.on('message', (msg) => {
  if (msg.type === 'search') {
    const results = [];
    for (const filePath of msg.files) {
      const r = searchFile(filePath);
      if (r) results.push(r);
    }
    parentPort.postMessage({ type: 'batch', results });
  } else if (msg.type === 'exit') {
    process.exit(0);
  }
});

/** 搜索单个文件，返回 { filePath, matches: [...] } 或 null（无匹配/二进制/错误）。 */
function searchFile(filePath) {
  let buf;
  try {
    buf = fs.readFileSync(filePath);
  } catch {
    return null;
  }

  // 二进制检测：前 8KB 含 NUL 字节则跳过
  const scanLen = Math.min(buf.length, 8192);
  for (let i = 0; i < scanLen; i++) {
    if (buf[i] === 0) return null;
  }

  const content = buf.toString('utf8');
  const matches = [];
  const lines = content.split('\n');

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const lineNumber = i + 1;
    if (i === lines.length - 1 && line === '') break;

    const hits = matcher.findAll(line);
    for (let h = 0; h < hits.length; h++) {
      matches.push({
        lineNumber,
        lineText: line + '\n',
        matchStart: hits[h].start,
        matchEnd: hits[h].end,
      });
    }
  }

  if (matches.length === 0) return null;
  return { filePath, matches };
}
