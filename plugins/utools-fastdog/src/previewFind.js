// 预览内二次查找：Ctrl+F 唤起，在预览全文中查找，橙色高亮，N/M 计数。
// 对应桌面版 AvalonEdit SearchPanel 的 Web 端轻量实现。

(function () {
  'use strict';
  let matches = [];      // 增量计数用（行内匹配数累计）
  let currentIdx = -1;

  function init() {
    const findBar = document.getElementById('preview-find');
    const previewCode = document.getElementById('preview-code');

    // 动态生成查找栏内容（index.html 里只有空容器）
    findBar.innerHTML =
      '<input type="text" id="find-input" placeholder="查找..." />' +
      '<span class="counter" id="find-counter">0/0</span>' +
      '<button id="find-prev">↑</button>' +
      '<button id="find-next">↓</button>' +
      '<button id="find-close">✕</button>';

    const input = document.getElementById('find-input');
    const counter = document.getElementById('find-counter');
    const prevBtn = document.getElementById('find-prev');
    const nextBtn = document.getElementById('find-next');
    const closeBtn = document.getElementById('find-close');

    // Ctrl+F 唤起 / Esc 关闭
    document.addEventListener('keydown', (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
        e.preventDefault();
        open();
      }
      if (e.key === 'Escape') close();
    });

    function open() {
      findBar.classList.remove('hidden');
      input.focus();
      input.select();
    }
    function close() {
      findBar.classList.add('hidden');
      clearHighlights();
      counter.textContent = '0/0';
      matches = [];
      currentIdx = -1;
    }

    input.addEventListener('input', () => doFind(input.value));
    prevBtn.addEventListener('click', () => navigate(-1));
    nextBtn.addEventListener('click', () => navigate(1));
    closeBtn.addEventListener('click', close);
    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') { e.preventDefault(); navigate(e.shiftKey ? -1 : 1); }
    });

    function doFind(query) {
      clearHighlights();
      matches = [];
      currentIdx = -1;
      if (!query) { counter.textContent = '0/0'; return; }

      const qLower = query.toLowerCase();
      const lines = previewCode.querySelectorAll('.pv-line');
      let total = 0;
      lines.forEach((lineNode) => {
        const original = lineNode.textContent;
        const lower = original.toLowerCase();
        let idx = lower.indexOf(qLower);
        if (idx === -1) return;
        // 重建 innerHTML，插入 mark
        let html = '';
        let last = 0;
        while (idx !== -1) {
          html += escapeHtml(original.slice(last, idx))
            + '<mark class="find-hl">' + escapeHtml(original.slice(idx, idx + query.length)) + '</mark>';
          last = idx + query.length;
          idx = lower.indexOf(qLower, last);
          total++;
        }
        html += escapeHtml(original.slice(last));
        lineNode.innerHTML = html;
      });
      matches = new Array(total);

      if (total > 0) {
        currentIdx = 0;
        markCurrent();
      }
      counter.textContent = (total === 0 ? 0 : currentIdx + 1) + '/' + total;
    }

    function clearHighlights() {
      // 清除查找高亮（保留主搜索 match-hl）。把 mark 还原为文本
      previewCode.querySelectorAll('.find-hl').forEach((m) => {
        const parent = m.parentNode;
        parent.replaceChild(document.createTextNode(m.textContent), m);
        parent.normalize();
      });
    }

    function navigate(dir) {
      const all = previewCode.querySelectorAll('.find-hl');
      if (all.length === 0) return;
      currentIdx = (currentIdx + dir + all.length) % all.length;
      // 更新 current 标记
      previewCode.querySelectorAll('.find-hl.current').forEach((m) => m.classList.remove('current'));
      markCurrent();
      counter.textContent = (currentIdx + 1) + '/' + all.length;
    }

    function markCurrent() {
      const all = previewCode.querySelectorAll('.find-hl');
      if (all[currentIdx]) {
        all[currentIdx].classList.add('current');
        all[currentIdx].scrollIntoView({ block: 'center', behavior: 'smooth' });
      }
    }
  }

  function escapeHtml(s) {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }

  document.addEventListener('DOMContentLoaded', init);
})();
