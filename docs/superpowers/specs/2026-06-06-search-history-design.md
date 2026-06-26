# FastDog 搜索历史与会话恢复 — 设计文档

## 背景

FastDog 当前每次启动都是空白状态，用户需要重新输入搜索条件。需要：
1. 记录搜索历史，用户可回溯和复用过去的搜索
2. 关闭程序后下次启动恢复上次的搜索条件（不自动搜索）

## 决策

| 项 | 决定 |
|---|---|
| 持久化方式 | JSON 文件，用 `System.Text.Json`（.NET 8 内置，零依赖） |
| 存储位置 | `%APPDATA%\FastDog\search-history.json` |
| 历史上限 | 50 条，超出后丢弃最旧的 |
| 去重策略 | 相同搜索条件（关键词+路径+选项）合并，更新统计和时间戳 |
| 会话恢复 | 仅恢复搜索条件到 UI，不自动执行搜索 |

## 数据模型

### SearchHistoryEntry (`Models/SearchHistoryEntry.cs`)

```
搜索条件：SearchPath, SearchText, IsRegex, CaseSensitive, WholeWord, FileFilter, ExcludeDirs, DateFilterEnabled, DateFrom, DateTo
统计信息：SearchedFiles, FoundFiles, TotalMatches, ElapsedTime
时间戳：SearchedAt
```

去重键：`SearchText + SearchPath + IsRegex + CaseSensitive + WholeWord + FileFilter + ExcludeDirs`

### 存储文件格式 (`%APPDATA%\FastDog\search-history.json`)

```json
{
  "LastSession": {
    "SearchPath": "...",
    "SearchText": "...",
    ...
  },
  "History": [
    { "SearchPath": "...", "SearchText": "...", "SearchedAt": "...", ... },
    ...
  ]
}
```

- `LastSession`：上次关闭时的搜索条件（用于会话恢复）
- `History`：搜索历史列表，按时间倒序，最多 50 条

## 新增组件

### SearchHistoryService (`Services/SearchHistoryService.cs`)

职责：
- `Load()` — 从 JSON 文件加载历史和上次会话
- `Save()` — 保存历史和当前会话到 JSON 文件
- `AddEntry(SearchHistoryEntry)` — 添加/合并历史记录，超限时裁剪
- `ClearHistory()` — 清空历史
- `GetLastSession()` → `SearchHistoryEntry?` — 获取上次会话条件
- `SaveCurrentSession(SearchHistoryEntry)` — 保存当前会话

文件不存在时自动创建目录和空文件。

## UI 改动（按 ui-option-d.html）

### 1. 顶部搜索栏 — 搜索输入框下拉提示

`SearchText` 输入框获得焦点且非空时，弹出下拉列表显示匹配的历史关键词，点击可快速填充。使用 WPF Popup + ListBox 实现。

### 2. 标签栏 — 新增"搜索历史"标签页

在搜索结果 DataGrid 上方添加 TabControl：
- "搜索结果"标签：显示现有的文件列表（保持不变）
- "搜索历史"标签：显示历史记录列表

### 3. 搜索历史列表布局

用 DataGrid 展示，列：
| 列 | 绑定 | 宽度 |
|---|---|---|
| 搜索内容 | SearchText | * |
| 路径 | SearchPath | 250 |
| 选项标签 | 计算列（正则/文本、大小写、全词） | 160 |
| 结果 | "已搜索 N 文件，M 命中，K 匹配" | 200 |
| 时间 | SearchedAt | 130 |

双击历史行 → 填充搜索条件到顶部输入框（不自动搜索）。
右键菜单："使用此条件搜索"、"删除"、"清空所有历史"。

### 4. 会话恢复提示条

程序启动时，如果检测到 `LastSession` 数据：
- 在搜索栏下方显示绿色提示条："已恢复上次关闭时的搜索状态"
- 提示条上有"关闭"按钮，关闭后不再显示
- 搜索条件已填充到输入框，用户手动点"搜索"即可

## MainViewModel 改动

新增属性和命令：
- `ObservableCollection<SearchHistoryEntry> HistoryEntries`
- `SearchHistoryEntry? SelectedHistoryEntry`
- `IAsyncRelayCommand UseHistoryCommand` — 双击历史条目，填充条件
- `IRelayCommand DeleteHistoryCommand` — 删除选中历史
- `IRelayCommand ClearHistoryCommand` — 清空所有历史

搜索完成时：调用 `SearchHistoryService.AddEntry()` 记录历史。
窗口关闭时：调用 `SearchHistoryService.SaveCurrentSession()` 保存当前条件。
窗口加载时：调用 `SearchHistoryService.GetLastSession()` 恢复条件。

## 修改文件清单

| 文件 | 操作 |
|---|---|
| `Models/SearchHistoryEntry.cs` | 新增 |
| `Services/SearchHistoryService.cs` | 新增 |
| `ViewModels/MainViewModel.cs` | 修改 — 添加历史属性/命令、搜索完成时记录、启动时恢复 |
| `MainWindow.xaml` | 修改 — 添加标签栏、历史 DataGrid、会话恢复提示条、下拉建议 |
| `MainWindow.xaml.cs` | 修改 — 处理历史相关的事件（双击、右键菜单） |

## 测试

- 新增 `SearchHistoryServiceTests.cs`：测试添加/去重/上限裁剪/序列化
- 手动测试：启动 → 搜索 → 关闭 → 重新启动，验证条件恢复和历史记录
