// 封装 ignore 包：合并搜索根目录的 .gitignore + 用户 excludeDirs + 强制 .git。
// 提供相对路径的 shouldIgnore 判定。
const fs = require('node:fs');
const path = require('node:path');
const ignore = require('ignore');

/**
 * 构建一个 gitignore 过滤器。
 * @param {string} rootPath 搜索根目录
 * @param {string[]} excludeDirs 用户配置的排除目录（如 ['bin','obj','node_modules']）
 * @returns {{shouldIgnore: (relPath:string)=>boolean}}
 */
function createGitignoreFilter(rootPath, excludeDirs) {
  const ig = ignore();

  // 1. 强制排除 .git
  ig.add('.git');

  // 2. 用户配置的 excludeDirs（每项作为目录 glob，同时匹配目录及其下所有内容）
  if (excludeDirs && excludeDirs.length) {
    excludeDirs.forEach((d) => {
      const t = d.trim();
      if (t) {
        ig.add(t);
        ig.add(t + '/**');
      }
    });
  }

  // 3. 解析根目录 .gitignore（简单版：只读根目录一个，不递归子目录的 .gitignore）
  const gitignorePath = path.join(rootPath, '.gitignore');
  try {
    if (fs.existsSync(gitignorePath)) {
      const content = fs.readFileSync(gitignorePath, 'utf8');
      ig.add(content);
    }
  } catch { /* 读取失败则忽略 */ }

  return {
    shouldIgnore(relPath) {
      try { return ig.ignores(relPath); } catch { return false; }
    },
  };
}

module.exports = { createGitignoreFilter };
