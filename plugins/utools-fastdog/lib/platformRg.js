// 按 process.platform 选择捆绑的 ripgrep 二进制。非 win32 平台补执行权限。
const path = require('node:path');
const fs = require('node:fs');

const RG_MAP = {
  win32: 'bin/rg-win-x64.exe',
  darwin: 'bin/rg-darwin-x64',
  linux: 'bin/rg-linux-x64',
};

/**
 * 返回当前平台 rg 可执行文件绝对路径。
 * 非首次调用会缓存；首次在非 win32 上补 chmod 0o755。
 */
let cached = null;
function resolveRgPath() {
  if (cached) return cached;
  const rel = RG_MAP[process.platform];
  if (!rel) throw new Error(`FastDog 插件不支持平台: ${process.platform}`);
  const p = path.join(__dirname, '..', rel);
  if (!fs.existsSync(p)) {
    throw new Error(`找不到 ripgrep 二进制: ${p}。请确认 bin/ 目录已放入对应平台文件。`);
  }
  if (process.platform !== 'win32') {
    try { fs.chmodSync(p, 0o755); } catch { /* 忽略权限失败 */ }
  }
  cached = p;
  return p;
}

module.exports = { resolveRgPath };
