// 移植自 src/FastDog/Services/RipgrepBridge.cs (BuildArguments / BuildFileListArguments
// / NormalizeFilePattern / BuildFilterArgs)。输出改为数组交 spawn，免去 EscapeArg。

/**
 * 构建内容搜索参数。
 * @param {object} q - SearchQuery: { searchText, searchPath, isRegex, caseSensitive, wholeWord, fileFilter, excludeDirs }
 * @returns {string[]} rg 参数数组
 */
function buildArgs(q) {
  const args = ['--json', '--no-heading', '--stats'];
  if (!q.caseSensitive) args.push('-i');
  if (!q.isRegex) args.push('-F');
  if (q.wholeWord) args.push('-w');
  buildFilterArgs(args, q.fileFilter, q.excludeDirs);
  args.push(q.searchText, q.searchPath);
  return args;
}

/**
 * 构建 --files 文件列表参数（用于统计待搜索文件总数）。
 */
function buildFileListArgs(q) {
  const args = ['--files'];
  buildFilterArgs(args, q.fileFilter, q.excludeDirs);
  args.push(q.searchPath);
  return args;
}

function buildFilterArgs(args, fileFilter, excludeDirs) {
  if (fileFilter && fileFilter.trim()) {
    for (const pattern of fileFilter.split(';')) {
      const trimmed = pattern.trim();
      if (trimmed.length > 0) {
        args.push('--iglob', normalizeFilePattern(trimmed));
      }
    }
  }

  const dirs = [];
  if (excludeDirs && excludeDirs.trim()) {
    for (const dir of excludeDirs.split(';')) {
      const trimmed = dir.trim();
      if (trimmed.length > 0) dirs.push(trimmed);
    }
  }
  if (!dirs.includes('.git')) dirs.push('.git');

  for (const dir of dirs) {
    args.push('--glob', '!' + dir);
  }
}

/**
 * 把用户输入的文件过滤模式归一化为 rg --iglob 可识别的 glob。
 * 移植自 NormalizeFilePattern：".cs"→"*.cs"、"cs"→"*.cs"，含通配符原样返回。
 */
function normalizeFilePattern(pattern) {
  if (pattern.includes('*') || pattern.includes('?') || pattern.includes('[')) {
    return pattern;
  }
  if (pattern.startsWith('.')) {
    return '*' + pattern;
  }
  if (!pattern.includes('.')) {
    return '*.' + pattern;
  }
  return pattern;
}

module.exports = { buildArgs, buildFileListArgs, normalizeFilePattern };
