# FastDog

基于 WPF + ripgrep 的文本搜索工具，参考 grepWin 核心搜索功能，面向团队内部使用。

## 技术栈

- .NET 8 (net8.0-windows)
- WPF + CommunityToolkit.Mvvm 8.4 (MVVM)
- AvalonEdit 6.3 (文件预览控件，语法高亮 + 文本标记)
- ripgrep 14.1.1 (捆绑在 `tools/rg.exe`)
- xUnit 测试框架

## 项目结构

```
src/FastDog/
  Models/          SearchQuery, SearchResult, MatchLine, SearchHistoryEntry, LayoutConfig
  Services/        RipgrepBridge (rg 进程管理), SearchService (业务逻辑), FilePreviewService (文件加载/偏移计算), TextMarkerService (匹配高亮渲染), SearchHistoryService (搜索历史JSON持久化), LayoutConfigService (布局持久化)
  Helpers/         ClipboardHelper (原生 Win32 剪贴板), DwmHelper (Win11 圆角), InputHistoryPopupController (输入历史自动补全), SearchPanelLocalization (查找栏中文本地化), PreviewFindCounter (查找 N/M 计数重算)
  ViewModels/      MainViewModel
  MainWindow.xaml  搜索条件区(Logo+按钮行选项) + 自定义标签栏(搜索结果/历史) + 匹配行列表(左) + 文件预览(右)
  App.xaml         颜色主题资源 + 全局控件样式（ToggleOptionButton, TabButtonStyle, SearchPanel 等）
tests/FastDog.Tests/
  ArgumentBuilderTests, JsonParserTests, DateFilterTests, FilePreviewServiceTests, SearchHistoryServiceTests, LayoutConfigServiceTests
tools/
  rg.exe           ripgrep 可执行文件
plugins/
  utools-fastdog/  uTools 插件子项目（独立版本号与 CHANGELOG，纯 JS 搜索引擎，见 plugins/utools-fastdog/DEVELOPMENT.md）
    DEVELOPMENT.md 开发文档（架构/契约/已知坑/决策背景，维护前必读）
    CHANGELOG.md   版本变更记录（独立于主仓库）
    lib/           jsSearchEngine(搜索引擎,child_process.fork多进程并行+串行fallback), searchChild(fork子进程脚本), matcher(匹配器工厂), gitignoreFilter, argumentBuilder, jsonParser, searchService, filePreview
    src/           app.js, style.css, highlight.js, previewFind.js (Web UI)
    preload.js     uTools 装配层（暴露 window.fastdog + onPluginEnter）
    plugin.json    uTools 插件配置
docs/
  superpowers/specs/   设计文档
  superpowers/plans/   实现计划
```

## 已实现功能

- 纯文本搜索 / 正则表达式搜索
- 大小写敏感、全词匹配
- 文件名通配符过滤（`*.cs;*.txt`），标签按钮内联编辑
- 目录排除（默认排除 .git），标签按钮内联编辑
- 按修改日期范围过滤（可折叠日期选择器）
- 搜索结果：文件列表（DataGrid，蓝色匹配数徽章）+ 匹配行列表（左，黄色选中高亮+橙色左边框）+ 文件预览面板（右，AvalonEdit）
- 文件预览：全文显示、行号、语法高亮、所有匹配文本黄色背景标记
- 点击匹配行自动滚动到预览区对应位置
- 预览内二次查找：Ctrl+F 唤起 AvalonEdit 内置 SearchPanel，在预览文件内搜索；命中项浅橙高亮(#ff9900)、当前项深橙(#e67300)+白字；自定义圆角投影查找栏(中文选项)、N/M 实时计数
- 大文件（>5MB）截断显示，二进制文件提示"无法预览"
- 双击打开文件 / 双击匹配行跳转行号（VS Code）
- 右键菜单：复制路径、复制文件名
- 拖放文件夹设置搜索路径
- 深色状态栏（#2c3e50）：状态文本、文件数、匹配数、耗时（蓝色高亮数字）
- 搜索历史：记录最近 50 条搜索（去重合并），卡片列表视图（搜索词、路径、选项标签、统计数据、时间）
- 会话恢复：下次启动自动恢复上次关闭时的搜索条件（状态栏提示，操作后自动清除）
- 自定义标签栏：RadioButton 样式标签 + 历史计数徽章
- 历史卡片：单击恢复条件、双击恢复并搜索
- 现代化 UI：蓝色主色调 #3498db、ToggleButton 式搜索选项、深色状态栏、面板标题栏
- 窗口居中启动，默认尺寸 800x1200
- 布局持久化：保存/恢复窗口位置、尺寸、最大化状态、GridSplitter 分割比例（JSON，%APPDATA%\FastDog\layout-config.json）
- 单实例限制：仅允许运行一个实例，重复启动时激活已有窗口（命名 Mutex + EventWaitHandle，`App.xaml.cs`）

## 构建与运行

```powershell
dotnet build FastDog.sln
dotnet run --project src/FastDog
dotnet test FastDog.sln
```

## 变更记录规则

每个版本的所有变更记录在根目录 `CHANGELOG.md`（Keep a Changelog 格式 + 语义化版本）。每次提交修复或新功能时必须同步更新。

- **添加变更时**：用"下一个要发布的版本号"作为标题（当前 `## [1.2.2]`）。**只要该版本尚未发布，所有新改动都继续累积在同一个标题下，不递增版本号**——无论改动有多少次、是什么类型。
- **发布版本时**：在标题后补日期，如 `## [1.2.2] - 2026-06-30`，并打 git tag `v1.2.2`。发布后，下一个新改动才启用新版本号（根据本次发布包含的改动类型确定）。
- **条目格式**：按分类归组——`新增` / `变更` / `修复` / `移除`，每条简洁说明"改了什么 + 为什么"，必要时附文件名。
- **版本号规则**：仅缺陷修复升修订号（PATCH，如 1.0.3 → 1.0.4）；含向后兼容的新功能升次版本号（MINOR，如 1.0.3 → 1.1.0）；含不兼容变更升主版本号（MAJOR，如 1.0.3 → 2.0.0）。**版本号在发布时根据本次改动确定，而非每提交即递增。**
- **每次更改结束后必须重新编译**：运行 `dotnet build FastDog.sln` 确认 0 错误 0 警告，确保改动可正常构建后再继续下一步。

## 架构

三层架构：UI (WPF MVVM) → SearchService → RipgrepBridge → rg.exe

- **RipgrepBridge**: 查找 rg.exe、构建命令参数、解析 JSON 输出、管理进程生命周期
- **SearchService**: 搜索编排、文件结果聚合、日期过滤、事件通知
- **FilePreviewService**: 文件加载、二进制检测、大文件截断（>5MB/5000行）、行内偏移→全局偏移转换
- **TextMarkerService**: AvalonEdit IBackgroundRenderer（KnownLayer.Background），黄色半透明背景标记主搜索匹配文本
- **MainViewModel**: 所有 UI 状态和命令，通过事件接收搜索结果，选中文件时加载预览并计算匹配偏移
- **预览内查找**（MainWindow.xaml.cs 装配）：复用 AvalonEdit 内置 `SearchPanel.Install(editor)`——Ctrl+F 唤起、选中文本预填、切换文件自动重跑均由内置实现覆盖；命中项橙色 `MarkerBrush`(KnownLayer.Selection) 与主搜索黄色物理隔离。当前项深橙经 `TextArea.SelectionBrush` 全局选区色实现。N/M 计数因 SearchPanel 内部结果集合为 internal，由 `PreviewFindCounter` 用公开 `SearchStrategyFactory` 自行重算
- **SearchHistoryService**: 搜索历史持久化（JSON，%APPDATA%\FastDog\），去重合并、50条上限、会话保存/恢复
- **LayoutConfigService**: 窗口布局持久化（位置、尺寸、最大化状态、GridSplitter 分割比例），独立 JSON 文件存储

rg.exe 通过 `--json` 模式输出，`IAsyncEnumerable<RgEvent>` 流式推送结果到 UI。


# 必须遵守的规则和约束（重点）

- 尽可能符合 `MVVM`  的开发规范；
- 代码中做好日志记录

