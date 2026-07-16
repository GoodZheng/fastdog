# 全文检索（uTools 插件）开发文档

本插件是 FastDog 的 uTools 版本，提供基于纯 JS 的文件内容全文检索。
本文档供开发维护参考，记录架构、关键契约、已知坑与决策背景。

---

## 一、目录结构

```
plugins/utools-fastdog/
├── plugin.json              # uTools 插件配置（features/platform/version/logo）
├── preload.js               # uTools 装配层：require lib，暴露 window.fastdog，处理 onPluginEnter
├── index.html               # UI 入口（还原桌面版三层布局）
├── logo.png                 # 插件图标（256x256）
├── package.json             # Node 配置（type:commonjs + ignore 依赖）
├── CHANGELOG.md             # 版本变更记录（独立于主仓库）
├── DEVELOPMENT.md           # 本文档
├── lib/                     # 纯逻辑层（可单测，不依赖 uTools/Electron）
│   ├── jsSearchEngine.js    # 搜索引擎（child_process.fork 多进程 + 串行 fallback）
│   ├── searchChild.js       # fork 子进程脚本（接收文件批次，回传匹配）
│   ├── matcher.js           # 匹配器工厂（RegExp，统一 findAll(line) 接口）
│   ├── gitignoreFilter.js   # .gitignore + excludeDirs 过滤（基于 ignore 包）
│   ├── argumentBuilder.js   # 文件名模式归一化（.cs→*.cs），normalizeFilePattern
│   ├── searchService.js     # 结果聚合（rg 事件流 → 文件→匹配行 Map）+ 日期过滤
│   ├── filePreview.js       # 文件预览（二进制检测/截断/行长度）+ BINARY_EXTENSIONS
│   └── jsonParser.js        # rg --json 解析（ripgrep 时代遗留，纯 JS 引擎不再调用，保留无害）
├── src/                     # Web UI 层（通过 window.fastdog.* 调能力）
│   ├── app.js               # 主逻辑（搜索/渲染/排序/列宽/会话恢复/右键菜单）
│   ├── style.css            # 蓝色主题，对齐桌面版配色
│   ├── highlight.js         # 匹配文本黄色高亮渲染
│   └── previewFind.js       # 预览内 Ctrl+F 二次查找（橙色高亮 + N/M 计数）
└── test/                    # 单元测试（node:test，41 例）
    ├── matcher.test.js      # 匹配器（6 例）
    ├── jsSearchEngine.test.js  # 搜索引擎（12 例）
    ├── argumentBuilder.test.js # 参数构建（6 例）
    ├── jsonParser.test.js   # rg JSON 解析（6 例）
    ├── dateFilter.test.js   # 日期过滤（4 例）
    └── filePreview.test.js  # 文件预览（7 例）
```

## 二、分层架构

```
UI 层 (index.html + src/*.js)          原生 DOM，window.fastdog.* 调能力
        ↑
preload 层 (preload.js)                uTools API 胶水 + 暴露 window.fastdog
        ↑
引擎层 (lib/jsSearchEngine.js)         fork 多进程并行 / 串行 fallback
        ↑
子进程 (lib/searchChild.js)            fs.readFile + matcher.findAll
```

**关键边界**：
- `lib/*.js` 是纯逻辑，零 uTools/Electron 依赖，可用 `npm test` 直接单测
- `preload.js` 是唯一接触 uTools API（utools.onPluginEnter / dbStorage / shellOpenPath）的地方
- `src/*.js` 只通过 `window.fastdog.*` 调能力，不直接 require

## 三、数据流契约（不可破坏）

搜索引擎通过 `handlers.onEvent(ev)` 推送的事件类型：

```js
{ type: 'scanned', searchedFiles }                              // 遍历完成，文件数
{ type: 'fileBegin', filePath }
{ type: 'match', filePath, lineNumber(1-based), lineText(含\n),
  matchStart(字符偏移), matchEnd(字符偏移) }                     // ⚠️ 字符偏移，非字节
{ type: 'fileEnd', filePath }
{ type: 'summary', totalMatches, matchedLines, elapsed, searchedFiles }
```

**⚠️ matchStart/matchEnd 是字符偏移**（直接用 RegExp 的 m.index）。
这是 v1.1.0 的变更——废除了 ripgrep 时代遗留的字节偏移契约。
- 生产端：matcher.findAll 返回 {start,end} 是 m.index（字符偏移）
- 消费端：highlight.js 直接 slice(text, start, end)，无 byteToChar 转换

## 四、搜索引擎演进与决策（重要，避免重蹈覆辙）

### 当前方案：child_process.fork 多进程并行（v1.1.0）
- fork N 个独立 Node 子进程（N=min(4,CPU/4)），动态分发 500 文件/批
- 实测 5.5 万文件：串行 6s → fork 3.2s
- fork 失败自动降级串行（forkRuntimeOk 标志）

### 已废弃的方案（记录失败原因，勿重复尝试）

| 方案 | 结果 | 失败原因 |
|---|---|---|
| **捆绑 ripgrep 二进制** | ❌ 审核被拒 | uTools 禁止外部可执行文件 |
| **worker_threads** | ❌ 运行时报错 | uTools 渲染进程 "V8 platform does not support creating Workers" |
| **fs.promises.readFile 异步并发池** | ❌ uTools 里更慢 | Electron 事件循环重，微任务调度开销超过 IO 并行收益（8-9s vs 串行 6s） |
| **indexOf 纯文本快车道** | ❌ 中文更慢 | toLowerCase 对中文是纯开销，比 RegExp(i) 慢 2 倍 |

### 关键教训
- **纯 Node 的基准不能代表 uTools/Electron 环境**：异步池在纯 Node 快 30%，在 uTools 里反而慢 40%。任何性能优化必须在 uTools 实测。
- **fork 的审核风险**：fork 产生独立 node 子进程（任务管理器可见），可能被审核判为"外部可执行"。已做降级保护。

## 五、性能相关

### 当前性能（实测，E:/demo/net/DataUpload，5.5 万文件）
| 阶段 | 串行 | fork 4 进程 |
|---|---|---|
| 目录遍历 | 0.5s | 0.5s |
| 文件读取+匹配 | 5.5s | 2.7s |
| **总计** | **6s** | **3.2s** |

### 瓶颈分析
- 79% 时间在 `readFile`（IO 密集），18% 在正则匹配，3% 在遍历
- fork 有效是因为：独立 OS 进程，CPU+IO 都真并行，不受主进程事件循环拖累

### 用户可做的提速（不改代码）
- excludeDirs 排掉发布产物目录（如 `Publish;publish_f`），直接少读几万文件
- fileFilter 限定文件类型（如只搜 `*.cs`）

## 六、常见问题排查

### 搜索结果出现 .dll 等二进制文件
检查 `lib/filePreview.js` 的 `BINARY_EXTENSIONS` 集合——遍历阶段按扩展名跳过。
若仍有二进制混入，是扩展名不在集合里，补充即可。

### 预览高亮位置错位
检查 matchStart/matchEnd 是否字符偏移。
- matcher.findAll 必须返回 m.index（字符偏移）
- highlight.js 直接 slice，不能有 byteToChar 转换
- app.js renderMatchList 的 `m.matchStart - leading`：leading 是前导空白字符数

### fork 失败/不生效
- `process.env.FD_NO_FORK=1` 强制禁用 fork（调试用）
- 检查 searchChild.js 路径（path.join(__dirname, 'searchChild.js')）
- 子进程 require('./matcher') 是相对 searchChild.js 的

### UI 不显示/卡住
- 检查 preload.js 是否暴露了 window.fastdog
- 串行模式靠 setImmediate 每 500 文件让出事件循环；fork 模式靠 IPC 回传
- scanned 事件必须推（状态栏显示文件数）

## 七、开发与测试

```bash
cd plugins/utools-fastdog
npm install        # 安装 ignore 依赖
npm test           # 运行全部单测（41 例）
node --check lib/jsSearchEngine.js  # 语法检查
```

### 性能调试环境变量
- `FD_NO_FORK=1`：禁用 fork，强制串行（对比基准用）
- `FD_NO_WORKER=1`：历史遗留，禁用 worker（已废弃）

### 在 uTools 调试
1. 开发者工具 → 新建项目 → 选 plugin.json
2. 每次进入插件加载最新代码
3. preload.js 在 uTools 环境运行（有 utools/window 全局），node --test 不加载它
