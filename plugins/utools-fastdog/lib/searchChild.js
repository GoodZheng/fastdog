// child_process 子进程脚本：接收文件批次 + 搜索参数，搜索后回传匹配结果。
// 用 child_process.fork 启动（独立 OS 进程，真并行，不受主进程事件循环拖累）。
// 通信用 process.send / process.on('message')。

const fs = require('node:fs');
const { createMatcher } = require('./matcher');

let matcher = null;

process.on('message', (msg) => {
  if (msg.type === 'init') {
    matcher = createMatcher(msg.query);
    process.send({ type: 'ready' });
  } else if (msg.type === 'search') {
    const results = [];
    for (const filePath of msg.files) {
      const r = searchFile(filePath);
      if (r) results.push(r);
    }
    process.send({ type: 'batch', results });
  } else if (msg.type === 'exit') {
    process.exit(0);
  }
});

function searchFile(filePath) {
  let buf;
  try { buf = fs.readFileSync(filePath); } catch { return null; }
  const scanLen = Math.min(buf.length, 8192);
  for (let i = 0; i < scanLen; i++) { if (buf[i] === 0) return null; }
  const matches = [];
  const lines = buf.toString('utf8').split('\n');
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (i === lines.length - 1 && line === '') break;
    const hits = matcher.findAll(line);
    for (let h = 0; h < hits.length; h++) {
      matches.push({ lineNumber: i + 1, lineText: line + '\n', matchStart: hits[h].start, matchEnd: hits[h].end });
    }
  }
  if (matches.length === 0) return null;
  return { filePath, matches };
}
