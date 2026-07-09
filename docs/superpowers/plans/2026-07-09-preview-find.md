# 预览内二次查找 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在文件预览面板（AvalonEdit TextEditor）中支持 Ctrl+F 唤起查找栏，匹配结果以橙色高亮，与主搜索黄色高亮区分。

**Architecture:** 复用 AvalonEdit 内置 `SearchPanel`。`SearchPanel.Install(editor)` 一行即获得 Ctrl+F / 上下一个 / 大小写·正则·全词切换；设 `MarkerBrush` = 橙色实现与黄色主高亮（`TextMarkerService`，`KnownLayer.Background`）的物理隔离（SearchPanel 内部 `SearchResultBackgroundRenderer` 画在 `KnownLayer.Selection`）。经源码验证，Ctrl+F 唤起、选中文本预填、上次查找词保留、切换文件重跑查找均由内置实现免费覆盖。

**Tech Stack:** .NET 8 WPF, AvalonEdit 6.3 (`ICSharpCode.AvalonEdit.Search.SearchPanel`), CommunityToolkit.Mvvm。

**测试说明：** 本功能为纯 UI 装配（调用库 API + 设属性），无可单元测试的新逻辑/服务/算法；在无头 xUnit 中实例化 WPF `TextArea` 不可靠。故采用 spec 定义的手动验证清单作为验证手段，对照每条行为逐一实测。这不是跳过测试，而是该功能无合适的可测单元。

**参考设计：** `docs/superpowers/specs/2026-07-09-preview-find-design.md`

---

### Task 1: 新增二次查找高亮颜色资源

**Files:**
- Modify: `src/FastDog/App.xaml:22`（在 `HoverBgBrush` 之后追加一行）

- [ ] **Step 1: 在 App.xaml 颜色主题区追加橙色画刷资源**

在 `src/FastDog/App.xaml` 第 22 行 `<SolidColorBrush x:Key="HoverBgBrush" Color="#e8e8e8"/>` 之后新增一行：

```xml
        <SolidColorBrush x:Key="FindMatchBrush" Color="#ff9900"/>
```

修改后该区域应为：

```xml
        <SolidColorBrush x:Key="HighlightBgBrush" Color="#fffbe6"/>
        <SolidColorBrush x:Key="HoverBgBrush" Color="#e8e8e8"/>
        <SolidColorBrush x:Key="FindMatchBrush" Color="#ff9900"/>
```

- [ ] **Step 2: 编译确认 0 错误 0 警告**

Run: `dotnet build FastDog.sln`
Expected: Build succeeded，0 Error，0 Warning。

- [ ] **Step 3: Commit**

```bash
git add src/FastDog/App.xaml
git commit -m "feat: 新增 FindMatchBrush 橙色资源用于预览内二次查找高亮"
```

---

### Task 2: 安装 SearchPanel 并设置橙色高亮

**Files:**
- Modify: `src/FastDog/MainWindow.xaml.cs:10-13`（using 区）、`:21-23`（字段区）、`:86-87`（OnContentRendered 获取 editor 后）

- [ ] **Step 1: 添加 Search 命名空间 using**

在 `src/FastDog/MainWindow.xaml.cs` 第 13 行 `using ICSharpCode.AvalonEdit.Highlighting;` 之后新增：

```csharp
using ICSharpCode.AvalonEdit.Search;
```

- [ ] **Step 2: 添加 SearchPanel 字段**

在第 22 行 `private TextMarkerService? _markerService;` 之后新增一行字段：

```csharp
    private SearchPanel? _searchPanel;
```

修改后字段区（21-23 行）为：

```csharp
    private MainViewModel? _vm;
    private TextMarkerService? _markerService;
    private SearchPanel? _searchPanel;
    private readonly LayoutConfigService _layoutService = new();
```

- [ ] **Step 3: 在 OnContentRendered 中 Install SearchPanel 并设 MarkerBrush**

在第 87 行 `if (editor is null) return;` 之后、第 89 行 `_vm.PropertyChanged += ...` 之前，插入：

```csharp
        // 安装 AvalonEdit 内置查找面板：Ctrl+F 唤起、选中文本预填、切换文件自动重跑均由内置实现覆盖。
        // MarkerBrush 设橙色，与主搜索黄色高亮（TextMarkerService，KnownLayer.Background）通过
        // KnownLayer.Selection 物理隔离。
        _searchPanel = SearchPanel.Install(editor);
        _searchPanel.MarkerBrush = (Brush)FindResource("FindMatchBrush");
```

修改后该段（86-95 行）为：

```csharp
        var editor = FindEditor();
        if (editor is null) return;

        // 安装 AvalonEdit 内置查找面板：Ctrl+F 唤起、选中文本预填、切换文件自动重跑均由内置实现覆盖。
        // MarkerBrush 设橙色，与主搜索黄色高亮（TextMarkerService，KnownLayer.Background）通过
        // KnownLayer.Selection 物理隔离。
        _searchPanel = SearchPanel.Install(editor);
        _searchPanel.MarkerBrush = (Brush)FindResource("FindMatchBrush");

        _vm.PropertyChanged += (s, args) =>
```

- [ ] **Step 4: 编译确认 0 错误 0 警告**

Run: `dotnet build FastDog.sln`
Expected: Build succeeded，0 Error，0 Warning。若提示 `FindResource` 找不到 `FindMatchBrush`，确认 Task 1 已合并。

- [ ] **Step 5: Commit**

```bash
git add src/FastDog/MainWindow.xaml.cs
git commit -m "feat: 预览面板支持 Ctrl+F 二次查找（AvalonEdit SearchPanel，橙色高亮）"
```

---

### Task 3: 手动验证 + 边界处理

**Files:**
- 验证为主，若边界泄漏则 Modify: `src/FastDog/MainWindow.xaml.cs`（新增 CanExecute 守卫）

**准备：** 运行程序并执行一次搜索，使预览面板加载一个有匹配结果的文件。

Run: `dotnet run --project src/FastDog`

- [ ] **Step 1: 基础唤起与高亮**

焦点移到预览编辑器内（点击预览区文本），按 `Ctrl+F`：
- [ ] 查找栏出现在编辑器右上角
- [ ] 输入一个词 → 所有匹配以**橙色**高亮
- [ ] 主搜索的**黄色**高亮仍然存在（两层共存）

- [ ] **Step 2: 导航**

- [ ] `Enter` → 跳到下一个匹配
- [ ] `Shift+Enter` → 跳到上一个匹配
- [ ] `F3` → 下一个
- [ ] `Shift+F3` → 上一个

- [ ] **Step 3: 查找选项**

点击查找栏下拉按钮：
- [ ] 勾选「大小写」→ 仅匹配指定大小写
- [ ] 勾选「全词」→ 仅匹配整词
- [ ] 勾选「正则」→ 按正则匹配

- [ ] **Step 4: 预填行为**

- [ ] 在预览区选中一段文本（单行）→ 按 `Ctrl+F` → 查找框预填选中文本且全选
- [ ] 关闭查找栏（`Esc`）→ 再次 `Ctrl+F` → 查找框显示上次查找词

- [ ] **Step 5: 切换文件**

- [ ] 查找栏**开启**状态下，点击文件列表另一个文件 → 新文件自动重跑查找，橙色高亮保持
- [ ] 查找栏**关闭**状态下（`Esc` 关闭），切换文件 → 无橙色高亮；再 `Ctrl+F` 仍显示上次查找词

- [ ] **Step 6: 无文件边界**

- [ ] 选中一个二进制文件（如 .exe / .png）→ 预览区显示「二进制文件，无法预览」→ 按 `Ctrl+F` → **不应**唤起查找栏
- [ ] 若上述任一状态下 `Ctrl+F` 仍唤起查找栏（边界泄漏），则需新增 CanExecute 守卫：
  在 `MainWindow.xaml.cs` 的 `OnContentRendered` 中，Install 之后绑定 `CommandManager` 对 `ApplicationCommands.Find` 的 `CanExecute`，当 `_vm.IsBinaryFile || _vm.IsFileError || string.IsNullOrEmpty(_vm.FileContent)` 时 `e.CanExecute = false`。具体实现视实际泄漏情况而定。

- [ ] **Step 7: 大文件截断**

- [ ] 选中一个 >5MB 的文本文件 → 预览显示「文件过大，仅显示部分内容」→ `Ctrl+F` 查找仅在已加载部分生效

- [ ] **Step 8: 若 Step 6 增加了守卫，编译并 Commit**

Run: `dotnet build FastDog.sln`
Expected: 0 Error，0 Warning。

```bash
git add src/FastDog/MainWindow.xaml.cs
git commit -m "fix: 二进制/错误文件状态下禁用 Ctrl+F 查找唤起"
```

（若 Step 6 无泄漏，跳过本步）

---

### Task 4: 更新 CHANGELOG

**Files:**
- Modify: `CHANGELOG.md`（在顶部 `## [1.3.0] - 2026-07-01` 之前新增 `## [1.4.0]` 段）

**版本号依据：** 新功能（向后兼容）→ MINOR 升号，1.3.0 → 1.4.0。当前 1.3.0 已发布（带日期），故启用新版本号。

- [ ] **Step 1: 在 CHANGELOG 顶部新增 1.4.0 段**

在 `## [1.3.0] - 2026-07-01` 这一行之前插入：

```markdown
## [1.4.0]

### 新增

- **预览内二次查找**：文件预览面板支持 `Ctrl+F` 唤起查找栏，在当前预览文件内
  二次搜索。匹配结果以橙色高亮（`#ff9900`），与主搜索的黄色匹配高亮区分；
  支持大小写 / 全词 / 正则切换，`Enter`/`F3` 下一个、`Shift+Enter`/`Shift+F3`
  上一个。复用 AvalonEdit 内置 `SearchPanel`，选中文本自动预填、切换文件自动
  重跑查找（`App.xaml`、`MainWindow.xaml.cs`）。

---

```

- [ ] **Step 2: Commit**

```bash
git add CHANGELOG.md
git commit -m "docs: CHANGELOG 记录 v1.4.0 预览内二次查找"
```

---

## Self-Review

**1. Spec 覆盖：**
- Ctrl+F 唤起 → Task 2 Step 3（Install 自动注册）+ Task 3 Step 1 验证 ✅
- 选中文本预填 → Task 3 Step 4 验证（内置 ExecuteFind 覆盖）✅
- 上次查找词保留 → Task 3 Step 4 验证（SearchPattern DP 持久化）✅
- 切换文件重跑 → Task 3 Step 5 验证（textArea_DocumentChanged 覆盖）✅
- 橙色高亮/与黄色区分 → Task 1 资源 + Task 2 MarkerBrush + Task 3 Step 1 验证 ✅
- 无文件边界 → Task 3 Step 6 验证 + 条件性守卫 ✅
- 大文件截断 → Task 3 Step 7 验证 ✅

**2. 占位符扫描：** 无 TBD/TODO；Step 6 的 CanExecute 守卫为条件性实现（依赖实测是否泄漏），已给出明确的判定条件与实现方向，非占位符。✅

**3. 类型一致性：** `SearchPanel`、`_searchPanel`、`FindMatchBrush`、`MarkerBrush` 在各 Task 间命名一致；`ICSharpCode.AvalonEdit.Search` using 与字段类型匹配。✅
