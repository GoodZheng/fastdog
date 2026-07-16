# Changelog

本插件版本号与变更记录独立维护，不纳入主仓库 FastDog 的 CHANGELOG。
格式遵循 Keep a Changelog + 语义化版本。

## [1.1.0] - 未发布

### 新增
- child_process.fork 多进程并行搜索：fork 独立 Node 子进程真并行，实测 5.5 万文件 6s→3.2s（约 2 倍）
  - 子进程脚本 `lib/searchChild.js`，IPC 通信（process.send/on('message')），动态分发 500 文件/批
  - 进程数 = min(4, CPU/4)，cancel 时 SIGTERM 杀子进程
  - fork 运行时不可用时自动降级串行（forkRuntimeOk 标志）

### 性能优化
- matchStart/matchEnd 改为字符偏移（直接用 RegExp m.index），废除 ripgrep 时代遗留的字节偏移契约
  - 消除每个匹配两次 TextEncoder.encode() 的 O(匹配数×行长) 双重转换
  - 上层 highlight.js/filePreview/preload 直接 slice，删 byteToChar/byteToCharOffset 调用
- 遍历阶段按扩展名跳过二进制文件（.dll/.png/.pdb 等不读内容），对齐 ripgrep
- search 内遍历后通过 scanned 事件回传文件数，删除 countFiles 预遍历（目录树只遍历一次）
- 新增 matcher 模块统一匹配器工厂

### 废弃
- 删除 searchWorker.js（worker_threads 版），uTools 渲染进程不支持创建 Worker
- 删除异步 IO 并发池（fs.promises.readFile 并发16），实测 uTools/Electron 事件循环重，微任务调度开销超过 IO 并行收益（8-9s 反而比串行 6s 慢）
- 回退 indexOf 纯文本快车道，实测中文场景比 RegExp 慢 2 倍（toLowerCase 对中文是纯开销）

### 保留
- 全部 UI 与功能不变（三层布局、搜索条件、预览高亮、Ctrl+F、排序、列宽、会话恢复、右键菜单等）

## [1.0.0] - 2026-07-16

### 变更
- 搜索引擎从捆绑 ripgrep 二进制改为纯 JS 实现（fs + RegExp），以通过 uTools 商店审核（禁止外部可执行文件）
- 新增 .gitignore 解析（基于 ignore 包），自动跳过被忽略的文件，对齐 ripgrep 体验
- 移除 bin/ 下三平台 rg 二进制、ripgrepBridge、platformRg
- 插件名改为「全文检索」，主入口关键字改为 搜索/全文搜索/文本搜索/内容搜索

### 性能优化
- 二进制文件按扩展名在遍历阶段预过滤（.dll/.png/.pdb 等不读内容不搜索，对齐 ripgrep）
- 二进制内容检测（前 8KB 扫描 NUL 字节），双重保障避免二进制误匹配
- 文件列表批量渲染（requestAnimationFrame 攒批 + 一次性 innerHTML），避免逐次 appendChild 重排
- 事件委托（文件表格点击/双击/右键统一委托到 tbody，消除每行 3 个监听器）
- 搜索引擎尝试 Worker 多线程并行（32 核环境理论可降至 1/8 耗时）；uTools 渲染进程不支持时自动降级单线程
- 单线程搜索用 setImmediate 定期让出事件循环（每 500 文件一次），实现边搜边显示，总耗时几乎无增加

### 保留
- 全部 UI 与功能不变（三层布局、正则/大小写/全词/文件过滤/目录排除、预览高亮、Ctrl+F 查找、排序、列宽、会话恢复、右键菜单等）
- matchStart/matchEnd 仍为 UTF-8 字节偏移，预览跳转零错位（用 TextEncoder 反算字符→字节，上层 filePreview/highlight 零改动）


## [0.1.0] - 2026-07-15

### 新增
- uTools 插件首版：实现 FastDog 除搜索历史外的全部功能
- 移植核心逻辑（参数构建 / rg JSON 解析 / 结果聚合 + 日期过滤 / 文件预览 + 偏移转换）为 JS，附 4 组单元测试（23 例，全部通过）
- 还原桌面版三层布局（搜索条件区 / 文件列表 / 匹配行 + 预览），配色一致
- 支持搜索条件：正则 / 纯文本、大小写敏感、全词匹配、文件名过滤、目录排除
  - 注：搜索模式默认「纯文本」（与桌面版 FastDog 默认「正则表达式」不同，uTools 即时搜索场景下更直觉）
  - 注：不提供日期范围过滤（桌面版有，插件按需精简）
- 预览内 Ctrl+F 二次查找（橙色高亮 + N/M 计数，与主搜索黄色高亮物理隔离）
- 文件预览：全文 + 行号 + 黄色匹配高亮、大文件（>5MB）截断、二进制文件提示、自动换行
- 流式输出 + 取消、双击打开文件、拖放文件夹设搜索路径、状态栏、可拖拽分割条
- 会话恢复：下次打开自动恢复上次搜索条件（搜索词/路径/正则·文本/大小写/全词/文件过滤/目录排除/日期范围）+ 布局（上下左右分割比例、各列列宽），基于 uTools dbStorage 持久化
- 3 个进入入口：关键字（fd / fastdog）、超级面板选中文本（预填搜索词）、文件夹（预填路径）
- 捆绑 win32 / darwin / linux x64 的 ripgrep 14.1.1，按 `process.platform` 自动加载
