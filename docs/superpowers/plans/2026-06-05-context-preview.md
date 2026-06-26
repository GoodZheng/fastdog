# 上下文预览面板 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将下方匹配行列表区域左右拆分，左侧保留匹配行列表，右侧新增 AvalonEdit 文件预览面板，支持全文显示、语法高亮、匹配文本黄色标记、点击匹配行滚动定位。

**Architecture:** 引入 ICSharpCode.AvalonEdit NuGet 包作为文件预览控件。在 MainWindow.xaml 中将下方 ListBox 区域扩展为左右两列布局。MainViewModel 新增文件读取逻辑和匹配标记偏移计算。新建 FilePreviewService 负责文件加载、大文件截断、二进制检测等逻辑，与 ViewModel 解耦。

**Tech Stack:** ICSharpCode.AvalonEdit (NuGet), WPF Grid + GridSplitter 布局, CommunityToolkit.Mvvm

---

## File Structure

| Action | File | Responsibility |
|--------|------|---------------|
| Modify | `src/FastDog/FastDog.csproj` | 添加 AvalonEdit NuGet 引用 |
| Create | `src/FastDog/Services/FilePreviewService.cs` | 文件加载、大文件截断、二进制检测、匹配偏移计算 |
| Modify | `src/FastDog/Models/MatchLine.cs` | 新增 `GlobalOffset` 属性用于 AvalonEdit 定位 |
| Modify | `src/FastDog/ViewModels/MainViewModel.cs` | 新增文件预览相关属性和逻辑 |
| Modify | `src/FastDog/MainWindow.xaml` | 下方区域左右拆分 + AvalonEdit 控件 |
| Modify | `src/FastDog/MainWindow.xaml.cs` | AvalonEdit 初始化、TextMarkerService 注册、匹配标记渲染 |
| Create | `tests/FastDog.Tests/FilePreviewServiceTests.cs` | 文件预览服务单元测试 |

---

### Task 1: 添加 AvalonEdit NuGet 包

**Files:**
- Modify: `src/FastDog/FastDog.csproj`

- [ ] **Step 1: 添加 NuGet 包引用**

在 `FastDog.csproj` 的 `<ItemGroup>` 中添加 AvalonEdit 包：

```xml
<PackageReference Include="AvalonEdit" Version="6.3.0.90" />
```

完整的 `<ItemGroup>` 应变为：

```xml
<ItemGroup>
    <PackageReference Include="AvalonEdit" Version="6.3.0.90" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
</ItemGroup>
```

- [ ] **Step 2: 还原包并验证编译**

Run: `dotnet restore FastDog.sln && dotnet build FastDog.sln`
Expected: 编译成功，无错误

- [ ] **Step 3: Commit**

```bash
git add src/FastDog/FastDog.csproj
git commit -m "chore: add AvalonEdit NuGet package"
```

---

### Task 2: 创建 FilePreviewService

**Files:**
- Create: `src/FastDog/Services/FilePreviewService.cs`
- Create: `tests/FastDog.Tests/FilePreviewServiceTests.cs`

FilePreviewService 负责：文件读取、大文件截断（>5MB 显示前 5000 行）、二进制文件检测、行偏移 → 全局偏移转换。

- [ ] **Step 1: 编写 FilePreviewService 的失败测试**

创建 `tests/FastDog.Tests/FilePreviewServiceTests.cs`：

```csharp
using FastDog.Services;
using FastDog.Models;

namespace FastDog.Tests;

public class FilePreviewServiceTests
{
    private readonly FilePreviewService _service = new();

    [Fact]
    public void IsBinaryFile_KnownBinaryExtensions_ReturnsTrue()
    {
        Assert.True(_service.IsBinaryFile("app.exe"));
        Assert.True(_service.IsBinaryFile("lib.dll"));
        Assert.True(_service.IsBinaryFile("image.png"));
        Assert.True(_service.IsBinaryFile("photo.jpg"));
        Assert.True(_service.IsBinaryFile("archive.zip"));
        Assert.True(_service.IsBinaryFile("data.bin"));
        Assert.True(_service.IsBinaryFile("lib.so"));
        Assert.True(_service.IsBinaryFile("app.pdb"));
    }

    [Fact]
    public void IsBinaryFile_TextExtensions_ReturnsFalse()
    {
        Assert.False(_service.IsBinaryFile("Program.cs"));
        Assert.False(_service.IsBinaryFile("config.json"));
        Assert.False(_service.IsBinaryFile("page.xaml"));
        Assert.False(_service.IsBinaryFile("style.css"));
        Assert.False(_service.IsBinaryFile("readme.md"));
        Assert.False(_service.IsBinaryFile("script.py"));
        Assert.False(_service.IsBinaryFile("app.js"));
        Assert.False(_service.IsBinaryFile("FileWithNoExtension"));
    }

    [Fact]
    public void ReadFileContent_NonExistentFile_ReturnsError()
    {
        var result = _service.LoadFileContent(@"Z:\nonexistent\path\file.txt", out _);
        Assert.Null(result);
    }

    [Fact]
    public void ComputeGlobalOffsets_SingleLine()
    {
        // 模拟一个 3 行文件，行长度分别为 10, 20, 15（含换行符）
        var lineLengths = new[] { 11, 21, 15 }; // 含 \n 各 +1
        var match = new MatchLine { LineNumber = 2, MatchStart = 5, MatchEnd = 10 };
        var (start, end) = _service.ComputeGlobalOffset(lineLengths, match);
        // 第 1 行长度 11，所以第 2 行起始偏移 = 11
        Assert.Equal(11 + 5, start);
        Assert.Equal(11 + 10, end);
    }

    [Fact]
    public void ComputeGlobalOffsets_FirstLine()
    {
        var lineLengths = new[] { 11, 21, 15 };
        var match = new MatchLine { LineNumber = 1, MatchStart = 0, MatchEnd = 5 };
        var (start, end) = _service.ComputeGlobalOffset(lineLengths, match);
        Assert.Equal(0, start);
        Assert.Equal(5, end);
    }

    [Fact]
    public void ComputeGlobalOffsets_LastLine()
    {
        var lineLengths = new[] { 11, 21, 15 };
        var match = new MatchLine { LineNumber = 3, MatchStart = 3, MatchEnd = 8 };
        var (start, end) = _service.ComputeGlobalOffset(lineLengths, match);
        // 前两行总长 = 11 + 21 = 32
        Assert.Equal(32 + 3, start);
        Assert.Equal(32 + 8, end);
    }

    [Fact]
    public void ComputeLineLengths_MultilineContent()
    {
        var content = "line1\nline2 is longer\nline3\n";
        var lengths = _service.ComputeLineLengths(content);
        // "line1\n" = 6, "line2 is longer\n" = 17, "line3\n" = 6
        Assert.Equal(3, lengths.Length);
        Assert.Equal(6, lengths[0]);
        Assert.Equal(17, lengths[1]);
        Assert.Equal(6, lengths[2]);
    }

    [Fact]
    public void ReadFileContent_ValidTextFile_ReturnsContent()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "hello world\nline 2\n");
            var result = _service.LoadFileContent(tempFile, out var truncated);
            Assert.Equal("hello world\nline 2\n", result);
            Assert.False(truncated);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadFileContent_LargeFile_Truncates()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // 生成超过 5000 行的文件
            var lines = Enumerable.Range(1, 6000)
                .Select(i => $"line {i} content here padding padding padding");
            File.WriteAllLines(tempFile, lines);
            var result = _service.LoadFileContent(tempFile, out var truncated);
            Assert.NotNull(result);
            Assert.True(truncated);
            var resultLineCount = result.Split('\n').Length - 1; // 最后有个空行
            Assert.True(resultLineCount <= 5001, $"Expected <= 5001 lines, got {resultLineCount}");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test FastDog.sln --filter "FullyQualifiedName~FilePreviewServiceTests" -v n`
Expected: 编译失败或测试失败，因为 `FilePreviewService` 类不存在

- [ ] **Step 3: 实现 FilePreviewService**

创建 `src/FastDog/Services/FilePreviewService.cs`：

```csharp
using System.IO;
using FastDog.Models;

namespace FastDog.Services;

public class FilePreviewService
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
    private const int MaxLines = 5000;

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".obj", ".o", ".so", ".dylib",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".tif", ".tiff", ".webp",
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz",
        ".mp3", ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flac", ".wav",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".bin", ".dat", ".db", ".sqlite", ".mdb",
        ".class", ".jar", ".war", ".nupkg", ".snk",
        ".woff", ".woff2", ".ttf", ".eot",
    };

    public bool IsBinaryFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return BinaryExtensions.Contains(ext);
    }

    public string? LoadFileContent(string filePath, out bool truncated)
    {
        truncated = false;

        if (!File.Exists(filePath))
            return null;

        try
        {
            if (IsBinaryFile(filePath))
                return null;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSize)
            {
                truncated = true;
                return ReadFirstLines(filePath, MaxLines);
            }

            return File.ReadAllText(filePath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public (int start, int end) ComputeGlobalOffset(int[] lineLengths, MatchLine match)
    {
        int lineIndex = match.LineNumber - 1; // LineNumber 是 1-based
        int offset = 0;
        for (int i = 0; i < lineIndex; i++)
            offset += lineLengths[i];

        return (offset + match.MatchStart, offset + match.MatchEnd);
    }

    public int[] ComputeLineLengths(string content)
    {
        var lines = content.Split('\n');
        var lengths = new int[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            // 每行内容 + \n（最后一行如果没有 \n 则不加）
            lengths[i] = lines[i].Length;
            if (i < lines.Length - 1)
                lengths[i] += 1; // \n 字符
        }
        return lengths;
    }

    private static string ReadFirstLines(string filePath, int maxLines)
    {
        var lines = new List<string>(maxLines);
        using var reader = new StreamReader(filePath);
        for (int i = 0; i < maxLines; i++)
        {
            var line = reader.ReadLine();
            if (line is null) break;
            lines.Add(line);
        }
        return string.Join("\n", lines) + "\n";
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test FastDog.sln --filter "FullyQualifiedName~FilePreviewServiceTests" -v n`
Expected: 所有测试 PASS

- [ ] **Step 5: 运行全部测试确认无回归**

Run: `dotnet test FastDog.sln -v n`
Expected: 所有测试 PASS

- [ ] **Step 6: Commit**

```bash
git add src/FastDog/Services/FilePreviewService.cs tests/FastDog.Tests/FilePreviewServiceTests.cs
git commit -m "feat: add FilePreviewService with binary detection, large file truncation, and offset computation"
```

---

### Task 3: 更新 MatchLine 模型

**Files:**
- Modify: `src/FastDog/Models/MatchLine.cs:1-8`

- [ ] **Step 1: 添加 GlobalOffset 属性**

`MatchLine` 当前有 `MatchStart` / `MatchEnd`（行内偏移），新增 `GlobalMatchStart` / `GlobalMatchEnd` 存储文档全局偏移，供 AvalonEdit TextMarker 使用。

将 `src/FastDog/Models/MatchLine.cs` 改为：

```csharp
namespace FastDog.Models;

public class MatchLine
{
    public int LineNumber { get; set; }
    public string LineText { get; set; } = string.Empty;
    public int MatchStart { get; set; }
    public int MatchEnd { get; set; }

    // AvalonEdit 全局偏移（由 FilePreviewService.ComputeGlobalOffset 填充）
    public int GlobalMatchStart { get; set; }
    public int GlobalMatchEnd { get; set; }
}
```

- [ ] **Step 2: 运行全部测试确认无回归**

Run: `dotnet test FastDog.sln -v n`
Expected: 所有测试 PASS（新增属性有默认值 0，不影响现有逻辑）

- [ ] **Step 3: Commit**

```bash
git add src/FastDog/Models/MatchLine.cs
git commit -m "feat: add GlobalMatchStart/GlobalMatchEnd to MatchLine for AvalonEdit offset"
```

---

### Task 4: 更新 MainViewModel 添加文件预览逻辑

**Files:**
- Modify: `src/FastDog/ViewModels/MainViewModel.cs`

- [ ] **Step 1: 添加 FilePreviewService 字段和预览相关属性**

在 `MainViewModel.cs` 中添加以下内容：

1) 新增 `using` 和字段：

```csharp
using System.Linq;  // 已有，确认存在
```

在 `MainViewModel` 类中，`_searchService` 后面添加：

```csharp
private readonly FilePreviewService _previewService = new();
```

2) 新增 ObservableProperty：

```csharp
[ObservableProperty] private string _fileContent = string.Empty;
[ObservableProperty] private string _filePath = string.Empty;
[ObservableProperty] private bool _isBinaryFile = false;
[ObservableProperty] private bool _isFileError = false;
[ObservableProperty] private string _fileErrorMessage = string.Empty;
[ObservableProperty] private bool _isFileTruncated = false;
```

3) 新增事件供 View 监听滚动：

```csharp
public event Action<int>? ScrollToLineRequested;
```

- [ ] **Step 2: 修改 OnSelectedResultChanged 加载文件内容**

将现有的 `OnSelectedResultChanged` 替换为：

```csharp
partial void OnSelectedResultChanged(SearchResult? value)
{
    MatchLines.Clear();
    FileContent = string.Empty;
    FilePath = string.Empty;
    IsBinaryFile = false;
    IsFileError = false;
    FileErrorMessage = string.Empty;
    IsFileTruncated = false;

    if (value is null) return;

    // 填充匹配行
    foreach (var match in value.Matches)
        MatchLines.Add(match);

    // 检测二进制文件
    if (_previewService.IsBinaryFile(value.FilePath))
    {
        IsBinaryFile = true;
        return;
    }

    // 加载文件内容
    var content = _previewService.LoadFileContent(value.FilePath, out var truncated);
    if (content is null)
    {
        if (!File.Exists(value.FilePath))
        {
            IsFileError = true;
            FileErrorMessage = "文件未找到";
        }
        else
        {
            IsFileError = true;
            FileErrorMessage = "无法读取文件";
        }
        return;
    }

    IsFileTruncated = truncated;
    FilePath = value.FilePath;
    FileContent = content;

    // 计算全局偏移
    var lineLengths = _previewService.ComputeLineLengths(content);
    foreach (var match in value.Matches)
    {
        var (start, end) = _previewService.ComputeGlobalOffset(lineLengths, match);
        match.GlobalMatchStart = start;
        match.GlobalMatchEnd = end;
    }
}
```

- [ ] **Step 3: 添加 OnSelectedMatchLineChanged 处理滚动**

在 `MainViewModel` 中添加新的 partial method：

```csharp
partial void OnSelectedMatchLineChanged(MatchLine? value)
{
    if (value is not null)
        ScrollToLineRequested?.Invoke(value.LineNumber);
}
```

- [ ] **Step 4: 运行全部测试确认无回归**

Run: `dotnet test FastDog.sln -v n`
Expected: 所有测试 PASS

- [ ] **Step 5: Commit**

```bash
git add src/FastDog/ViewModels/MainViewModel.cs
git commit -m "feat: add file preview logic to MainViewModel with binary detection and offset computation"
```

---

### Task 5: 更新 MainWindow.xaml 布局（左右拆分）

**Files:**
- Modify: `src/FastDog/MainWindow.xaml:100-151`

- [ ] **Step 1: 添加 AvalonEdit XML 命名空间**

在 `MainWindow.xaml` 的 `<Window>` 标签中添加命名空间：

```xml
xmlns:avalonedit="http://icsharpcode.net/sharpdevelop/avalonedit"
```

- [ ] **Step 2: 将下方区域改为左右两列布局**

将 `MainWindow.xaml` 中 Grid Row 2（`<!-- 行内容预览 -->` 那个部分，约第 134-149 行）替换为：

```xml
            <!-- 行内容预览（左右拆分） -->
            <Grid Grid.Row="2">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="35*" MinWidth="200"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="65*" MinWidth="300"/>
                </Grid.ColumnDefinitions>

                <!-- 左侧：匹配行列表 -->
                <ListBox Grid.Column="0"
                         ItemsSource="{Binding MatchLines}"
                         SelectedItem="{Binding SelectedMatchLine}"
                         MouseDoubleClick="MatchList_MouseDoubleClick">
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <StackPanel Orientation="Horizontal">
                                <TextBlock Text="{Binding LineNumber}" Width="50"
                                           Foreground="Gray" FontWeight="Bold"
                                           VerticalAlignment="Center"/>
                                <TextBlock Text="{Binding LineText}" TextTrimming="CharacterEllipsis"/>
                            </StackPanel>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>

                <GridSplitter Grid.Column="1" Width="4" HorizontalAlignment="Stretch"
                              VerticalAlignment="Stretch"/>

                <!-- 右侧：文件预览 -->
                <Grid Grid.Column="2">
                    <!-- 正常预览 -->
                    <avalonedit:TextEditor Name="PreviewEditor"
                        Visibility="{Binding IsBinaryFile, Converter={StaticResource InvertBoolToVisibility}}"
                        FontFamily="Consolas"
                        FontSize="12"
                        IsReadOnly="True"
                        ShowLineNumbers="True"
                        WordWrap="False"/>

                    <!-- 二进制文件提示 -->
                    <TextBlock Text="二进制文件，无法预览"
                               Visibility="{Binding IsBinaryFile, Converter={StaticResource BoolToVisibility}}"
                               HorizontalAlignment="Center" VerticalAlignment="Center"
                               Foreground="Gray" FontSize="14"/>

                    <!-- 错误提示 -->
                    <TextBlock Text="{Binding FileErrorMessage}"
                               Visibility="{Binding IsFileError, Converter={StaticResource BoolToVisibility}}"
                               HorizontalAlignment="Center" VerticalAlignment="Center"
                               Foreground="Gray" FontSize="14"/>

                    <!-- 截断提示 -->
                    <TextBlock Text="文件过大，仅显示部分内容"
                               Visibility="{Binding IsFileTruncated, Converter={StaticResource BoolToVisibility}}"
                               HorizontalAlignment="Right" VerticalAlignment="Top"
                               Foreground="Orange" FontSize="11" Margin="4"
                               Panel.ZIndex="1"/>
                </Grid>
            </Grid>
```

- [ ] **Step 3: 添加 BooleanToVisibility 转换器资源**

在 `MainWindow.xaml` 的 `<Window.Resources>` 中（如果不存在则添加）添加转换器。在 `<DockPanel>` 之前添加：

```xml
    <Window.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVisibility"/>
        <local:InvertBoolToVisibilityConverter x:Key="InvertBoolToVisibility"/>
    </Window.Resources>
```

注意：WPF 自带 `BooleanToVisibilityConverter`，但需要一个反转版本。这个反转版本在 `MainWindow.xaml.cs` 中定义（见 Task 6）。

- [ ] **Step 4: 验证编译**

Run: `dotnet build FastDog.sln`
Expected: 编译可能因缺少 `InvertBoolToVisibilityConverter` 类而失败（将在 Task 6 中修复）

- [ ] **Step 5: Commit**

```bash
git add src/FastDog/MainWindow.xaml
git commit -m "feat: split bottom area into match list (left) and file preview (right)"
```

---

### Task 6: 更新 MainWindow.xaml.cs（AvalonEdit 初始化与交互）

**Files:**
- Modify: `src/FastDog/MainWindow.xaml.cs`

- [ ] **Step 1: 添加反转转换器和 AvalonEdit 交互逻辑**

将 `MainWindow.xaml.cs` 完整替换为：

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FastDog.ViewModels;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace FastDog;

public class InvertBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public partial class MainWindow : Window
{
    private MainViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        _vm = DataContext as MainViewModel;
        if (_vm is null) return;

        var editor = FindEditor();
        if (editor is null) return;

        // 监听 ViewModel 属性变更
        _vm.PropertyChanged += (s, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(MainViewModel.FileContent):
                    LoadFileContent(editor, _vm);
                    break;
                case nameof(MainViewModel.FilePath):
                    ApplySyntaxHighlighting(editor, _vm.FilePath);
                    break;
            }
        };

        // 监听滚动请求
        _vm.ScrollToLineRequested += lineNumber =>
        {
            Dispatcher.Invoke(() =>
            {
                if (lineNumber >= 1 && lineNumber <= editor.Document.LineCount)
                    editor.ScrollTo(lineNumber, 1);
            });
        };
    }

    private void LoadFileContent(TextEditor editor, MainViewModel vm)
    {
        // 清除旧的匹配标记
        ClearMarkers(editor);

        if (string.IsNullOrEmpty(vm.FileContent))
        {
            editor.Document = new TextDocument();
            return;
        }

        editor.Document = new TextDocument(vm.FileContent);

        // 高亮所有匹配文本
        ApplyMatchMarkers(editor, vm);
    }

    private void ApplyMatchMarkers(TextEditor editor, MainViewModel vm)
    {
        if (vm.SelectedResult is null) return;

        var textArea = editor.TextArea;
        var textView = textArea.TextView;

        // 确保 TextMarkerService 已注册
        var markerService = EnsureMarkerService(textArea);

        foreach (var match in vm.SelectedResult.Matches)
        {
            if (match.GlobalMatchStart < 0 || match.GlobalMatchEnd <= match.GlobalMatchStart)
                continue;
            if (match.GlobalMatchEnd > editor.Document.TextLength)
                continue;

            var marker = markerService.Create(match.GlobalMatchStart,
                match.GlobalMatchEnd - match.GlobalMatchStart);
            marker.BackgroundColor = System.Windows.Media.Colors.Yellow;
        }
    }

    private TextMarkerService? _currentMarkerService;

    private TextMarkerService EnsureMarkerService(TextArea textArea)
    {
        if (_currentMarkerService is not null)
            return _currentMarkerService;

        _currentMarkerService = new TextMarkerService(textArea);
        textArea.TextView.BackgroundRenderers.Add(_currentMarkerService);
        textArea.TextView.LineTransformers.Add(_currentMarkerService);
        return _currentMarkerService;
    }

    private void ClearMarkers(TextEditor editor)
    {
        if (_currentMarkerService is null) return;

        _currentMarkerService.RemoveAll();
        var textArea = editor.TextArea;
        textArea.TextView.BackgroundRenderers.Remove(_currentMarkerService);
        textArea.TextView.LineTransformers.Remove(_currentMarkerService);
        _currentMarkerService = null;
    }

    private static void ApplySyntaxHighlighting(TextEditor editor, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        var ext = System.IO.Path.GetExtension(filePath);
        var highlighting = HighlightingManager.Instance.GetDefinitionByExtension(ext);
        editor.SyntaxHighlighting = highlighting;
    }

    private TextEditor? FindEditor()
    {
        return this.FindName("PreviewEditor") as TextEditor;
    }

    private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OpenFileCommand.Execute(null);
    }

    private void MatchList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OpenFileAtLineCommand.Execute(null);
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files.Length > 0 && System.IO.Directory.Exists(files[0]))
            {
                if (DataContext is MainViewModel vm)
                    vm.SearchPath = files[0];
            }
        }
    }
}
```

- [ ] **Step 2: 创建 TextMarkerService 辅助类**

AvalonEdit 的 `TextMarkerService` 需要自定义实现。创建 `src/FastDog/Services/TextMarkerService.cs`：

```csharp
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace FastDog.Services;

public sealed class TextMarker : TextSegment
{
    public Color BackgroundColor { get; set; }
}

public sealed class TextMarkerService : IBackgroundRenderer, IVisualLineTransformer
{
    private readonly TextArea _textArea;
    private readonly List<TextMarker> _markers = [];
    private readonly TextSegmentCollection<TextMarker> _segments;

    public TextMarkerService(TextArea textArea)
    {
        _textArea = textArea;
        _segments = new TextSegmentCollection<TextMarker>(textArea.Document);
    }

    public KnownLayer Layer => KnownLayer.Background;

    public TextMarker Create(int startOffset, int length)
    {
        var marker = new TextMarker
        {
            StartOffset = startOffset,
            Length = length,
            BackgroundColor = Colors.Yellow
        };
        _markers.Add(marker);
        _segments.Add(marker);
        _textArea.TextView.InvalidateLayer(KnownLayer.Background);
        return marker;
    }

    public void RemoveAll()
    {
        _markers.Clear();
        _segments.Clear();
        _textArea.TextView.InvalidateLayer(KnownLayer.Background);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_segments.Count == 0 || textView.Document == null) return;

        foreach (var line in textView.VisualLines)
        {
            var lineStart = line.FirstDocumentLine.Offset;
            var lineEnd = line.LastDocumentLine.Offset + line.LastDocumentLine.Length;

            foreach (var marker in _segments.FindOverlappingSegments(lineStart, lineEnd - lineStart))
            {
                var geoBuilder = new BackgroundGeometryBuilder
                {
                    AlignToWholePixels = true,
                    CornerRadius = 2
                };
                geoBuilder.AddSegment(textView, marker);

                var geo = geoBuilder.CreateGeometry();
                if (geo is not null)
                {
                    var brush = new SolidColorBrush(marker.BackgroundColor) { Opacity = 0.4 };
                    drawingContext.DrawGeometry(brush, null, geo);
                }
            }
        }
    }

    public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
    {
        // 不需要文本变换，匹配标记通过 BackgroundRenderer 绘制
    }
}
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build FastDog.sln`
Expected: 编译成功

- [ ] **Step 4: 运行全部测试确认无回归**

Run: `dotnet test FastDog.sln -v n`
Expected: 所有测试 PASS

- [ ] **Step 5: Commit**

```bash
git add src/FastDog/MainWindow.xaml.cs src/FastDog/Services/TextMarkerService.cs
git commit -m "feat: add AvalonEdit preview with TextMarker highlighting and scroll-to-line"
```

---

### Task 7: 手动功能验证

**Files:** 无代码变更

- [ ] **Step 1: 启动应用**

Run: `dotnet run --project src/FastDog`

- [ ] **Step 2: 验证基本搜索**

1. 设置搜索路径为一个包含文本文件的目录（如项目自身的 `src` 目录）
2. 搜索 "class"，点击搜索
3. 确认文件列表正确显示

- [ ] **Step 3: 验证文件预览**

1. 点击文件列表中的一个 `.cs` 文件
2. 确认右侧 AvalonEdit 显示文件全文内容
3. 确认有行号显示
4. 确认有语法高亮（关键字着色）
5. 确认匹配文本有黄色背景标记

- [ ] **Step 4: 验证点击匹配行滚动**

1. 点击左侧匹配行列表中不同的匹配行
2. 确认右侧预览区自动滚动到对应行
3. 确认滚动位置居中或可见

- [ ] **Step 5: 验证左右面板拖拽调整**

1. 拖拽中间的 GridSplitter
2. 确认可以调整匹配行列表和预览区的宽度比例

- [ ] **Step 6: 验证边界情况**

1. 搜索结果包含 `.exe` 文件 → 右侧显示"二进制文件，无法预览"
2. 双击匹配行 → VS Code 跳转仍然正常工作
