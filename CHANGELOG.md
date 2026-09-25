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

## [1.8.0]

### 新增

- **文件模式输入即搜**：文件搜索模式下逐字输入关键词（或切换大小写/后缀/日期/文件与文件夹开关/排除目录等选项）停顿 200ms 后自动搜索，无需点「搜索」按钮；文本搜索模式保持手动触发不变。
  - **内存枚举缓存**（Everything 思路）：rg `--files` 目录遍历每个 (搜索路径, 排除目录, 隐藏白名单) 组合只做一次，枚举结果常驻内存；之后关键词/后缀/日期变化走缓存纯内存过滤（命中项并行 stat），毫秒级出结果。冷构建期间继续输入会顶掉旧构建，半截枚举不落缓存。
  - **批量填充**：缓存命中结果经新事件 `FileSearchService.ResultsReady` 一次性批量推送（预排序：文件在前、文件夹在后），避免大结果集逐条 `Dispatcher.Invoke` 卡 UI；冷构建保留原有的流式渐进显示。
  - **搜索按钮语义升级**：文件模式下点「搜索」= 强制重建枚举缓存的全量刷新——缓存建立后新建/删除的文件借此可见（不做 TTL 与文件系统监听；已删除文件凭 Exists 检查自动从结果消失，修改时间/日期过滤每次实时 stat 不受缓存影响）。
  - **自动搜索静默化**：自动路径不弹错误框（路径不存在→状态栏提示）；防抖、代际守卫与查询签名去重（`FileSearchQuery.BuildSignature`）合并连击、丢弃被顶掉搜索的迟到事件、跳过与上次完全相同的条件；搜索历史与输入补全历史只记录"完成且未再输入"的最终查询。
  - 启用时机：窗口渲染完成（`MainWindow.OnContentRendered` → `EnableLiveFileSearch`）后才允许自动搜索，构造期会话恢复不触发。

### 变更

- **后缀过滤从 rg `--iglob` 移到 C# 侧**（`FileSearchService.MatchesExtensions`）：使枚举结果与后缀选择解耦、可被内存缓存复用；匹配语义不变（`*.cs`/`.cs`/`cs` 等写法等价，忽略大小写，多选为 OR）。

### 修复

- `SearchService`/`FileSearchService` 新搜索直接覆盖 `_cts` 且 finally 误释放新 CTS 的竞态：改为局部 CTS + `ReferenceEquals` 守卫，新搜索启动前先取消旧运行并杀死 rg 进程（输入即搜下并发顶替成为常态，此前单飞按钮搜索亦受影响）。
- 文件搜索文件夹结果的 `Directory.Exists` 判断顺序颠倒：已删除的目录反而会留在结果中；改为先判存在再过日期过滤。

---

## [1.7.0] - 2026-09-25

### 新增

- **文件搜索模式**：在全文内容搜索之外新增"文件搜索"模式，搜索条件区首行以「文本搜索 / 文件搜索」胶囊切换（`MainViewModel.IsFileSearchMode`）。
  - **匹配语义**：文件/文件夹名称支持子串包含与 `*`/`?` 通配符（整名锚定，Everything 风格），留空显示全部；区分大小写默认关闭。
  - **对象类型**：「文件」「文件夹」独立双开关（默认全开），两者全关时搜索按钮置灰。
  - **后缀过滤下拉**：内置 12 个常用后缀（cs/xaml/js/ts/py/java/md/txt/json/xml/html/css）多选复选列表；每一项（含内置项）可 ✕ 删除，「恢复默认」一键还原内置列表；支持添加自定义后缀（接受 `log` / `.log` / `*.log` 三种写法）；不选任何项 = 显示全部匹配；按钮文案实时联动（「后缀: 全部」↔「后缀: *.cs +2」）；列表增删与勾选状态持久化到 `%APPDATA%\FastDog\extension-config.json`（`FileExtensionConfigService`）。
  - **引擎**：复用捆绑 rg.exe 的 `--files` 模式流式枚举路径（不做内容匹配，`RipgrepBridge.BuildFileSearchArguments`/`ListFilesAsync`），后缀多选归一化为 `--iglob` 白名单交 rg 在遍历时过滤，排除目录（默认 .git）与隐藏目录白名单语义与文本搜索完全一致；文件名匹配在 C# 侧完成（`FileNameMatcher`）；文件夹结果由文件路径祖先目录推导（rg 只列文件，完全空的目录因此不在结果中，属已知边界）。
  - **结果列表**：文件名（类型图标）+ 类型徽章（文件=浅蓝后缀、文件夹=灰色）+ 大小 + 修改时间 + 路径；完成后统一重排为文件夹优先、同类文件名升序，列头点击可重排。
  - **下半区**：文件模式取消「匹配行」面板，改为全宽文件预览（复用 AvalonEdit 预览管线与语法高亮）；选中文件夹显示文件夹信息面板（类型/路径/修改时间）。
  - **交互**：双击文件用 VS Code 打开（`code` 不可用时回退系统默认程序）、双击文件夹用资源管理器打开；右键菜单"打开/在资源管理器中打开/复制文件名/复制文件路径"；支持多选批量复制。
  - **状态栏**：文件模式显示「文件 / 文件夹 / 耗时」统计。
  - **搜索历史**：与文本模式共用列表（`SearchHistoryEntry.SearchMode` 字段），卡片加「文件名」模式标签与「后缀」标签，统计标签随模式变化；单击恢复条件、双击恢复并搜索；会话保存/恢复同样携带模式。
  - 窗口副标题改为「文本 / 文件名双模式搜索」。
  - 文件预览行号边栏与左边缘增加 5px 间距（默认贴边显示过挤）。
  - 测试：新增 `FileNameMatcherTests`（子串/通配符/大小写/转义/锚定）、`FileExtensionConfigServiceTests`（默认/损坏回退/往返持久化）、`FileSearchArgumentTests`（--files 参数/后缀 glob/排除/隐藏白名单/引号转义）。

### 修复

- **文件模式下文件预览未占满下半区**：预览面板与文本模式一样停在 65% 列位。
  根因是 WPF 依赖属性优先级——列定义的本地 `Width/MinWidth` 与
  `ApplySplitRatios` 的代码赋值都是本地值，XAML 样式触发器永远无法覆盖。
  现改为由 `MainWindow.ApplyBottomPanelLayout` 在模式切换时直接以代码写入
  列宽/`ColumnSpan`（切文件模式前记住文本模式列宽，切回时原样还原）。
- **窗口可缩小到只剩标题栏**：Window 未设最小尺寸，拖拽可把主窗口缩到不可用。现设
  `MinWidth=960` / `MinHeight=600`——宽度下限保证文本模式选项按钮行不换行，高度下限
  保证结果列表与预览区拆分后仍各自可用（`MainWindow.xaml`）。

## [1.6.0] - 2026-08-27

### 新增

- **安装包中英双语**：安装界面此前只有英文（Inno Setup 默认 `Default.isl`）。现在
  `[Languages]` 同时声明英文与简体中文（官方翻译 `ChineseSimplified.isl` 随仓库入库，
  含 UTF-8 BOM），安装时按系统 UI 语言自动预选——中文系统显示中文，其他语言系统
  回退英文（english 声明在首位作为默认）；卸载程序同样跟随所选语言。另设
  `UsePreviousLanguage=no`：默认的 `yes` 会沿用旧版本（如 1.5.0 英文版）注册表里的
  语言记录作默认，导致语言框不选中系统语言，现改为每次安装都按系统语言检测。

### 修复

- **搜索历史重启后丢失**：搜索完成后只更新了内存集合 `HistoryEntries`，从未调用
  `SearchHistoryService.AddEntry` 落盘，导致 `search-history.json` 的 `History` 恒为空、
  重启后历史全部消失。现在 `OnSearchCompleted` 中同步持久化，UI 集合与磁盘保持一致的
  去重与 50 条截断（`MaxHistoryCount` 常量改为 public 供 ViewModel 复用）。

## [1.5.0]

### 新增

- **隐藏目录白名单**：搜索条件区新增「隐藏:」标签按钮（与「文件」「排除」同款，
  点击内联编辑），可指定要搜索的 `.` 开头目录（如 `.config;.vscode`）。这些目录会被
  纳入搜索，其余点开头目录（`.git`/`.vs`/`.idea` 等）仍默认跳过。实现要点：白名单非空
  时为 ripgrep 启用 `--hidden`，用兜底 `--glob '!.*'` 排除所有点路径，再为每个白名单
  目录追加两条正向 glob（`dir` 覆盖目录剪枝 + `dir/**` 包含目录内容）放在最后以覆盖
  `!.*`；无文件过滤时额外追加 `--glob '*'` 把普通文件拉回（否则正向 glob 触发的 include
  模式会排除普通文件）；显式 `--glob` 优先级高于 `.gitignore`，故被忽略规则的点目录也
  能搜到。此前
  ripgrep 默认 `--no-hidden` 导致用户完全无法搜索任何点目录内容（`RipgrepBridge.cs`、
  `MainViewModel.cs`、`SearchQuery.cs`、`MainWindow.xaml`）。

### 变更

- **日期范围选择改为弹出框**：原日期范围勾选后两个 DatePicker 就地嵌入按钮行（会把
  按钮行撑长/挤乱），现改为点击「📅 日期范围」按钮弹出浮动小框，内含起止 DatePicker
  与「启用日期过滤」勾选，点框外自动关闭。按钮文字固定「📅 日期范围」，启用过滤时
  按钮变深蓝（与正则/全词等选项按钮选中态一致），不再用文字标记状态。
  处理了 DatePicker 嵌套
  Popup 的已知坑：日历展开时临时切换外层 Popup 的 `StaysOpen`，避免「点日历选日期」
  被误判为「点外部」而关闭弹窗（`MainWindow.xaml`、`MainViewModel.cs`、`MainWindow.xaml.cs`）。

## [1.4.1] - 2026-07-13

### 修复

- **输入历史下拉框不再悬浮于其他程序窗口之上**：搜索路径/搜索内容输入框聚焦时弹出的
  历史补全下拉框（`Popup`，`StaysOpen=True`）是独立顶层 Win32 窗口，切换到其他程序时
  主窗口 `Deactivated` 但 `TextBox` 逻辑焦点不丢失（`LostFocus` 不触发），导致下拉框
  仍浮在其他应用窗口之上。`InputHistoryPopupController` 现订阅宿主窗口 `Deactivated`
  事件，失活即关闭下拉框（`InputHistoryPopupController.cs`）。
- **错误日志记录完整异常链**：`App.LogException` 现在遍历 `InnerException` 链，逐层
  记录内层异常的类型、消息和堆栈（`--->` 分隔），便于诊断被外层异常包装的真实根因
  （`App.xaml.cs`）。
- **检查更新网络请求增强**：直连 GitHub 失败时，自动用系统代理重试一次（
  `HttpClient.DefaultProxy`），并在 403 限流异常中附带 `X-RateLimit-Remaining` /
  `X-RateLimit-Reset` 头部信息，方便定位限流恢复时间（`UpdateService.cs`）。

## [1.4.0] - 2026-07-10

### 新增

- **预览内二次查找**：文件预览面板支持 `Ctrl+F` 唤起查找栏，在当前预览文件内
  二次搜索。匹配结果以橙色高亮（`#ff9900`），与主搜索的黄色匹配高亮区分；
  支持大小写 / 全词 / 正则切换，`Enter`/`F3` 下一个、`Shift+Enter`/`Shift+F3`
  上一个。复用 AvalonEdit 内置 `SearchPanel`，选中文本自动预填、切换文件自动
  重跑查找（`App.xaml`、`MainWindow.xaml.cs`）。
- **查找计数显示**：查找栏显示"第 N 项，共 M 项"实时计数（类似 VS Code），随搜索词
  变化、`F3`/`Enter` 跳转、切换文件实时刷新；无匹配时自动隐藏。因 AvalonEdit 内部
  匹配集合为 internal，新增 `Helpers/PreviewFindCounter.cs` 用公开 `SearchStrategyFactory`
  自行重算匹配数与当前序号，经 `SearchPanel.Tag` 传入模板 TextBlock 显示。

### 变更

- **查找框 UI 美化**：重写 AvalonEdit `SearchPanel` 的 `ControlTemplate`——圆角 6 +
  投影的白底容器、圆角 4 聚焦变蓝的输入框、`Path` 几何图标按钮（替换原生
  `prev.png`/`next.png`），对齐项目 VS Code 风配色（`App.xaml`）。
- **当前命中项配色**：查找跳转到的当前项由系统蓝绿选区色改为深橙 `#e67300`
  + 白字（设 `TextArea.SelectionBrush`/`SelectionForeground`），与所有命中项的
  浅橙 `#ff9900` 区分。该色为全局选区色，手动选中文字同样生效（`MainWindow.xaml.cs`）。
- **查找栏汉化**：`SearchPanel` 选项与提示由英文改为中文（区分大小写 / 全词匹配 /
  正则表达式、上/下一个、未找到匹配项），新增 `Helpers/SearchPanelLocalization.cs`
  覆盖 AvalonEdit `Localization`（`MainWindow.xaml.cs`）。
- **查找栏间距收紧**：选项箭头、上/下/关闭按钮间水平边距统一置零紧凑排列；计数 `1/2` 紧凑显示（`App.xaml`、`Helpers/PreviewFindCounter.cs`）。

---

## [1.3.0] - 2026-07-01

### 新增

- **文件列表多选**：搜索结果文件列表支持 Shift（范围选择）/ Ctrl（增减选择）
  + 鼠标点击多选。复制文件名/路径、打开文件均支持批量操作（多选时换行拼接复制、
  逐个打开），预览面板始终跟随焦点（最近点击）项。`MainViewModel.cs`、
  `MainWindow.xaml(.cs)`。
- **「在资源管理器中打开」定位选中文件**：原先仅打开父目录，现改用
  `explorer /select,"<文件全路径>"`，在资源管理器中高亮选中目标文件
  （`MainViewModel.cs`）。多选时该项禁用（explorer 不支持同时高亮多文件）。

### 变更

- **列表项选中/悬停配色统一为蓝色系**：文件列表与匹配行列表原先悬停为灰色、
  匹配行选中为黄色，色调不一致。现统一为蓝色梯度——悬停浅蓝（`#eaf3fb`）、
  选中淡蓝（`#c6e3ff`，复用 `AccentBgBrush`），匹配行左侧装饰条改为主色蓝。
  新增颜色资源 `AccentBgHoverBrush`，并重写 `DataGridRow`/`DataGridCell`/
  `ListBoxItem` 的 `ControlTemplate` 以彻底覆盖系统选中灰
  （`App.xaml`、`MainWindow.xaml`）。
- **检查更新缓存缩短**：`UpdateService` 结果缓存有效期由 24 小时调整为
  10 秒（`CacheHours` → `CacheSeconds`，`UpdateService.cs`），方便快速验证更新流程，
  调试时不再需要手动清理缓存或等待 24 小时。

### 修复

- **复制操作偶发崩溃**：多选复制文件名/路径时，若其它进程（输入法、剪贴板工具等）
  占用剪贴板，`Clipboard.SetText` 会抛 `ExternalException` 导致程序崩溃。新增
  `Helpers/ClipboardHelper.cs`，改走原生 Win32 API（绕过 OLE）并带重试 + 回退，
  失败时在状态栏提示而非崩溃。

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
