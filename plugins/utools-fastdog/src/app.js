// UI 主逻辑：搜索条件读写、调用 window.fastdog、渲染文件列表/匹配行/预览。
// 仅通过 window.fastdog.* 和 utools.* 调能力。

(function () {
  'use strict';
  const fd = window.fastdog;
  const H = window.FastDogHighlight;
  let allResults = [];        // 当前搜索结果
  let selectedResult = null;  // 当前选中文件
  // 文件列表排序状态
  let sortState = { col: null, desc: false };
  // 当前布局状态（拖拽时更新，mouseup 时持久化）。初始从已恢复的 layout 继承。
  const currentLayout = { rowRatio: null, colRatio: null, colWidths: {} };

  // ===== DOM 引用 =====
  const $ = (id) => document.getElementById(id);
  const els = {
    path: $('search-path'), text: $('search-text'),
    browse: $('btn-browse'), search: $('btn-search'), cancel: $('btn-cancel'),
    modeRegex: $('mode-regex'), modeText: $('mode-text'),
    optCase: $('opt-case'), optWord: $('opt-word'),
    tagFileFilter: $('tag-filefilter'), inputFileFilter: $('input-filefilter'),
    tagExclude: $('tag-exclude'), inputExclude: $('input-exclude'),
    fileTbody: $('file-tbody'), matchList: $('match-list'), matchCount: $('match-count'),
    matchFilename: $('match-filename'),
    previewCode: $('preview-code'), previewFilename: $('preview-filename'),
    binaryHint: $('binary-hint'), truncateHint: $('truncate-hint'),
    optWrap: $('opt-wrap'),
    statusText: $('status-text'), statFiles: $('stat-files'),
    statMatches: $('stat-matches'), statElapsed: $('stat-elapsed'),
  };

  // ===== 初始化：恢复上次路径 + 监听进入事件预填 =====
  function init() {
    bindOptions();
    bindButtons();
    bindSplitters();
    bindDragDrop();
    initColumnResize();
    bindSort();
    bindFileTableDelegation();

    // 恢复上次会话（搜索条件）+ 布局（分割比例 + 列宽）
    restoreSession();
    restoreLayout();

    // uTools 进入事件（preload 派发 fastdog:enter）
    window.addEventListener('fastdog:enter', (e) => {
      applyEnter(e.detail || window.__fastdogEnter || {});
    });
    // 若 preload 已先于本脚本执行设置过 enter
    if (window.__fastdogEnter) applyEnter(window.__fastdogEnter);
  }

  // ===== 会话恢复：把上次的搜索条件填回 UI =====
  function restoreSession() {
    const s = fd.getSession();
    if (!s) {
      // 无历史会话，仅恢复路径
      els.path.value = fd.getLastPath() || '';
      return;
    }
    els.path.value = s.searchPath || '';
    els.text.value = s.searchText || '';
    els.modeRegex.checked = !!s.isRegex;
    els.modeText.checked = !s.isRegex;
    els.optCase.checked = !!s.caseSensitive;
    els.optWord.checked = !!s.wholeWord;
    els.inputFileFilter.value = s.fileFilter || '';
    els.inputExclude.value = s.excludeDirs || '';
    refreshTag(els.tagFileFilter, els.inputFileFilter, '文件: ');
    refreshTag(els.tagExclude, els.inputExclude, '排除: ');
  }

  // ===== 布局恢复：分割比例 + 列宽 =====
  function restoreLayout() {
    const l = fd.getLayout();
    if (!l) return;
    if (l.rowRatio) {
      $('file-table-wrap').style.flex = (l.rowRatio * 100) + ' 0 0';
      $('bottom-panel').style.flex = ((1 - l.rowRatio) * 100) + ' 0 0';
      currentLayout.rowRatio = l.rowRatio;
    }
    if (l.colRatio) {
      $('match-panel').style.flex = (l.colRatio * 100) + ' 0 0';
      $('preview-panel').style.flex = ((1 - l.colRatio) * 100) + ' 0 0';
      currentLayout.colRatio = l.colRatio;
    }
    if (l.colWidths) {
      const map = { colName: '.col-name', colSize: '.col-size', colMatch: '.col-match', colPath: '.col-path', colMtime: '.col-mtime' };
      const ths = document.querySelectorAll('#file-table th');
      ths.forEach((th) => {
        for (const key in map) {
          if (th.matches(map[key]) && l.colWidths[key]) {
            th.style.width = l.colWidths[key] + 'px';
            currentLayout.colWidths[key] = l.colWidths[key];
          }
        }
      });
    }
    // 恢复自动换行状态
    if (l.previewWordWrap) {
      els.optWrap.checked = true;
      els.previewCode.classList.add('wrap');
      currentLayout.previewWordWrap = true;
    }
  }

  function applyEnter(enter) {
    if (!enter) return;
    if (enter.searchPath) els.path.value = enter.searchPath;
    if (enter.searchText) els.text.value = enter.searchText;
    els.text.focus();
  }

  // ===== 选项交互 =====
  function bindOptions() {
    // 文件过滤标签内联编辑
    bindTagEdit(els.tagFileFilter, els.inputFileFilter, '文件: ');
    bindTagEdit(els.tagExclude, els.inputExclude, '排除: ');
    // 默认值（对齐桌面版 MainViewModel：FileFilter=空，ExcludeDirs="bin;obj"）
    els.inputFileFilter.value = '';
    els.inputExclude.value = 'bin;obj';
    refreshTag(els.tagFileFilter, els.inputFileFilter, '文件: ');
    refreshTag(els.tagExclude, els.inputExclude, '排除: ');

    // 自动换行（状态即时持久化）
    els.optWrap.addEventListener('change', () => {
      els.previewCode.classList.toggle('wrap', els.optWrap.checked);
      currentLayout.previewWordWrap = els.optWrap.checked;
      saveLayout();
    });
  }

  function bindTagEdit(btn, input, prefix) {
    btn.addEventListener('click', () => {
      btn.classList.add('hidden');
      input.classList.remove('hidden');
      input.focus();
      input.select();
    });
    input.addEventListener('blur', () => {
      refreshTag(btn, input, prefix);
      btn.classList.remove('hidden');
      input.classList.add('hidden');
    });
    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') input.blur();
      if (e.key === 'Escape') input.blur();
    });
  }
  function refreshTag(btn, input, prefix) {
    // 对齐桌面版 FileFilterDisplay / ExcludeDirsDisplay：空时显示 * 或 (无)
    const val = input.value.trim();
    let text;
    if (prefix === '文件: ') {
      text = val ? ('文件: ' + val) : '文件: *';
    } else {
      text = val ? ('排除: ' + val) : '排除: (无)';
    }
    btn.textContent = text;
  }

  // ===== 按钮绑定 =====
  function bindButtons() {
    els.browse.addEventListener('click', () => {
      const r = fd.selectDirectory();
      if (r && r[0]) els.path.value = r[0];
    });
    els.search.addEventListener('click', doSearch);
    els.text.addEventListener('keydown', (e) => { if (e.key === 'Enter') doSearch(); });
    els.path.addEventListener('keydown', (e) => { if (e.key === 'Enter') doSearch(); });
    els.cancel.addEventListener('click', () => { fd.cancel(); setStatus('已取消'); });
  }

  // ===== 拖放文件夹设为搜索路径 =====
  function bindDragDrop() {
    document.body.addEventListener('dragover', (e) => { e.preventDefault(); });
    document.body.addEventListener('drop', (e) => {
      e.preventDefault();
      const f = e.dataTransfer.files[0];
      if (f && f.path) els.path.value = f.path;
    });
  }

  // ===== 拼装 SearchQuery =====
  function buildQuery() {
    return {
      searchPath: els.path.value.trim(),
      searchText: els.text.value,
      isRegex: els.modeRegex.checked,
      caseSensitive: els.optCase.checked,
      wholeWord: els.optWord.checked,
      fileFilter: els.inputFileFilter.value.trim(),
      excludeDirs: els.inputExclude.value.trim(),
    };
  }

  // ===== 搜索 =====
  function doSearch() {
    const q = buildQuery();
    if (!q.searchPath) { setStatus('请先选择搜索路径'); return; }
    if (!q.searchText) { setStatus('请输入搜索内容'); return; }

    fd.saveLastPath(q.searchPath);
    sortState = { col: null, desc: false };  // 新搜索重置排序状态
    updateSortIndicators();
    // 保存完整会话（下次打开恢复）
    fd.saveSession({
      searchText: q.searchText,
      searchPath: q.searchPath,
      isRegex: q.isRegex,
      caseSensitive: q.caseSensitive,
      wholeWord: q.wholeWord,
      fileFilter: q.fileFilter,
      excludeDirs: q.excludeDirs,
    });
    allResults = [];
    clearUI();
    setStatus('搜索中...');

    fd.search(q, {
      onResult: (r) => { allResults.push(r); enqueueFileRow(r, allResults.length - 1); },
      onStats: (s) => {
        flushRows(); // 确保搜索结束时所有待渲染行已插入
        els.statFiles.textContent = s.searchedFiles;
        if (s.elapsed === '') {
          // scanned 事件（遍历完成，搜索进行中）：只更新文件数 + 状态文本，不清零匹配/耗时
          setStatus('正在搜索 ' + s.searchedFiles + ' 个文件...');
        } else {
          // summary 事件（搜索完成）：完整统计
          els.statMatches.textContent = s.totalMatches;
          els.statElapsed.textContent = s.elapsed;
          setStatus('完成：找到 ' + s.foundFiles + ' 个文件，' + s.totalMatches + ' 处匹配');
        }
      },
      onDone: () => { if (els.statusText.textContent === '搜索中...') setStatus('完成'); },
      onError: (err) => setStatus('错误：' + err.message),
    });
  }

  function clearUI() {
    els.fileTbody.innerHTML = '';
    els.matchList.innerHTML = '';
    els.previewCode.innerHTML = '';
    els.statFiles.textContent = '0';
    els.statMatches.textContent = '0';
    els.statElapsed.textContent = '0s';
  }
  function setStatus(t) { els.statusText.textContent = t; }

  // ===== 文件列表行（批量渲染 + 事件委托，性能优化）=====
  // 性能要点：
  // 1. onResult 攒进 pendingRows，rAF 回调里用 innerHTML 批量拼接一次性插入（避免逐次 appendChild 重排）
  // 2. 行用 data-idx 索引到 allResults，事件委托到 tbody（避免每行绑 3 个监听器）
  // 3. HTML 字符串拼接 + 一次 innerHTML 赋值，比逐个 createElement 快一个数量级
  let pendingRows = [];
  let rafScheduled = false;

  function enqueueFileRow(r, idx) {
    pendingRows.push({ r, idx });
    if (!rafScheduled) {
      rafScheduled = true;
      requestAnimationFrame(flushRows);
    }
  }

  function flushRows() {
    rafScheduled = false;
    if (!pendingRows.length) return;
    // 批量拼 HTML 一次性追加（避免逐次 appendChild 重排；字符串拼接 + 一次 innerHTML 比逐个 createElement 快）
    const html = pendingRows.map(({ r, idx }) =>
      '<tr data-idx="' + idx + '">' +
      '<td class="col-name">' + H.escapeHtml(r.fileName) + '</td>' +
      '<td class="col-size">' + formatSize(r.fileSize) + '</td>' +
      '<td class="col-match"><span class="match-badge">' + r.matchCount + '</span></td>' +
      '<td class="col-path">' + H.escapeHtml(r.filePath) + '</td>' +
      '<td class="col-mtime">' + formatDate(r.lastModified) + '</td>' +
      '</tr>'
    ).join('');
    els.fileTbody.insertAdjacentHTML('beforeend', html);
    pendingRows = [];
  }

  // 事件委托：在 tbody 上一次性绑定 click/dblclick/contextmenu
  function bindFileTableDelegation() {
    els.fileTbody.addEventListener('click', (e) => {
      const tr = e.target.closest('tr');
      if (!tr) return;
      const idx = parseInt(tr.dataset.idx, 10);
      const r = allResults[idx];
      if (r) selectResult(r, tr);
    });
    els.fileTbody.addEventListener('dblclick', (e) => {
      const tr = e.target.closest('tr');
      if (!tr) return;
      const idx = parseInt(tr.dataset.idx, 10);
      const r = allResults[idx];
      if (r) fd.openFile(r.filePath);
    });
    els.fileTbody.addEventListener('contextmenu', (e) => {
      const tr = e.target.closest('tr');
      if (!tr) return;
      const idx = parseInt(tr.dataset.idx, 10);
      const r = allResults[idx];
      if (r) showFileMenu(e, r);
    });
  }

  // 兼容排序重渲染：rerenderFileList 仍用单行 append（重排场景行数已固定，开销小）
  function appendFileRow(r) {
    const idx = allResults.indexOf(r);
    const tr = document.createElement('tr');
    tr.dataset.idx = idx;
    tr.innerHTML =
      '<td class="col-name">' + H.escapeHtml(r.fileName) + '</td>' +
      '<td class="col-size">' + formatSize(r.fileSize) + '</td>' +
      '<td class="col-match"><span class="match-badge">' + r.matchCount + '</span></td>' +
      '<td class="col-path">' + H.escapeHtml(r.filePath) + '</td>' +
      '<td class="col-mtime">' + formatDate(r.lastModified) + '</td>';
    els.fileTbody.appendChild(tr);
    if (r.filePath === (selectedResult ? selectedResult.filePath : null)) {
      tr.classList.add('selected');
    }
  }

  // ===== 文件列表排序（对齐桌面版 DataGrid 点击列头排序）=====
  // th class → 排序字段名
  const SORT_KEY = { 'col-name': 'fileName', 'col-size': 'fileSize', 'col-match': 'matchCount', 'col-path': 'filePath', 'col-mtime': 'lastModified' };

  function bindSort() {
    document.querySelectorAll('#file-table th').forEach((th) => {
      // 找该 th 对应的排序字段（无映射的列不支持排序）
      const key = SORT_KEY[Object.keys(SORT_KEY).find((c) => th.classList.contains(c))];
      if (!key) return;
      th.classList.add('sortable');
      th.addEventListener('click', () => {
        if (sortState.col === key) {
          sortState.desc = !sortState.desc;       // 同列切换升降序
        } else {
          sortState.col = key;
          sortState.desc = false;                  // 新列默认升序
        }
        updateSortIndicators();
        rerenderFileList();
      });
    });
  }

  function updateSortIndicators() {
    document.querySelectorAll('#file-table th').forEach((th) => {
      const key = SORT_KEY[Object.keys(SORT_KEY).find((c) => th.classList.contains(c))];
      const textEl = th.querySelector('.th-text');
      if (!textEl) return;
      // 清除旧指示符（只改 .th-text 文本，保留 resizer 手柄等其他子节点）
      const base = textEl.textContent.replace(/[▲▼]\s*$/, '').trim();
      textEl.textContent = (key && sortState.col === key)
        ? (base + ' ' + (sortState.desc ? '▼' : '▲'))
        : base;
    });
  }

  function rerenderFileList() {
    // 保留选中项引用，重渲染后恢复高亮
    const selectedPath = selectedResult ? selectedResult.filePath : null;
    els.fileTbody.innerHTML = '';

    let list = allResults;
    if (sortState.col) {
      const key = sortState.col;
      const desc = sortState.desc;
      // 拷贝后排序，不破坏原始顺序
      list = allResults.slice().sort((a, b) => {
        let va = a[key], vb = b[key];
        // 日期对象用 valueOf 比较
        if (va instanceof Date) va = va.getTime();
        if (vb instanceof Date) vb = vb.getTime();
        if (typeof va === 'string') va = va.toLowerCase();
        if (typeof vb === 'string') vb = vb.toLowerCase();
        if (va < vb) return desc ? 1 : -1;
        if (va > vb) return desc ? -1 : 1;
        return 0;
      });
    }

    let selectedTr = null;
    list.forEach((r) => {
      appendFileRow(r);
      if (r.filePath === selectedPath) {
        selectedTr = els.fileTbody.lastChild;
        selectedTr.classList.add('selected');
      }
    });
    if (selectedTr) selectedTr.scrollIntoView({ block: 'nearest' });
  }

  function selectResult(r, tr) {
    document.querySelectorAll('#file-tbody tr.selected').forEach(x => x.classList.remove('selected'));
    tr.classList.add('selected');
    selectedResult = r;
    renderMatchList(r);
    renderPreview(r);
  }

  // ===== 匹配行列表 =====
  function renderMatchList(r) {
    els.matchCount.textContent = r.matchCount;
    els.matchFilename.textContent = r.fileName;
    els.matchList.innerHTML = '';
    r.matches.forEach((m) => {
      const div = document.createElement('div');
      div.className = 'match-item';
      // trim 行首空白以对齐桌面版 DisplayText；matchStart/matchEnd 是字符偏移，
      // leading 是前导空白字符数，同步减去修正高亮位置
      const leading = m.lineText.length - m.lineText.replace(/^\s+/, '').length;
      const trimmed = m.lineText.slice(leading);
      const lineHtml = H.highlightLine(trimmed, m.matchStart - leading, m.matchEnd - leading).replace(/\n$/, '');
      div.innerHTML =
        '<span class="ln">' + m.lineNumber + '</span>' + lineHtml;
      div.addEventListener('click', () => {
        document.querySelectorAll('.match-item.selected').forEach(x => x.classList.remove('selected'));
        div.classList.add('selected');
        scrollToLine(m.lineNumber);
      });
      div.addEventListener('dblclick', () => fd.openFile(r.filePath));
      els.matchList.appendChild(div);
    });
    if (r.matches[0]) {
      scrollToLine(r.matches[0].lineNumber);
    }
  }

  // ===== 文件预览 =====
  function renderPreview(r) {
    els.previewFilename.textContent = r.fileName;
    els.binaryHint.classList.add('hidden');
    els.truncateHint.classList.add('hidden');

    const { info, matches } = fd.previewFile(r.filePath, r.matches);
    if (info.isBinary) {
      els.previewCode.innerHTML = '';
      els.binaryHint.classList.remove('hidden');
      return;
    }
    if (info.content === null) {
      els.previewCode.innerHTML = '<span style="color:gray">无法读取文件</span>';
      return;
    }
    if (info.truncated) els.truncateHint.classList.remove('hidden');

    // 渲染全文 + 行号 + 主搜索匹配黄色高亮（按行匹配）
    els.previewCode.innerHTML = renderPreviewHtml(info.content, matches);
  }

  function renderPreviewHtml(content, matches) {
    // 按行渲染，匹配行高亮匹配片段
    const lines = content.split('\n');
    const matchByLine = new Map();
    for (const m of matches) matchByLine.set(m.lineNumber, m);

    // 行号列宽按最大行数位数动态计算（最小 2 字符宽），避免小文件行号列过宽
    const linenoCh = Math.max(2, String(lines.length).length);
    els.previewCode.style.setProperty('--lineno-ch', linenoCh + 'ch');

    let html = '';
    for (let i = 0; i < lines.length; i++) {
      const ln = i + 1;
      const line = lines[i];
      const m = matchByLine.get(ln);
      const lineHtml = m
        ? H.highlightLine(line, m.matchStart, m.matchEnd)
        : H.escapeHtml(line);
      // 行号（右对齐灰色，对齐桌面版 AvalonEdit ShowLineNumbers）+ data-line 供滚动定位
      html += '<div class="pv-line" data-line="' + ln + '">'
        + '<span class="pv-lineno">' + ln + '</span>'
        + '<span class="pv-content">' + lineHtml + '</span></div>';
    }
    return html;
  }

  function scrollToLine(lineNumber) {
    const node = els.previewCode.querySelector('.pv-line[data-line="' + lineNumber + '"]');
    if (node) node.scrollIntoView({ block: 'center', behavior: 'smooth' });
  }

  // ===== GridSplitter 拖拽（上下/左右）=====
  function bindSplitters() {
    bindSplit('splitter-h', 'file-panel', 'row');
    bindSplit('splitter-v', 'bottom-panel', 'col');
  }
  function bindSplit(splitterId, panelId, dir) {
    const splitter = $(splitterId);
    const panel = $(panelId);
    let dragging = false;
    splitter.addEventListener('mousedown', () => {
      dragging = true;
      document.body.style.cursor = dir === 'row' ? 'row-resize' : 'col-resize';
    });
    document.addEventListener('mousemove', (e) => {
      if (!dragging) return;
      if (dir === 'row') {
        const rect = panel.getBoundingClientRect();
        const ratio = (e.clientY - rect.top) / rect.height;
        if (ratio > 0.1 && ratio < 0.9) {
          const top = panel.querySelector('.table-wrap');
          const bottom = $('bottom-panel');
          top.style.flex = (ratio * 100) + ' 0 0';
          bottom.style.flex = ((1 - ratio) * 100) + ' 0 0';
          currentLayout.rowRatio = ratio;
        }
      } else {
        const rect = panel.getBoundingClientRect();
        const ratio = (e.clientX - rect.left) / rect.width;
        if (ratio > 0.1 && ratio < 0.9) {
          $('match-panel').style.flex = (ratio * 100) + ' 0 0';
          $('preview-panel').style.flex = ((1 - ratio) * 100) + ' 0 0';
          currentLayout.colRatio = ratio;
        }
      }
    });
    document.addEventListener('mouseup', () => {
      if (dragging) {
        dragging = false;
        document.body.style.cursor = '';
        saveLayout();
      }
    });
  }

  // ===== 表格列宽拖拽（对齐桌面版 DataGrid 可拖列宽）=====
  // th class → layout key 映射
  const COL_KEY = { 'col-name': 'colName', 'col-size': 'colSize', 'col-match': 'colMatch', 'col-path': 'colPath', 'col-mtime': 'colMtime' };
  function initColumnResize() {
    const ths = document.querySelectorAll('#file-table th');
    ths.forEach((th) => {
      const resizer = document.createElement('div');
      resizer.className = 'col-resizer';
      th.appendChild(resizer);

      let dragging = false;
      let startX = 0;
      let startW = 0;
      // 找到该 th 对应的 layout key
      const key = COL_KEY[Object.keys(COL_KEY).find((c) => th.classList.contains(c))];

      resizer.addEventListener('mousedown', (e) => {
        e.preventDefault();
        e.stopPropagation();
        dragging = true;
        startX = e.clientX;
        startW = th.offsetWidth;
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
      });

      document.addEventListener('mousemove', (e) => {
        if (!dragging) return;
        // table-layout:fixed 下，设 th 宽度即可生效
        const newW = Math.max(40, startW + (e.clientX - startX));
        th.style.width = newW + 'px';
        if (key) currentLayout.colWidths[key] = newW;
      });

      document.addEventListener('mouseup', () => {
        if (dragging) {
          dragging = false;
          document.body.style.cursor = '';
          document.body.style.userSelect = '';
          saveLayout();
        }
      });
    });
  }

  // ===== 布局持久化：把 currentLayout 存入 uTools dbStorage（extra 可合并额外字段）=====
  function saveLayout(extra) {
    const data = {
      rowRatio: currentLayout.rowRatio,
      colRatio: currentLayout.colRatio,
      colWidths: currentLayout.colWidths,
      previewWordWrap: currentLayout.previewWordWrap,
    };
    if (extra) Object.assign(data, extra);
    fd.saveLayout(data);
  }

  // ===== 右键菜单（浮动 DOM 菜单，对齐桌面版右键体验）=====
  let contextMenu = null;
  function showFileMenu(e, r) {
    e.preventDefault();
    e.stopPropagation();
    closeContextMenu();

    const items = [
      { label: '打开文件', action: () => fd.openFile(r.filePath) },
      { label: '在资源管理器中打开', action: () => fd.openInExplorer(r.filePath) },
      { sep: true },
      { label: '复制文件名', action: () => fd.copyText(r.fileName) },
      { label: '复制文件路径', action: () => fd.copyText(r.filePath) },
    ];

    contextMenu = document.createElement('div');
    contextMenu.className = 'ctx-menu';
    items.forEach((it) => {
      if (it.sep) {
        const sep = document.createElement('div');
        sep.className = 'ctx-sep';
        contextMenu.appendChild(sep);
        return;
      }
      const item = document.createElement('div');
      item.className = 'ctx-item';
      item.textContent = it.label;
      item.addEventListener('click', () => { it.action(); closeContextMenu(); });
      contextMenu.appendChild(item);
    });
    document.body.appendChild(contextMenu);

    // 定位到鼠标位置（防止超出右下边界）
    contextMenu.style.left = Math.min(e.clientX, window.innerWidth - 180) + 'px';
    contextMenu.style.top = Math.min(e.clientY, window.innerHeight - 160) + 'px';
  }

  function closeContextMenu() {
    if (contextMenu) { contextMenu.remove(); contextMenu = null; }
  }

  // 点击空白处关闭右键菜单
  document.addEventListener('click', closeContextMenu);
  document.addEventListener('contextmenu', (e) => {
    // 右键在菜单项以外的地方：关闭当前菜单（由具体行的 contextmenu 处理后再决定是否打开新的）
    if (!e.target.closest('.ctx-item')) closeContextMenu();
  }, true);

  // ===== 工具 =====
  function formatSize(bytes) {
    const KB = 1024;
    if (bytes < KB) return bytes + ' B';
    if (bytes < KB * 1024) return (bytes / KB).toFixed(1) + ' KB';
    if (bytes < KB * 1024 * 1024) return (bytes / (KB * 1024)).toFixed(1) + ' MB';
    return (bytes / (KB * 1024 * 1024)).toFixed(2) + ' GB';
  }
  function formatDate(d) {
    if (!(d instanceof Date) || isNaN(d)) return '';
    const p = (n) => String(n).padStart(2, '0');
    return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate())
      + ' ' + p(d.getHours()) + ':' + p(d.getMinutes());
  }

  document.addEventListener('DOMContentLoaded', init);
})();
