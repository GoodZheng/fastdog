# FastDog uTools 插件设计

> ## ⚠️ 架构演进说明（2026-07-16 更新）
>
> 本文档下方的「概述」及「跨平台 rg 二进制」等章节描述的是**初版设计**——捆绑 ripgrep 二进制 + `child_process.spawn` 调用。
>
> **实际发布版已废弃该方案**，原因：uTools 商店审核**禁止外部可执行文件**（包含捆绑的原生二进制），初版因此被拒。
>
> 当前实现改为**纯 JS 搜索引擎**（`fs/promises` 递归遍历 + `RegExp` 逐行匹配），详见：
> - 实现计划：`docs/superpowers/plans/2026-07-16-pure-js-search.md`
> - 变更记录：`plugins/utools-fastdog/CHANGELOG.md`（v1.0.0）
>
> 下方关于 `bin/rg-*`、`ripgrepBridge.js`、`platformRg.js`、`child_process.spawn` 的描述均已**不再适用**，仅作历史决策记录保留。当前搜索引擎为 `lib/jsSearchEngine.js`（含 Worker 多线程尝试 + 单线程 fallback + setImmediate 边搜边显示）。
>
> 本文档其余部分（UI 三层布局、功能需求、移植映射中除搜索引擎外的模块）仍然有效。

## 概述

为 FastDog 开发 uTools 插件，让用户在 uTools 平台内完成与桌面版等价的文本搜索体验。插件复用 FastDog 的核心搜索逻辑（ripgrep 桥接、参数构建、JSON 解析、结果聚合、文件预览），用 JavaScript 重写实现层，用 HTML/CSS/JS 还原桌面版布局，捆绑 ripgrep 二进制实现跨平台运行。

**范围边界**：对齐 FastDog 除「搜索历史」外的全部功能；UI 布局尽量还原桌面版三层结构（搜索条件区 → 文件列表 → 下半区左匹配行 + 右预览）。

## 背景

### uTools 插件技术本质（基于官方文档）

- 插件 = `plugin.json`（核心配置）+ `preload.js`（Node.js 16.x + Electron 渲染进程 + uTools API）+ 前端页面（任意框架）
- `preload.js` 遵循 CommonJS，可 `require("node:child_process")` 调用捆绑的二进制——这是复用 ripgrep 的技术基础
- 通过 `window.exports = {...}` 在 preload 暴露逻辑给 UI 层；`utools.onPluginEnter(({code, type, payload}) => {})` 是进入插件的核心生命周期回调
- 进入入口（`features[].cmds`）支持 6 型：`text`（关键字）、`regex`（正则匹配进入）、`over`（超级面板/选中内容）、`files`（文件/文件夹）、`img`、`window`
- 持久化能力：`utools.dbStorage`（类 localStorage）、`utools.db`（NoSQL）——本插件 MVP 不使用历史，但预留接口
- 系统能力：`utools.getPath('appData'/'userData'/'desktop')`、`utools.shellOpenPath()`、`utools.copyText()`、`utools.setSubInput()`

**核心可行性结论**：FastDog 的 C# 代码无法直接复用，但核心能力（参数构建、JSON 解析、rg 调用、结果聚合、文件预览）都是**确定性纯逻辑**，可 1:1 翻译成 JS。这是「逻辑重写 + 二进制复用」型移植。

### FastDog 能力盘点与移植映射

| FastDog 能力 | 实现位置 | 移植策略 | 本插件对应模块 |
|---|---|---|---|
| rg 参数构建 | `RipgrepBridge.BuildArguments` | 纯函数 1:1 翻译 | `lib/argumentBuilder.js` |
| 文件名模式归一化（`.cs`→`*.cs`） | `RipgrepBridge.NormalizeFilePattern` | 纯函数 1:1 翻译 | `lib/argumentBuilder.js` |
| 参数转义（`EscapeArg`） | `RipgrepBridge.EscapeArg` | 改用 Node `spawn` 数组传参，规避 shell 注入，无需手写转义 | `lib/ripgrepBridge.js` |
| rg JSON 解析（begin/match/end/summary、base64） | `RipgrepBridge.ParseRgLine` 等 | 纯函数 1:1 翻译 | `lib/jsonParser.js` |
| 进程管理与流式输出 | `RipgrepBridge.SearchAsync` | `child_process.spawn` + stdout 行流，回调推送 | `lib/ripgrepBridge.js` |
| 文件列表统计 | `CountFilesAsync` / `BuildFileListArguments` | `rg --files` 计数 | `lib/argumentBuilder.js` + `ripgrepBridge.js` |
| 结果聚合（文件→匹配行 + 日期过滤） | `SearchService` | JS 重写，回调推送 | `lib/searchService.js` |
| 日期过滤 | `SearchService.PassDateFilter` | 纯函数 | `lib/searchService.js` |
| 文件预览（二进制检测/截断/偏移） | `FilePreviewService` | JS 重写 | `lib/filePreview.js` |
| 字节偏移→字符偏移（UTF-8 多字节） | `FilePreviewService.ByteToCharOffset` | 用 `TextEncoder` + 切片计数 | `lib/filePreview.js` |
| 匹配文本高亮 | `TextMarkerService`（AvalonEdit 背景） | CSS `<mark>` 高亮 | `src/highlight.js` |
| 预览内二次查找（Ctrl+F） | `MainWindow.xaml.cs`（AvalonEdit SearchPanel） | 自建轻量查找栏 | `src/previewFind.js` |
| 主搜索高亮 | `TextMarkerService` 黄色 | CSS 黄色背景 | `src/highlight.js` |
| 搜索历史 | `SearchHistoryService` | **本版本不实现**（uTools 端无需） | — |

### 可移植的单元测试

`ArgumentBuilderTests`、`JsonParserTests`、`DateFilterTests`、`FilePreviewServiceTests` 的断言逻辑可直接移植为 JS 单测，保证移植正确性。

## 方案选型

### 1. 跨平台 rg 二进制

| 方案 | 说明 | 结论 |
|---|---|---|
| **A. 捆绑三平台 rg（已选）** | 从 ripgrep v14.1.1 release 下载 win32/darwin/linux x64 预编译包，按 `process.platform` 加载 | 保证与桌面版版本一致、离线可用 |
| B. 运行时要求系统已装 rg | 不捆绑，依赖 `rg` 在 PATH | 用户体验差，多数机器无 rg |
| C. 用 WASM 版 ripgrep | 无二进制，但 WASM 版功能受限/性能折损 | 不成熟，风险高 |

选定方案 A。`plugin.json` 的 `platform` 字段设为 `["win32","darwin","linux"]`。

### 2. UI 框架

| 方案 | 说明 | 结论 |
|---|---|---|
| A. Vue/React | 组件化、状态管理强 | 引入构建链，体积大，过度工程 |
| **B. 原生 HTML/CSS/JS（已选）** | 零依赖，逻辑在 preload，UI 仅渲染 | 轻量、符合 uTools 轻量插件定位，调试简单 |
| C. uTools 官方模板 | 仅列表型交互 | 无法还原桌面版左右分栏布局 |

选定方案 B。用原生 DOM 操作 + 轻量状态对象，配色与桌面版一致（主色 `#0e639c`，黄高亮 `rgba(255,255,0,0.4)`）。

### 3. 搜索词输入交互

| 方案 | 说明 | 结论 |
|---|---|---|
| A. uTools `setSubInput`（标题栏原生） | 符合 uTools 习惯，但与桌面版「搜索条件区」布局不一致 | — |
| **B. 还原桌面版搜索条件区（已选）** | 插件页面顶部含搜索路径 + 搜索内容 + 选项按钮行 | 布局与桌面版一致，用户零学习成本 |

选定方案 B。搜索词在插件页面内输入（仿 `SearchTextTextBox`），不使用 `setSubInput`，以最大化还原桌面版体验。但**利用 uTools 进入入口**（`over`/`files`/`regex` 型 payload）预填搜索词或路径。

## 功能需求

### 必须实现（对齐桌面版）

- **F1 搜索条件**（对应 `SearchQuery`）
  - 搜索路径、搜索内容
  - 搜索模式：正则表达式 / 纯文本（RadioButton 二选一）
  - 选项：区分大小写、全词匹配（CheckBox）
  - 文件名过滤（`*.cs;*.txt`，标签按钮内联编辑）
  - 目录排除（默认含 `.git`，标签按钮内联编辑）
  - 日期范围过滤（可折叠，含起止 DatePicker）

- **F2 路径设定**
  - 默认 `utools.getPath('desktop')` 或上次路径（用 `utools.dbStorage` 存储上次路径）
  - 进入入口 `type=files & fileType=directory` 时，`payload[0].path` 自动填为搜索路径
  - 「浏览...」按钮调用 uTools 原生目录选择（或 `utools.showOpenDialog`）

- **F3 文件列表**（对应 `ResultsGrid`）
  - 列：文件名、大小、匹配数（蓝色徽章 `#0e639c`）、路径、修改时间
  - 列表型虚拟滚动（大结果集时）；选中高亮（蓝色背景）
  - 右键菜单：打开文件、在资源管理器中打开、复制文件名、复制文件路径

- **F4 匹配行列表**（对应下半区左侧 `ListBox`）
  - 列：行号（右对齐灰）+ 匹配内容（Consolas 等宽，黄色高亮匹配片段）
  - 选中行：蓝色背景 + 橙色左边框，点击联动预览定位

- **F5 文件预览**（对应下半区右侧 `TextEditor`）
  - 全文显示 + 行号 + 语法高亮（用 highlight.js 按扩展名）
  - 主搜索匹配黄色背景标记（与匹配行联动，点击匹配行滚动定位）
  - 大文件（>5MB）截断（前 5000 行），提示「文件过大，仅显示部分内容」
  - 二进制文件提示「二进制文件，无法预览」
  - 自动换行开关
  - 预览内二次查找：Ctrl+F 唤起查找栏，橙色高亮，N/M 计数（自建轻量实现，替代 AvalonEdit SearchPanel）

- **F6 流式输出与取消**
  - rg 结果实时增量显示（stdout 逐行解析）
  - 「取消」按钮 / `onPluginOut` 时 kill 进程

- **F7 双击与拖放**
  - 双击匹配行：跳转该行号（调用 `utools.shellOpenPath` 或外部编辑器配置）
  - 双击文件行：打开文件
  - 拖放文件夹到页面：设为搜索路径（用 uTools 窗口的 HTML5 drag/drop）

- **F8 状态栏**
  - 状态文本 + 文件数 + 匹配数 + 耗时（蓝色高亮数字）

### 明确不实现

- 搜索历史 / 历史标签页 / 历史卡片（用户已明确排除）
- 会话恢复（依赖历史）
- 布局持久化（GridSplitter 比例、窗口位置）——uTools 窗口由宿主管理，插件不控制窗口尺寸

## 设计方案

### 目录结构

```
plugins/utools-fastdog/
├── plugin.json              # 核心配置（features/platform/main/preload/logo）
├── logo.png                 # 插件图标（复用 FastDog newLogo1.2）
├── preload.js               # Node 层入口（require child_process + window.exports）
├── lib/
│   ├── ripgrepBridge.js     # ← 移植 RipgrepBridge.cs（spawn + 进程管理 + kill）
│   ├── argumentBuilder.js   # ← 移植 BuildArguments/NormalizeFilePattern/BuildFileListArguments
│   ├── jsonParser.js        # ← 移植 ParseRgLine（begin/match/end/summary/base64）
│   ├── searchService.js     # ← 移植 SearchService.cs（聚合 + 日期过滤 + 事件回调）
│   ├── filePreview.js       # ← 移植 FilePreviewService.cs（二进制检测/截断/偏移转换）
│   └── platformRg.js        # 跨平台 rg 二进制定位 + 权限修正
├── index.html               # UI 入口（还原三层布局）
├── src/
│   ├── app.js               # UI 渲染逻辑（文件列表/匹配行/预览/状态栏）
│   ├── style.css            # 蓝色主题，对齐桌面版配色
│   ├── highlight.js         # 主搜索匹配黄色高亮 + 行内高亮
│   └── previewFind.js       # 预览内二次查找（Ctrl+F 查找栏 + N/M 计数）
├── vendor/
│   └── highlightjs/         # 语法高亮库（本地，离线）
├── bin/                     # 捆绑的 ripgrep（平台二进制）
│   ├── rg-win-x64.exe
│   ├── rg-darwin-x64
│   └── rg-linux-x64
└── test/
    ├── argumentBuilder.test.js   # ← 移植 ArgumentBuilderTests
    ├── jsonParser.test.js        # ← 移植 JsonParserTests
    ├── dateFilter.test.js        # ← 移植 DateFilterTests
    └── filePreview.test.js       # ← 移植 FilePreviewServiceTests
```

### `plugin.json` 核心配置

```jsonc
{
  "main": "index.html",
  "preload": "preload.js",
  "logo": "logo.png",
  "platform": ["win32", "darwin", "linux"],
  "features": [
    {
      "code": "fastdog",
      "explain": "FastDog — 基于 ripgrep 的文本搜索",
      "cmds": ["fd", "fastdog", "搜索", "文本搜索"],
      "main": "index.html"
    },
    {
      "code": "fastdog-search-text",
      "explain": "用 FastDog 搜索该文本",
      "cmds": [{ "type": "over", "label": "FastDog 搜索选中内容", "minLength": 1 }],
      "main": "index.html"
    },
    {
      "code": "fastdog-search-dir",
      "explain": "在此文件夹中搜索",
      "cmds": [{ "type": "files", "fileType": "directory", "name": "文件夹" }],
      "main": "index.html"
    }
  ]
}
```

- `code=fastdog`（`type` 默认关键字）：输入 `fd`/`fastdog` 唤起，页面内输入条件
- `code=fastdog-search-text`（`type=over`）：超级面板对选中文本搜索，`payload` 预填到搜索内容框
- `code=fastdog-search-dir`（`type=files & fileType=directory`）：文件管理器选中文件夹后进入，`payload[0].path` 预填到搜索路径框

### 分层架构（对应 FastDog 三层）

```
UI 层 (index.html + src/app.js)          ← 对应 WPF MainWindow
        ↑ 调用 window.exports.* 方法
preload 层 (preload.js + lib/)           ← 对应 SearchService + RipgrepBridge
        ↑ child_process.spawn(rgPath, args)
rg 二进制 (bin/rg-*)                     ← 对应 tools/rg.exe
```

> ⚠️ **注意 `window.exports` 的适用范围**：`window.exports[features.code] = { mode, args }` 是**模板插件**（无 `main` 字段）专用的结构，文档明确"使用模板插件时无法同时启用自定义界面"。本插件有 `main` 字段（自定义 UI），**不走模板模式**：进入逻辑用 `utools.onPluginEnter` 处理，Node 能力用普通 `window` 属性暴露给前端页面。

`preload.js` 暴露的 API 契约（挂到 `window.fastdog`）：

```js
// preload.js
const fastdog = {
  // 搜索：query 对应 SearchQuery；回调流式推送结果
  search: (query, { onResult, onStats, onDone, onError }) => {},
  cancel: () => {},                          // kill 进程
  previewFile: (filePath) => ({ content, truncated, isBinary, lineLengths }),
  openFile: (filePath) => {},                // utools.shellOpenPath
  openInExplorer: (filePath) => {},          // utools.shellOpenPath(所在目录)
  copyText: (text) => {},                    // utools.copyText
  selectDirectory: () => {},                 // utools.showOpenDialog
  getLastPath: () => {},                     // utools.dbStorage.getItem
  saveLastPath: (path) => {},                // utools.dbStorage.setItem
};
window.fastdog = fastdog;   // 暴露给前端页面（src/app.js 用 window.fastdog.* 调用）

// 进入逻辑：区分 3 个入口
utools.onPluginEnter(({ code, type, payload }) => {
  // code=fastdog：空白进入；code=fastdog-search-text：预填搜索词；
  // code=fastdog-search-dir：预填搜索路径
});
```

### UI 布局（还原桌面版三层）

`index.html` 用纯 HTML/CSS 还原 `MainWindow.xaml` 的三层结构，配色一致：

```
┌─ Header：搜索条件区（白底 #fff，下边框 #e0e0e0）──────────────┐
│ 搜索路径  [_______________] [浏览...]                          │
│ 搜索内容  [_______________]                                    │
│ [正则表达式][纯文本] [区分大小写][全词匹配] | [文件过滤][排除目录] | [📅 日期范围]   [搜索][取消]│
├─ Content（上下分割，2:3）─────────────────────────────────────┤
│ ┌─ 文件列表（DataGrid 风格）───────────────────────────────┐ │
│ │ 文件名 | 大小 | 匹配数(蓝徽章) | 路径 | 修改时间            │ │
│ ├───────────────────── GridSplitter ──────────────────────┤ │
│ │ ┌匹配行(左 35%)────┬─GridSplitter─┬文件预览(右 65%)──────┐│ │
│ │ │ 匹配行 N — file   │              │ 文件预览 — file  [自动换行]││ │
│ │ │ 123  const x=... │              │ 1  const x = 1;       ││ │
│ │ │ 456  function... │              │ 2  ...                ││ │
│ │ └──────────────────┴──────────────┴───────────────────────┘│ │
├─ 状态栏（#faf8f3）─────────────────────────────────────────────┤
│ 状态文本...                          文件: N  匹配: M  耗时: Xs │
└────────────────────────────────────────────────────────────────┘
```

- GridSplitter 用可拖拽的 `<div class="splitter">` + mousedown/mousemove 实现，比例存 `utools.dbStorage`
- 文件列表用 `<table>` + 虚拟滚动（仅渲染可视行，避免万级结果卡顿）
- 匹配行/预览用 `<pre>` + `<code>`，等宽字体 Consolas/Monaco

### 关键移植细节

#### 1. 参数构建（`argumentBuilder.js`）

`BuildArguments` 逐行翻译，但输出从「拼接字符串」改为「参数数组」交给 `spawn`，省去 `EscapeArg`：

```js
function buildArgs(query) {
  const args = ['--json', '--no-heading', '--stats'];
  if (!query.caseSensitive) args.push('-i');
  if (!query.isRegex) args.push('-F');
  if (query.wholeWord) args.push('-w');
  buildFilterArgs(args, query.fileFilter, query.excludeDirs);
  args.push(query.searchText, query.searchPath);
  return args;
}
```

`NormalizeFilePattern` 逻辑（`.cs`→`*.cs`、`cs`→`*.cs`、含通配符原样返回）1:1 翻译。

#### 2. JSON 解析（`jsonParser.js`）

`ParseRgLine` 翻译：`JSON.parse` + 按 `type` 分支（begin/match/end/summary）。base64 字段（`data.lines.bytes` / `data.path.bytes`）用 `Buffer.from(str, 'base64').toString('utf8')` 还原（对应 C# 的 `Convert.FromBase64String` + `Encoding.UTF8.GetString`）。

#### 3. 进程管理（`ripgrepBridge.js`）

用 `spawn(rgPath, args)`，stdout 按行分割（`readline` 模块），每行交给 `jsonParser`。`cancel()` 调 `child.kill('SIGTERM')`（对应 C# `KillProcess`）。

```js
const { spawn } = require('node:child_process');
const { createInterface } = require('node:readline');

class RipgrepBridge {
  search(args, { onEvent, onDone, onError }) {
    const child = spawn(resolveRgPath(), args, { windowsHide: true });
    this._child = child;
    const rl = createInterface({ input: child.stdout });
    rl.on('line', line => { const ev = parseRgLine(line); if (ev) onEvent(ev); });
    child.on('close', code => onDone(code));
    child.on('error', err => onError(err));
  }
  cancel() { try { this._child?.kill('SIGTERM'); } catch {} }
}
```

#### 4. 结果聚合与日期过滤（`searchService.js`）

移植 `SearchService`：用 `Map` 暂存文件的匹配行（`FileEnd` 时触发日期过滤，通过则回调）。日期过滤逻辑（`from`/`to` 比较 `LastModified`）1:1 翻译。

#### 5. 文件预览（`filePreview.js`）

移植 `FilePreviewService`：
- `BinaryExtensions` 集合直接复制
- `LoadFileContent`：`fs.readFileSync`，>5MB 读前 5000 行
- `ByteToCharOffset`：用 `new TextEncoder().encode(text).slice(0, byteOffset)` + `new TextDecoder().decode()` 得到字符数

```js
function byteToCharOffset(text, byteOffset) {
  if (byteOffset <= 0) return 0;
  if (!text) return byteOffset;
  const bytes = new TextEncoder().encode(text);
  if (byteOffset >= bytes.length) return text.length;
  return new TextDecoder().decode(bytes.slice(0, byteOffset)).length;
}
```

#### 6. 预览内二次查找（`previewFind.js`）

桌面版用 AvalonEdit 内置 `SearchPanel`，Web 端无对应，自建轻量查找栏：
- Ctrl+F 唤起浮动查找栏（输入框 + 上/下一个 + 关闭 + N/M 计数）
- 在预览的全文中查找，命中项橙色背景（`#ff9900`），当前项深橙（`#e67300`）+白字
- 与主搜索黄色高亮物理隔离（不同 DOM 层 / 不同 class）

### 跨平台 rg 二进制策略（`platformRg.js`）

```js
function resolveRgPath() {
  const map = {
    win32: 'bin/rg-win-x64.exe',
    darwin: 'bin/rg-darwin-x64',
    linux:  'bin/rg-linux-x64',
  };
  const rel = map[process.platform];
  if (!rel) throw new Error(`不支持的平台: ${process.platform}`);
  const p = path.join(__dirname, '..', rel);
  if (process.platform !== 'win32') {
    try { fs.chmodSync(p, 0o755); } catch {} // 确保可执行
  }
  return p;
}
```

二进制来源：[ripgrep v14.1.1 release](https://github.com/BurntSushi/ripgrep/releases/tag/14.1.1) 对应平台预编译包，版本与 `tools/rg.exe` 对齐。

> ⚠️ macOS 未签名二进制首次运行可能被 Gatekeeper 拦截，文档需提示 `xattr -d com.apple.quarantine <path>` 或首次通过系统弹窗放行。

## 改动范围

### 新增（本插件为独立子项目，不改动现有 FastDog C# 代码）

- `plugins/utools-fastdog/` 整个目录（见「目录结构」）
- `docs/superpowers/plans/2026-07-15-utools-plugin.md`（实现计划）

### 不改动

- `src/FastDog/**`：桌面版 C# 代码完全不动，插件是平行的 JS 实现
- `tools/rg.exe`：桌面版继续用；插件自带 `bin/rg-*`
- 主仓库 `CHANGELOG.md`：插件不纳入主变更记录
- `plugins/utools-fastdog/CHANGELOG.md`：插件独立维护版本号（语义化版本，遵循 uTools 规范）与变更记录

## 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| macOS rg 未签名 | darwin 首次运行被 Gatekeeper 拦截 | 文档提示 `xattr -d`；考虑上架时签名 |
| uTools 市场审核 | 二进制体积（约 15MB）、安全审查 | 先本地开发调试，上架前评估 |
| 大结果集渲染卡顿 | 万级文件/匹配行卡 UI | 文件列表虚拟滚动；匹配行按需渲染 |
| arm64（Apple Silicon） | 仅 x64 二进制，M 系列芯片走 Rosetta | MVP 先 x64；后续补 `rg-darwin-arm64` |
| rg 中文路径/搜索词 | 编码问题 | spawn 用 UTF-8，rg 原生支持 |
| 字节/字符偏移转换 | UTF-8 多字节字符定位偏差 | 单测覆盖中文场景，移植 `FilePreviewServiceTests` |

## 验收标准

- 3 个进入入口（关键字 / 超级面板选中文本 / 文件夹）均能进入并正确预填
- 搜索条件全部生效（正则/纯文本/大小写/全词/文件过滤/目录排除/日期范围）
- 文件列表、匹配行、预览三层布局与桌面版视觉一致
- 流式输出 + 取消正常工作
- 大文件截断、二进制提示、预览内 Ctrl+F 查找均可用
- 三平台 rg 二进制正确加载并搜索
- `test/` 下 4 组单测全部通过，断言与 C# 版一致
