# 预览内二次查找设计

## 概述

在文件预览面板（AvalonEdit `TextEditor`）中支持二次查找：用户按 `Ctrl+F` 唤起查找栏，在当前预览的文件内搜索文本，匹配结果以橙色高亮显示，与主搜索的黄色匹配高亮区分。行为对标 VS Code 的查找体验。

## 背景

现有预览面板的主搜索匹配高亮由 `TextMarkerService`（`IBackgroundRenderer`，`KnownLayer.Background`）绘制，颜色为半透明黄色（`Colors.Yellow` @ 40% 透明度）。预览区目前无任何查找 UI、无 `Ctrl+F` 绑定、无 `Window.InputBindings`。

本设计通过复用 AvalonEdit 内置 `SearchPanel` 实现，经源码验证其已覆盖四个核心需求，自定义代码量极小。

## 方案选型

| 方案 | 说明 | 结论 |
|---|---|---|
| **A. 内置 SearchPanel（已选）** | `SearchPanel.Install(editor)` 一行获得 Ctrl+F / 上下一个 / 大小写·正则·全词切换。`MarkerBrush` 设橙色即可与黄色主高亮隔离。 | 复用成熟实现，代码量最小 |
| B. 自建 VS Code 风格查找栏 | 手写 XAML + 搜索/导航/高亮逻辑 | 工作量大数倍，无额外收益 |
| C. 内置 SearchPanel + 换肤 | 后期用 ControlTemplate 改造 UI | 换肤工作量不小，当前默认 UI 已可接受 |

选定方案 A。

## 内置能力验证（基于 AvalonEdit 源码）

阅读 `SearchPanel.cs` / `SearchCommands.cs` / `SearchPanel.xaml` 源码确认：

| 需求 | 内置实现 | 源码依据 |
|---|---|---|
| Ctrl+F 唤起 | ✅ 免费 | `Install` 创建 `SearchInputHandler` 并注册 `ApplicationCommands.Find`（WPF 默认手势 `Ctrl+F`） |
| 选中文本预填 | ✅ 免费 | `ExecuteFind`：选区非空且非多行时自动填入 `SearchPattern`，再 `Reactivate()` 全选便于覆盖输入 |
| 上次查找词保留 | ✅ 免费 | `SearchPattern` 是 DependencyProperty，`Close()` 不清空它；查找框 `TwoWay` 绑定，重开即显示上次词 |
| 切换文件重跑查找 | ✅ 基本免费 | `textArea_DocumentChanged` 自动调 `DoSearch(false)` 在新文档重跑——**仅当面板开启时**（`DoSearch` 首行 `if (IsClosed) return`）。关闭时不重高亮，等同 VS Code 行为 |
| 不同高亮色 | ✅ 一行 | 设 `MarkerBrush` = 橙色画刷。`SearchResultBackgroundRenderer` 画在 `KnownLayer.Selection`，与黄色 `TextMarkerService`（`KnownLayer.Background`）物理隔离 |

## 改动范围

### 1. `App.xaml`（1 行）

新增颜色资源，集中管理配色：

```xml
<SolidColorBrush x:Key="FindMatchBrush" Color="#ff9900"/>
```

### 2. `MainWindow.xaml.cs`（约 5 行核心改动）

- 新增字段：`private SearchPanel? _searchPanel;`（需 `using ICSharpCode.AvalonEdit.Search;`）
- 在 `OnContentRendered` 获取 editor 后：

```csharp
_searchPanel = SearchPanel.Install(editor);
_searchPanel.MarkerBrush = (Brush)FindResource("FindMatchBrush");
```

不需要：`Window.InputBindings`、`PreviewKeyDown`、在 `LoadFileContent` 手动重跑查找。`editor.Document = new TextDocument(...)` 触发的 `DocumentChanged` 事件已被 SearchPanel 订阅并自动重跑。

### 不改动

- `TextMarkerService`：主搜索黄色高亮逻辑完全不动
- `FilePreviewService`：文件加载/偏移计算不变
- `MainViewModel`：不新增属性/命令
- 任何 Model

## 双层高亮共存机制

| 层 | 渲染器 | AvalonEdit 层级 | 颜色 | 触发 |
|---|---|---|---|---|
| 主搜索匹配 | `TextMarkerService` | `KnownLayer.Background` | 半透明黄 `#FFFF00` @ 40% | 选中文件时由 `ApplyMatchMarkers` 创建 |
| 二次查找匹配 | `SearchResultBackgroundRenderer`（SearchPanel 内部） | `KnownLayer.Selection` | 橙色 `#ff9900` | 用户 Ctrl+F 查找时由 SearchPanel 创建 |

两个 `IBackgroundRenderer` 互不干扰。若同一文本既被主搜索命中又被二次查找命中，两层高亮叠加显示（橙+黄），视觉上可接受。

## 交互行为

- **唤起**：焦点在预览编辑器内时按 `Ctrl+F` → 查找栏出现在编辑器右上角（AdornerLayer）
- **预填**：有选中文本（单行）则填入选中文本；否则显示上次查找词（TextBox 全选便于覆盖）
- **导航**：`Enter` 下一个、`Shift+Enter` 上一个、`F3` 下一个、`Shift+F3` 上一个
- **选项**：下拉按钮切换大小写 / 全词 / 正则
- **关闭**：`Escape` 或点关闭按钮；关闭后清除橙色高亮，`SearchPattern` 保留
- **切换文件**：面板开启时自动在新文件重跑查找并保持橙色高亮；面板关闭时不重高亮（VS Code 行为）
- **无文件时**：二进制/错误/空状态下编辑器被遮罩覆盖、无内容、焦点不在编辑器，`Ctrl+F` 不会触发

## 边界与验证点

1. **无文件时 Ctrl+F 不泄漏**：依赖 `ApplicationCommands.Find` 绑定在 TextArea 上、焦点不在编辑器即不触发。实现时需实测二进制/错误状态，若确有泄漏再加 `CanExecute` 守卫。
2. **Install 时机**：必须在 `OnContentRendered` 获取到 editor 之后 Install，且 editor 实例在窗口生命周期内不变（现有代码已满足）。
3. **MarkerBrush 资源查找**：`FindResource("FindMatchBrush")` 要求资源已加载，放在 `OnContentRendered` 中安全。

## 测试策略

以手动验证为主（UI 交互难以单元测试），对照以下清单：

- [ ] 选中文件后，焦点在预览区按 Ctrl+F → 查找栏出现
- [ ] 输入词 → 橙色高亮所有匹配，主搜索黄色高亮仍在
- [ ] Enter/Shift+Enter/F3/Shift+F3 → 正确跳转上下一个
- [ ] 大小写/全词/正则切换生效
- [ ] 选中文本后 Ctrl+F → 预填选中文本
- [ ] 关闭查找栏（Esc）→ 橙色高亮消失，再次 Ctrl+F 显示上次词
- [ ] 切换到另一文件（面板开启）→ 自动在新文件重跑查找
- [ ] 切换到另一文件（面板关闭）→ 无橙色高亮，Ctrl+F 仍显示上次词
- [ ] 二进制/错误/空文件状态 → Ctrl+F 不唤起查找栏
- [ ] 大文件截断预览 → 查找仅在已加载部分生效

## 实现演进（原"后续可扩展"两项已落地）

初版仅做最小装配（Install + MarkerBrush），随后基于使用反馈做了以下增强，均超出本 spec 原始范围但已实现并记录于 CHANGELOG v1.4.0：

### 当前项深橙高亮

初版所有命中项统一浅橙，当前跳转项被 AvalonEdit 系统选区色（蓝绿）覆盖，视觉不统一。
**改法**：设 `editor.TextArea.SelectionBrush` = 深橙 `#e67300`（新增 `FindCurrentBrush` 资源）+ `SelectionForeground` = 白。
**权衡**：`SelectionBrush` 是全局选区色，手动选中文字也呈深橙（VS Code 同此行为，可接受）。

### 查找栏 ControlTemplate 重写（VS Code 风）

重写 `App.xaml` 中 `search:SearchPanel` 的 `ControlTemplate`：
- 外层 Border 圆角 6 + 投影（对齐 `HistoryPopupContent`）
- 输入框圆角 4、聚焦边框变蓝（对齐 `SearchInput`，含必需的 `PART_ContentHost`）
- 上/下/关闭按钮用 `Path` 几何图标替代原生 `prev.png`/`next.png`（FastDog 无这些图片资源）
- **必须保留** `PART_searchTextBox`、`PART_dropdownPopup` 及三个按钮的 `Command` 绑定

### 查找栏汉化

新增 `Helpers/SearchPanelLocalization.cs` 子类化 AvalonEdit `Localization`，覆盖 7 项文本为中文
（区分大小写 / 全词匹配 / 正则表达式 / 上下一个 / 未找到匹配项 / 错误），经 `_searchPanel.Localization` 应用。

### N/M 实时计数

内置 `SearchPanel` 不提供计数，其匹配集合 `renderer.CurrentResults` 为 internal。
**改法**：新增 `Helpers/PreviewFindCounter.cs`，订阅 `SearchOptionsChanged` + `Caret.PositionChanged` + `DocumentChanged`，
用公开 `SearchStrategyFactory.Create(...).FindAll(...)` 自行重算匹配 offset 列表，二分查找当前序号（对齐 VS Code，到末尾回绕）。
计数经 `SearchPanel.Tag`（object 属性）传入模板 TextBlock，空值时 `DataTrigger` 折叠隐藏。
**坑**：`ISearchResult`（继承 `ISegment`）的起始偏移属性是 `.Offset`，非 `.StartOffset`（后者只在具体类 `TextSegment`/`SearchResult` 上）。

