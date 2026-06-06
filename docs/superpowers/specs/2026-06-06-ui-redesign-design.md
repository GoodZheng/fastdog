# FastDog UI 现代化改造设计

> 基于 ui-option-d.html 参考设计，纯 XAML 样式重写，不引入第三方 UI 框架。

## 颜色主题

在 `App.xaml` 中定义 `SolidColorBrush` 资源，全局统一使用：

| 资源键 | 色值 | 用途 |
|--------|------|------|
| `AccentBrush` | `#3498db` | 主强调色（选中标签、徽章、输入框焦点边框、状态栏数字） |
| `DarkBrush` | `#2c3e50` | 深色文字、状态栏背景、Logo "Dog" |
| `DangerBrush` | `#e74c3c` | 取消按钮、Logo "Fast"、匹配行计数 |
| `SuccessBrush` | `#27ae60` | 搜索按钮 |
| `WarningBrush` | `#f39c12` | 匹配行选中左边框 |
| `GrayLightBrush` | `#ecf0f1` | 面板标题背景、标签栏背景、表头背景 |
| `GrayMidBrush` | `#bdc3c7` | 分割线、标签栏底线、次要边框 |
| `GrayTextBrush` | `#7f8c8d` | 次要文字（标签、面板标题、未选中标签） |
| `MutedTextBrush` | `#999` | 提示文字（路径、日期） |
| `HighlightBgBrush` | `#fef9e7` | 匹配行选中背景、代码行选中背景 |
| `HighlightFgBrush` | `#e67e22` | 匹配文本高亮色 |
| `HoverBgBrush` | `#f8f9fa` | 行 hover 背景 |
| `SelectedRowBrush` | `#eaf2f8` | DataGrid 选中行背景 |

## 布局结构

整体窗口结构（从上到下）：

```
DockPanel
├── Header (DockPanel.Dock=Top)     — 搜索条件区
├── StatusBar (DockPanel.Dock=Bottom) — 深色状态栏
├── TabBar (DockPanel.Dock=Top)      — 自定义标签栏
└── MainContent                      — 根据选中标签切换显示
    ├── ResultsView                  — 搜索结果视图
    │   ├── FileTable (DataGrid)     — 文件列表（flex:3）
    │   ├── GridSplitter
    │   └── BottomPanel (Grid)       — 匹配行 + 预览（flex:2）
    └── HistoryView                  — 搜索历史视图（默认隐藏）
```

## 各区域详细设计

### 1. 头部搜索区

白色背景 (`#fff`)，底部 `2px #e0e0e0` 边框，`Padding: 14px 18px`。

**Logo 行**: `Fast` 红色加粗 + `Dog` 深色加粗 + 版本号灰色小字。

**输入行**: 两行输入（搜索路径、搜索内容），左侧灰色 label（50px 宽），输入框 `border: 2px #ddd`，`border-radius: 5px`，focus 时边框变 `#3498db`。搜索路径行末尾有"浏览..."按钮。

**按钮行**: 一行 ToggleButton 风格按钮：
- 正则表达式 / 竖文本：互斥 toggle，选中时 `background: AccentBrush, color: #fff, border: AccentBrush`
- 区分大小写 / 全词匹配：独立 toggle
- 分隔符 `|`
- 文件过滤标签按钮：显示 "文件: {值}"，点击弹出输入框或直接可编辑
- 排除目录标签按钮：显示 "排除: {值}"，同上
- 分隔符 `|`
- 日期范围按钮：点击展开/收起日期选择器
- 右侧：取消按钮（DangerBrush）+ 搜索按钮（SuccessBrush，加粗）

**文件过滤/排除目录标签按钮的交互**：
- 默认显示为按钮样式（如 "文件: *.cs"）
- 点击后切换为内联编辑模式（显示一个 TextBox 覆盖按钮区域）
- 回车或失焦确认，恢复为按钮显示
- 空值时显示为 "文件: *" 或 "排除: (无)"

### 2. 自定义标签栏

灰色背景 (`GrayLightBrush`)，两个 `RadioButton` 样式按钮：
- 未选中：灰色文字，透明底边框
- 选中：深色加粗文字，`2px AccentBrush` 底边框
- 历史标签右侧显示计数徽章（小圆角矩形，未选中时 `GrayMidBrush` 背景，选中时 `AccentBrush` 背景）

通过绑定 `bool` 属性控制两个内容面板的 `Visibility`。

### 3. 文件列表 (DataGrid)

重新样式化 `DataGrid`：
- 表头：`GrayLightBrush` 背景，`GrayTextBrush` 文字，`2px GrayMidBrush` 底边框，`font-size: 11px`
- 行：白色背景，hover 时 `HoverBgBrush`，选中时 `SelectedRowBrush`
- 文件名列：加粗，`DarkBrush` 颜色
- 匹配数列：蓝色徽章（`AccentBrush` 圆角矩形，白色加粗数字）
- 路径列：`MutedTextBrush`，截断显示
- 日期列：`MutedTextBrush`，`font-size: 11px`
- 网格线：仅底部 `1px #ecf0f1`
- 保留复选框列

### 4. 下半区：匹配行 + 预览

与当前布局一致（左右分割），只做样式更新：

**匹配行面板** (40% 宽度)：
- 面板标题：`GrayLightBrush` 背景，"匹配行 {count} — {文件名}"，count 用 `DangerBrush` 加粗
- 行：Consolas 字体，行号灰色，匹配文本 `HighlightFgBrush` + `HighlightBgBrush` 背景
- 选中行：`HighlightBgBrush` 背景 + `3px WarningBrush` 左边框

**预览面板** (60% 宽度)：
- 面板标题：`GrayLightBrush` 背景，"文件预览 — {文件名}"
- AvalonEdit 控件保持现有功能（行号、语法高亮、匹配标记）
- TextMarkerService 的匹配标记颜色更新为 `HighlightBgBrush` 背景 + `WarningBrush` 边框

### 5. 搜索历史视图

卡片列表，替代当前 DataGrid：
- 工具栏：灰色浅背景，标题 "搜索历史"，提示文字，右侧 "清空历史" 按钮
- 每条历史显示为卡片行（`ItemsControl` + `DataTemplate`）：
  - 左侧：搜索词（Consolas 加粗）、路径（灰色）、选项标签（小圆角色块，正则/大小写等激活时蓝色背景）
  - 中间：统计数据（文件数、命中数、匹配数，数字蓝色加粗）
  - 右侧：时间（灰色）
- 右键菜单保留当前功能（使用条件、使用并搜索、删除、清空）

### 6. 状态栏

深色背景 (`DarkBrush`)，浅色文字 (`#ecf0f1`)：
- 左侧：状态文本，数字部分用 `AccentBrush` 加粗
- 右侧：耗时，数字部分用 `AccentBrush` 加粗
- 会话恢复提示继续使用 `StatusText` 显示，用户操作后自动清除（现有逻辑不变）

## 需要的 ViewModel 变更

最小化 ViewModel 改动：

1. **新增属性**: `bool IsResultsTab` (默认 true), `bool IsHistoryTab` — 用于标签栏切换
2. **新增属性**: `bool IsFileFilterEditing`, `bool IsExcludeEditing` — 用于文件过滤/排除的内联编辑模式
3. **移除**: 无（现有属性全部保留）
4. **标签切换命令**: `SwitchToResultsTabCommand`, `SwitchToHistoryTabCommand` 或直接用 `IsResultsTab` setter 联动

## 不改动的部分

- RipgrepBridge、SearchService、FilePreviewService、TextMarkerService、SearchHistoryService — 服务层不动
- Models（SearchQuery, SearchResult, MatchLine, SearchHistoryEntry）— 不动
- MainWindow.xaml.cs 的 code-behind 逻辑（事件处理、AvalonEdit 集成、拖放等）— 基本不动，仅因布局变化微调 FindName 等
- 测试 — 不动
