# 变更记录 (Changelog)

本项目所有 notable 版本变更记录于此文件。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本 (Semantic Versioning)](https://semver.org/lang/zh-CN/)。

## 版本号规则

- **主版本号 (MAJOR)**：不兼容的 API/行为变更
- **次版本号 (MINOR)**：向后兼容的新功能
- **修订号 (PATCH)**：向后兼容的缺陷修复

每个版本分类记录：
- `新增` 新功能
- `变更` 对现有功能的修改
- `修复` 缺陷修复
- `移除` 已移除的功能

---

## [1.2.1] - 2026-06-30

### 新增

- **单实例限制**：程序仅允许运行一个实例。重复启动时，新进程通过命名
  `EventWaitHandle` 通知已有实例激活主窗口（从托盘恢复到前台），然后自行退出。
  实现方式：`Global\FastDog_SingleInstance_Mutex`（命名互斥量检测首次实例）+
  `Global\FastDog_ShowWindow_Event`（命名事件通知激活窗口），后台监听线程在
  `App.xaml.cs`。

---

## [1.2.0] - 2026-06-29

### 新增

- **最小化到系统托盘**：关闭主窗口（标题栏 ✕ 或 Alt+F4）时不再退出程序，
  而是直接隐藏到系统托盘，便于后台常驻、随用随调。托盘图标复用
  `newLogo1.2.ico`，**左键单击**图标直接恢复窗口（符合常见托盘图标交互习惯），
  右键单击弹出「显示 FastDog / 退出」菜单。真正退出仅在托盘菜单选择「退出」时
  发生，退出前照常保存搜索会话与窗口布局。涉及 `App.xaml`（移除 `StartupUri`、
  改 `OnExplicitShutdown`）、`App.xaml.cs`（`NotifyIcon` 生命周期）、
  `MainWindow.xaml.cs`（`OnClosing` 拦截 + `Quit()`）。

- **输入历史自动补全**：搜索路径与搜索内容输入框现在支持历史补全——
  聚焦输入框即下拉显示全部历史项，继续输入则按前缀过滤；支持 ↑/↓ 选择、
  Enter 提交、Esc 关闭，鼠标点击项即填入。路径与内容各自独立去重持久化
  （各 50 条上限，`%APPDATA%\FastDog\input-history.json`）。新增
  `Services/InputHistoryService.cs`、`InputHistoryPopupController.cs`，
  `MainViewModel` 集成补全建议集合并在搜索成功后记录输入历史。

- **关于窗口**：托盘右键菜单新增「关于…」入口，弹出居中模态窗口展示应用
  Logo、版本号（程序集 Version，与 csproj `<Version>` 同源）、简介、技术栈
  （.NET 8 / WPF / AvalonEdit / ripgrep）、GitHub 仓库链接。窗口内置「检查
  更新」按钮，与托盘菜单形成双入口。新增 `AboutWindow.xaml(.cs)`。

- **检查更新**：托盘右键菜单新增「检查更新」入口（关于窗口内也有）。调用
  GitHub Releases API 获取最新版本，与本地程序集版本比对（支持 `v1.2.0` 格式
  tag 解析）；发现新版本后提示用户下载，流式安装到 `%TEMP%\FastDog-Setup-{version}.exe`
  并自动打开，用户自行安装。已封装为 `Services/UpdateService.cs`（GitHub API
  对接 + 版本比对 + 资产匹配 + 流式下载）、`UpdateProgressWindow.xaml(.cs)`
  （下载进度弹窗）。

### 变更

- **托盘右键菜单视觉统一**：原 WinForms 默认的蓝紫渐变菜单替换为自定义 WPF
  圆角菜单窗口（`TrayMenuWindow`）——矢量渲染圆角（8px，无锯齿）、白底卡片
  + 投影、暖灰边框、蓝色悬停高亮 `#0e639c`，与主窗口风格一致。改用 WPF 窗口
  而非 WinForms `ContextMenuStrip`，规避了 Region 裁剪必然产生的锯齿。

- **输入历史下拉框视觉优化**：下拉面板改为圆角白底 + 投影，使其悬浮于窗口
  内容之上、与背景清晰区分；列表项改为圆角卡片样式（选中蓝底白字、悬停浅灰）。
  相关样式提取为 App.xaml 全局资源 `HistoryListItem` / `HistoryPopupContent`。

### 修复

- **托盘右键菜单位置错乱（偏到图标右下方、离图标很远）**：菜单定位前测量自身尺寸
  时对未显示的 `Window` 调 `Measure`，WPF 返回 0×0，导致定位偏移取 0、菜单左上角
  钉在光标处向右下展开。改为测量内容根元素取真实尺寸。顺带修正水平定位三元分支
  （越界时未左对齐）、菜单窗口补 `Topmost` 以免被任务栏/溢出面板遮挡，并用
  `GetDpiForMonitor`（光标所在屏）替换 `GetDpiForSystem`（主屏）兼容混合 DPI 多屏。
  涉及 `TrayMenuWindow.xaml(.cs)`、`App.xaml.cs`。

- **点击托盘菜单项导致进程崩溃（鼠标转圈数秒后退出）**：`TrayMenuWindow` 主动
  `Close()` 时会先触发 `OnDeactivated`，后者无守卫地再次 `Close()`，对正在关闭的
  窗口重入调用抛 `InvalidOperationException`，异常逃逸出事件处理器后被 Dispatcher
  终止进程。加 `_isClosing` 守卫统一收口到 `CloseMenu()`，并将点击回调改为菜单
  关闭后 `Dispatcher.BeginInvoke` 异步执行，避免 `Close()`/`Show()` 同栈交互。

---

## [1.1.1] - 2026-06-27

### 变更

- **更换软件 logo**：窗口图标、任务栏图标、标题栏内置图标及 README 文档中的
  品牌图统一替换为 `newLogo1.1.ico` / `newLogo1.1.png`（更新 `FastDog.csproj`
  的 `ApplicationIcon` 与 `Resource`、`MainWindow.xaml` 的 `Icon` 及标题栏
  `Image`、中英文 README 的 `<img>` 引用）。标题栏原手绘「蓝底白字 F」方块
  替换为真 logo 图片（22×22，高质量缩放）。

- **标题栏与状态栏改用浅米色背景**：标题栏、状态栏背景由纯白/浅灰统一为
  `#faf8f3`（暖米色），底边线用暖灰 `#ebe6da`。与内容区纯白形成"米色边框 +
  白色核心"的层次结构，上下两端色调呼应，整体更协调，避免标题栏与内容区
  同为纯白缺乏分隔。
- 修正 `## [1.1.0]` 中状态栏色值描述（原记录的浅灰 `#e8e8e8` 已被本次米色调整覆盖）。

---

## [1.1.0] - 2026-06-26

### 新增

- **自定义标题栏**：用 `WindowChrome` 取代 Windows 默认蓝色系统标题栏，
  自绘「最小化 / 最大化 / 还原 / 关闭」三个按钮（关闭按钮 hover 显红 `#c0392b`），
  保留原生拖拽、双击最大化、贴边（Aero Snap）等窗口行为。
  - 左侧内嵌图标 + 彩色 Logo（"Fast" 红 `#e74c3c` + "Dog" 深色）+ 版本副标题。
  - 最大化时按钮图标自动切换为「还原」形态（`MainWindow.xaml.cs` `Window_StateChanged`）。
  - `App.xaml` 新增 `CaptionButton` / `CaptionCloseButton` 两个标题栏按钮样式。

### 变更

- **整体视觉重做为 VS Code 风极简配色**（`App.xaml` 主题资源 + `MainWindow.xaml` 全量刷新）：
  - 强调色 `#3498db`（饱和蓝）→ `#0e639c`（深青蓝），统一选中态、徽章、统计数字。
  - 主文字 `#2c3e50` → `#1e1e1e`；状态栏文字深灰 `#555`、数字用强调色蓝高亮。
  - 选中行 `#eaf2f8` → `#cce5ff`（淡蓝）；hover `#f8f9fa` → `#e8e8e8`；
    匹配行选中 `#fef9e7`/`#f39c12` → `#fffbe6`/`#b8860b`（沉稳琥珀）。
  - 所有粗边框（2px）统一降为 1px 细线 `#e0e0e0`，圆角统一 3px。
  - 面板标题、表头底色 `#ecf0f1` → `#fafafa`；弱文字 `#7f8c8d` → `#9a9a9a`。

- **标题栏与状态栏改用浅米色背景**：标题栏、状态栏背景由纯白/浅灰统一为
  `#faf8f3`（暖米色），底边线用暖灰 `#ebe6da`。与内容区纯白形成"米色边框 +
  白色核心"的层次结构，上下两端色调呼应，整体更协调，避免标题栏与内容区
  同为纯白缺乏分隔。

- **日期范围筛选改为内联布局**：启用「日期范围」后，两个日期选择框直接
  接在 toggle 右侧同一行显示（与文件过滤、排除目录一致），不再塌到下方
  独立一行，搜索区高度不再跳变。

- **搜索区顶部 Logo 移至标题栏**：原搜索区顶部的彩色 Logo（"Fast Dog" + 版本）
  合并进自定义标题栏，搜索区更紧凑，直接从「搜索路径」开始。

- 搜索按钮（绿 `#27ae60`）、取消按钮（红 `#e74c3c`）颜色保持不变，仅微调 hover 色阶。

- **全局圆角统一**：为窗口外框、所有按钮（浏览/搜索/取消/标签/选项）、输入框
  （搜索框/标签内联输入）添加圆角。原生 `Button`/`TextBox` 无圆角支持，改用
  `ControlTemplate` 包 `Border CornerRadius`，焦点态边框色切换由模板触发器承载。
  - 统一圆角值：控件 4px。窗口外框因 `WindowChrome`（非透明窗口）无法真正裁切圆角，
    保持直角以保证拖拽、缩放、贴边（Aero Snap）等原生行为稳定可用。

---

## [1.0.3]

### 修复

- **文件格式过滤输入扩展名写法搜不到结果**：在文件过滤框输入 `.cs`、`cs`
  这类扩展名写法时搜索结果为空，而输入 `*.cs` 或清空过滤框则正常。
  根因是 `RipgrepBridge.BuildFilterArgs` 把用户输入原样传给 ripgrep 的
  `--iglob`，而 glob 语义中 `.cs` 表示「文件名恰好为 .cs」，匹配不到
  `Program.cs` 等文件。
  - 新增 `NormalizeFilePattern()`，对点号开头的扩展名（`.cs`）补成 `*.cs`、
    对纯扩展名（`cs`）补成 `*.cs`；已含通配符（`*.cs`、`?.x`）或完整文件名
    （`Program.cs`）则原样保留。
  - 该归一化同时作用于搜索（`BuildArguments`）和文件计数（`BuildFileListArguments`）。
  - 新增 3 组回归测试（`ArgumentBuilderTests`）覆盖三种输入写法。

- **搜索词首尾空白未处理**：在搜索框中输入（或复制粘贴）带首尾空白的搜索词时，
  空白会被原样传给 ripgrep，导致匹配不到预期结果。
  - `MainViewModel.SearchAsync()` 开头对 `SearchText` 调用 `Trim()`，
    确保传给 `SearchQuery`、记录到历史、界面回显的值一致。

- **搜索词含双引号时匹配失败**：纯文本模式下搜索形如
  `AddDocumentPropertyResultDto.Error($"添加失败：{ex.Message}")` 这类含双引号的
  内容时无结果。根因是 `RipgrepBridge` 构造 ripgrep 命令行参数时未转义搜索词内部的
  双引号，导致命令行从中间被截断，pattern 被破坏。
  - 新增 `EscapeArg()`，按标准 Windows 命令行规则（`CommandLineToArgvW` 逆运算）
    转义参数，正确处理双引号、反斜杠结尾等边界情况。
  - 替换 `BuildArguments` / `BuildFileListArguments` / `BuildFilterArgs` 中所有手工
    拼接双引号的写法（搜索词、搜索路径、`--iglob` 文件过滤、`--glob` 目录排除）。
  - 新增 3 个回归测试（`ArgumentBuilderTests`）：含双引号转义、含空格加引号、
    简单词不加引号；修正 1 个旧测试。全部 51 个测试通过。

- **日期范围可设置为倒置区间**：在日期范围过滤中，可以把结束日期（右侧）
  选得早于开始日期（左侧），从而产生无意义/空结果的搜索。根因是
  `DateFrom` 与 `DateTo` 两个属性之间没有任何联动校验。
  - 在 `MainViewModel` 中新增 `OnDateFromChanged` / `OnDateToChanged`
    交叉校验：任一边改变后若越过另一边，就把另一边拉齐到当前值，
    始终保证 `DateFrom <= DateTo`。仅在越界时才写回，避免链式递归触发。

---

## 维护说明（给开发者 / Agent）

- **添加变更时**：用"下一个要发布的版本号"作为标题（如当前的 `## [1.0.3]`）。**只要该版本尚未发布，所有新改动都继续累积在同一个标题下，不递增版本号**——无论改动有多少次、是什么类型。
- **发布版本时**：在标题后补上日期，如 `## [1.0.3] - 2026-06-26`，并打 git tag `v1.0.3`。发布后，下一个新改动才启用新版本号。
- **条目格式**：按分类（新增/变更/修复/移除）归组，每条简洁说明"改了什么 + 为什么"，必要时附文件名。
- **版本号规则**：仅缺陷修复升修订号（PATCH，如 1.0.3 → 1.0.4）；含新功能升次版本号（MINOR，如 1.0.3 → 1.1.0）；含不兼容变更升主版本号（MAJOR，如 1.0.3 → 2.0.0）。**版本号在发布时根据本次改动确定，而非每提交即递增。**
- **每次更改结束后必须重新编译**：运行 `dotnet build FastDog.sln` 确认 0 错误 0 警告，确保改动可正常构建后再继续下一步。
