// UI 主逻辑：搜索条件读写、调用 window.fastdog、渲染文件列表/匹配行/预览。
// 仅通过 window.fastdog.* 和 utools.* 调能力。

(function () {
  'use strict';
  const fd = window.fastdog;
  const H = window.FastDogHighlight;
  let allResults = [];        // 当前搜索结果
  let selectedResult = null;  // 当前选中文件

  // ===== DOM 引用 =====
  const $ = (id) => document.getElementById(id);
  const els = {
    path: $('search-path'), text: $('search-text'),
    browse: $('btn-browse'), search: $('btn-search'), cancel: $('btn-cancel'),
    modeRegex: $('mode-regex'), modeText: $('mode-text'),
    optCase: $('opt-case'), optWord: $('opt-word'),
    tagFileFilter: $('tag-filefilter'), inputFileFilter: $('input-filefilter'),
    tagExclude: $('tag-exclude'), inputExclude: $('input-exclude'),
    dateToggle: $('date-toggle'), dateFrom: $('date-from'), dateTo: $('date-to'),
    dateSep: $('date-sep'),
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
    els.path.value = fd.getLastPath() || '';
    bindOptions();
    bindButtons();
    bindSplitters();
    bindDragDrop();

    // uTools 进入事件（preload 派发 fastdog:enter）
    window.addEventListener('fastdog:enter', (e) => {
      applyEnter(e.detail || window.__fastdogEnter || {});
    });
    // 若 preload 已先于本脚本执行设置过 enter
    if (window.__fastdogEnter) applyEnter(window.__fastdogEnter);
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
    // 默认值
    els.inputFileFilter.value = '*.cs;*.txt';
    els.inputExclude.value = 'node_modules';
    refreshTag(els.tagFileFilter, els.inputFileFilter, '文件: ');
    refreshTag(els.tagExclude, els.inputExclude, '排除: ');

    // 日期折叠
    els.dateToggle.addEventListener('change', () => {
      const show = els.dateToggle.checked;
      els.dateFrom.classList.toggle('hidden', !show);
      els.dateTo.classList.toggle('hidden', !show);
      els.dateSep.classList.toggle('hidden', !show);
    });

    // 自动换行
    els.optWrap.addEventListener('change', () => {
      els.previewCode.classList.toggle('wrap', els.optWrap.checked);
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
    btn.textContent = prefix + (input.value || '');
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
      dateFilterEnabled: els.dateToggle.checked,
      dateFrom: els.dateFrom.value ? new Date(els.dateFrom.value) : null,
      dateTo: els.dateTo.value ? new Date(els.dateTo.value) : null,
    };
  }

  // ===== 搜索 =====
  function doSearch() {
    const q = buildQuery();
    if (!q.searchPath) { setStatus('请先选择搜索路径'); return; }
    if (!q.searchText) { setStatus('请输入搜索内容'); return; }

    fd.saveLastPath(q.searchPath);
    allResults = [];
    clearUI();
    setStatus('搜索中...');

    fd.search(q, {
      onResult: (r) => { allResults.push(r); appendFileRow(r); },
      onStats: (s) => {
        els.statFiles.textContent = s.searchedFiles;
        els.statMatches.textContent = s.totalMatches;
        els.statElapsed.textContent = s.elapsed;
        setStatus('完成：找到 ' + s.foundFiles + ' 个文件，' + s.totalMatches + ' 处匹配');
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

  // ===== 文件列表行 =====
  function appendFileRow(r) {
    const tr = document.createElement('tr');
    tr.innerHTML =
      '<td class="col-name">' + H.escapeHtml(r.fileName) + '</td>' +
      '<td class="col-size">' + formatSize(r.fileSize) + '</td>' +
      '<td class="col-match"><span class="match-badge">' + r.matchCount + '</span></td>' +
      '<td class="col-path">' + H.escapeHtml(r.filePath) + '</td>' +
      '<td class="col-mtime">' + formatDate(r.lastModified) + '</td>';
    tr.addEventListener('click', () => selectResult(r, tr));
    tr.addEventListener('dblclick', () => fd.openFile(r.filePath));
    tr.addEventListener('contextmenu', (e) => showFileMenu(e, r));
    els.fileTbody.appendChild(tr);
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
      const lineHtml = H.highlightLine(m.lineText, m.matchStart, m.matchEnd).replace(/\n$/, '');
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

    let html = '';
    for (let i = 0; i < lines.length; i++) {
      const ln = i + 1;
      const line = lines[i];
      const m = matchByLine.get(ln);
      const lineHtml = m
        ? H.highlightLine(line, m.matchStart, m.matchEnd)
        : H.escapeHtml(line);
      // data-line 供滚动定位
      html += '<div class="pv-line" data-line="' + ln + '">' + lineHtml + '</div>';
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
        }
      } else {
        const rect = panel.getBoundingClientRect();
        const ratio = (e.clientX - rect.left) / rect.width;
        if (ratio > 0.1 && ratio < 0.9) {
          $('match-panel').style.flex = (ratio * 100) + ' 0 0';
          $('preview-panel').style.flex = ((1 - ratio) * 100) + ' 0 0';
        }
      }
    });
    document.addEventListener('mouseup', () => {
      if (dragging) {
        dragging = false;
        document.body.style.cursor = '';
      }
    });
  }

  // ===== 右键菜单 =====
  function showFileMenu(e, r) {
    e.preventDefault();
    const action = ['打开文件', '在资源管理器中打开', '复制文件名', '复制文件路径'];
    utools.showMessageBox({
      type: 'question',
      title: '文件操作',
      message: r.fileName,
      buttons: action,
    }, (idx) => {
      if (idx === 0) fd.openFile(r.filePath);
      else if (idx === 1) fd.openInExplorer(r.filePath);
      else if (idx === 2) fd.copyText(r.fileName);
      else if (idx === 3) fd.copyText(r.filePath);
    });
  }

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
