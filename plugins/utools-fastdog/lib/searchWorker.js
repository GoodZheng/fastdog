// Worker 线程脚本：接收一批文件路径 + 搜索参数，并行搜索后回传每个文件的匹配结果。
// 主线程（jsSearchEngine.js）负责目录遍历，把文件列表分发给多个 Worker。
//
// 注意：Worker 内 require 的模块路径相对于本文件。仅用 Node 内置模块 + 纯 JS 依赖（ignore/argumentBuilder）。

const { workerData, parentPort } = require('node:worker_threads');
const fs = require('node:fs');
const path = require('node:path');

// workerData 在线程启动时传入：{ regexSource, regexFlags, wholeWord, isPlainText }
// 收到消息 { type: 'search', files: [...] } 时搜索这批文件，回传每个文件的匹配
const { regexSource, regexFlags } = workerData;
const regex = new RegExp(regexSource, regexFlags);

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
  const encoder = new TextEncoder();

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const lineNumber = i + 1;
    if (i === lines.length - 1 && line === '') break;

    regex.lastIndex = 0;
    let m;
    while ((m = regex.exec(line)) !== null) {
      const charStart = m.index;
      const charEnd = m.index + m[0].length;
      // 字符偏移 → UTF-8 字节偏移（复用同一 encoder，减少分配）
      const byteStart = encoder.encode(line.slice(0, charStart)).length;
      const byteEnd = byteStart + encoder.encode(line.slice(charStart, charEnd)).length;
      matches.push({
        lineNumber,
        lineText: line + '\n',
        matchStart: byteStart,
        matchEnd: byteEnd,
      });
      if (m[0] === '') regex.lastIndex++;
    }
  }

  if (matches.length === 0) return null;
  return { filePath, matches };
}
