// 移植自 src/FastDog/Services/RipgrepBridge.cs (SearchAsync / CountFilesAsync / KillProcess)。
// 用 child_process.spawn + readline 做行流，cancel 用 kill。

const { spawn } = require('node:child_process');
const { createInterface } = require('node:readline');
const { parseRgLine } = require('./jsonParser');
const { resolveRgPath } = require('./platformRg');

class RipgrepBridge {
  constructor() {
    this._child = null;
  }

  /**
   * 内容搜索（流式）。
   * @param {string[]} args - 来自 argumentBuilder.buildArgs
   * @param {{onEvent:(ev)=>void, onDone?:(code:number)=>void, onError?:(err)=>void}} handlers
   */
  search(args, handlers) {
    const rgPath = resolveRgPath();
    const child = spawn(rgPath, args, { windowsHide: true });
    this._child = child;

    const rl = createInterface({ input: child.stdout });
    rl.on('line', (line) => {
      const ev = parseRgLine(line);
      if (ev) handlers.onEvent(ev);
    });

    // 捕获 stderr 防止管道阻塞，错误时回调
    let stderrText = '';
    const rlErr = createInterface({ input: child.stderr });
    rlErr.on('line', (line) => { stderrText += line + '\n'; });

    child.on('close', (code) => {
      this._child = null;
      if (code !== 0 && code !== null && stderrText && handlers.onError) {
        handlers.onError(new Error('rg 退出码 ' + code + ': ' + stderrText.trim()));
      } else {
        handlers.onDone && handlers.onDone(code);
      }
    });
    child.on('error', (err) => {
      this._child = null;
      handlers.onError && handlers.onError(err);
    });
  }

  /**
   * 统计待搜索文件数（rg --files 行数）。对应 CountFilesAsync。
   * @param {string[]} args - 来自 argumentBuilder.buildFileListArgs
   * @returns {Promise<number>}
   */
  countFiles(args) {
    return new Promise((resolve, reject) => {
      const rgPath = resolveRgPath();
      const child = spawn(rgPath, args, { windowsHide: true });
      let count = 0;
      const rl = createInterface({ input: child.stdout });
      rl.on('line', () => count++);
      child.on('close', () => resolve(count));
      child.on('error', reject);
    });
  }

  /** 取消当前搜索，杀进程。对应 KillProcess。 */
  cancel() {
    if (!this._child) return;
    try {
      this._child.kill('SIGTERM');
    } catch { /* 忽略 */ }
    this._child = null;
  }
}

module.exports = { RipgrepBridge };
