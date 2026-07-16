// uTools preload：装配 lib，暴露 window.fastdog 给前端；处理 onPluginEnter 的 3 个入口。
// 所有 uTools/Electron API 集中在此，lib/*.js 保持纯逻辑可单测。

const { JsSearchEngine } = require('./lib/jsSearchEngine');
const { createSearchSession } = require('./lib/searchService');
const { loadFileContent, byteToCharOffset } = require('./lib/filePreview');

const bridge = new JsSearchEngine();
let currentSession = null;

const fastdog = {
  /**
   * 搜索。query 字段对应桌面版 SearchQuery。
   * @param {object} query
   * @param {{onResult:(r)=>void, onStats:(s)=>void, onDone:()=>void, onError:(e)=>void}} handlers
   */
  search(query, handlers) {
    // 直接搜索（不再预调 countFiles —— search 内部遍历后通过 scanned 事件回传文件数，避免目录树二次遍历）
    doSearch(query, handlers);
  },

  cancel() {
    bridge.cancel();
    currentSession = null;
  },

  /** 预览文件。返回 { info, matches }，matches 已填充全局偏移。 */
  previewFile(filePath, matches) {
    const info = loadFileContent(filePath);
    // 计算每个匹配行内偏移→全局偏移（供 UI 滚动定位 + 高亮）
    if (info.content && matches && matches.length) {
      for (const m of matches) {
        const lineIndex = m.lineNumber - 1;
        let offset = 0;
        for (let i = 0; i < lineIndex && i < info.lineLengths.length; i++) {
          offset += info.lineLengths[i];
        }
        m.globalMatchStart = offset + byteToCharOffset(m.lineText, m.matchStart);
        m.globalMatchEnd = offset + byteToCharOffset(m.lineText, m.matchEnd);
      }
    }
    return { info, matches };
  },

  openFile(filePath) {
    try { utools.shellOpenPath(filePath); } catch (e) { console.error(e); }
  },

  openInExplorer(filePath) {
    try { utools.shellShowItemInFolder(filePath); } catch (e) { console.error(e); }
  },

  copyText(text) {
    try { utools.copyText(text); } catch (e) { console.error(e); }
  },

  selectDirectory() {
    return utools.showOpenDialog({ properties: ['openDirectory'] });
  },

  getLastPath() {
    return utools.dbStorage.getItem('fastdog:lastPath') || '';
  },

  saveLastPath(p) {
    utools.dbStorage.setItem('fastdog:lastPath', p);
  },

  // ===== 会话恢复：保存/恢复完整搜索条件（对齐桌面版 SearchHistoryEntry 字段）=====
  /**
   * @param {object} s - { searchText, searchPath, isRegex, caseSensitive, wholeWord,
   *                       fileFilter, excludeDirs }
   */
  saveSession(s) {
    utools.dbStorage.setItem('fastdog:session', s);
  },

  getSession() {
    return utools.dbStorage.getItem('fastdog:session') || null;
  },

  // ===== 布局持久化：保存/恢复分割比例 + 列宽（对齐桌面版 LayoutConfig）=====
  /**
   * @param {object} l - { rowRatio, colRatio, colWidths: { colName, colSize, colMatch, colPath, colMtime } }
   */
  saveLayout(l) {
    utools.dbStorage.setItem('fastdog:layout', l);
  },

  getLayout() {
    return utools.dbStorage.getItem('fastdog:layout') || null;
  },
};

window.fastdog = fastdog;

function doSearch(query, handlers) {
  currentSession = createSearchSession(query.searchPath, query, { onResult: handlers.onResult });

  bridge.search(query, {
    onEvent: (ev) => {
      currentSession.handleEvent(ev);
      // 遍历完成：尽早更新状态栏「待搜 N 文件」
      if (ev.type === 'scanned') {
        handlers.onStats && handlers.onStats({
          searchedFiles: ev.searchedFiles,
          foundFiles: 0,
          elapsed: '',
          totalMatches: 0,
        });
      } else if (ev.type === 'summary') {
        // 搜索完成：最终统计
        handlers.onStats && handlers.onStats({
          searchedFiles: ev.searchedFiles,
          foundFiles: currentSession.getFoundCount(),
          elapsed: ev.elapsed,
          totalMatches: ev.totalMatches,
        });
      }
    },
    onDone: () => handlers.onDone && handlers.onDone(),
    onError: (err) => handlers.onError && handlers.onError(err),
  });
}

// ===== 进入入口处理（3 个 feature code）=====
utools.onPluginEnter(({ code, type, payload }) => {
  // 把进入信息暂存，供 app.js 读取后预填搜索词/路径
  let enter = { code, searchText: '', searchPath: fastdog.getLastPath() };

  if (code === 'fastdog-search-text' && type === 'over') {
    // 超级面板选中文本 → 预填搜索词
    enter.searchText = typeof payload === 'string' ? payload : '';
  } else if (code === 'fastdog-search-dir' && type === 'files' && Array.isArray(payload)) {
    // 文件管理器选中文件夹 → 预填路径
    enter.searchPath = payload[0] && payload[0].path ? payload[0].path : enter.searchPath;
  }

  window.__fastdogEnter = enter;
  // 通知前端页面
  window.dispatchEvent(new CustomEvent('fastdog:enter', { detail: enter }));
});

module.exports = fastdog;
