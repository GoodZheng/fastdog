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
  Models/          SearchQuery, SearchResult, MatchLine, SearchHistoryEntry
  Services/        RipgrepBridge (rg 进程管理), SearchService (业务逻辑), FilePreviewService (文件加载/偏移计算), TextMarkerService (匹配高亮渲染), SearchHistoryService (搜索历史JSON持久化)
  ViewModels/      MainViewModel
  MainWindow.xaml  搜索条件区 + 会话恢复提示条 + 标签页(搜索结果/历史) + 匹配行列表(左) + 文件预览(右)
tests/FastDog.Tests/
  ArgumentBuilderTests, JsonParserTests, DateFilterTests, FilePreviewServiceTests, SearchHistoryServiceTests
tools/
  rg.exe           ripgrep 可执行文件
docs/
  superpowers/specs/   设计文档
  superpowers/plans/   实现计划
```

## 已实现功能

- 纯文本搜索 / 正则表达式搜索
- 大小写敏感、全词匹配
- 文件名通配符过滤（`*.cs;*.txt`）
- 目录排除（默认排除 .git）
- 按修改日期范围过滤
- 搜索结果：文件列表（DataGrid）+ 匹配行列表（左）+ 文件预览面板（右，AvalonEdit）
- 文件预览：全文显示、行号、语法高亮、所有匹配文本黄色标记
- 点击匹配行自动滚动到预览区对应位置
- 大文件（>5MB）截断显示，二进制文件提示"无法预览"
- 双击打开文件 / 双击匹配行跳转行号（VS Code）
- 右键菜单：复制路径、复制文件名
- 拖放文件夹设置搜索路径
- 状态栏：文件数、匹配数、耗时
- 搜索历史：记录最近 50 条搜索（去重合并），可查看/恢复/删除/清空
- 会话恢复：下次启动自动恢复上次关闭时的搜索条件（不自动搜索）
- 标签页：搜索结果 / 搜索历史 切换
- 历史右键菜单：使用条件、使用条件并搜索、删除、清空

## 构建与运行

```powershell
dotnet build FastDog.sln
dotnet run --project src/FastDog
dotnet test FastDog.sln
```

## 架构

三层架构：UI (WPF MVVM) → SearchService → RipgrepBridge → rg.exe

- **RipgrepBridge**: 查找 rg.exe、构建命令参数、解析 JSON 输出、管理进程生命周期
- **SearchService**: 搜索编排、文件结果聚合、日期过滤、事件通知
- **FilePreviewService**: 文件加载、二进制检测、大文件截断（>5MB/5000行）、行内偏移→全局偏移转换
- **TextMarkerService**: AvalonEdit IBackgroundRenderer，黄色半透明背景标记匹配文本
- **MainViewModel**: 所有 UI 状态和命令，通过事件接收搜索结果，选中文件时加载预览并计算匹配偏移
- **SearchHistoryService**: 搜索历史持久化（JSON，%APPDATA%\FastDog\），去重合并、50条上限、会话保存/恢复

rg.exe 通过 `--json` 模式输出，`IAsyncEnumerable<RgEvent>` 流式推送结果到 UI。
