# 搜索历史与会话恢复 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 FastDog 添加搜索历史记录和会话恢复功能，用户可以回溯过去的搜索，关闭程序后下次启动恢复上次的搜索条件。

**Architecture:** 新增 `SearchHistoryEntry` 模型和 `SearchHistoryService` 服务，用 `System.Text.Json` 序列化到 `%APPDATA%\FastDog\search-history.json`。ViewModel 在搜索完成时记录历史，窗口关闭时保存会话，启动时恢复条件。UI 添加标签栏切换搜索结果/历史列表，以及会话恢复提示条。

**Tech Stack:** .NET 8 (System.Text.Json 内置), WPF + CommunityToolkit.Mvvm 8.4, xUnit

---

### Task 1: 创建 SearchHistoryEntry 模型

**Files:**
- Create: `src/FastDog/Models/SearchHistoryEntry.cs`

- [ ] **Step 1: 创建模型文件**

```csharp
namespace FastDog.Models;

public class SearchHistoryEntry
{
    public string SearchPath { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public bool IsRegex { get; set; } = true;
    public bool CaseSensitive { get; set; }
    public bool WholeWord { get; set; }
    public string FileFilter { get; set; } = string.Empty;
    public string ExcludeDirs { get; set; } = string.Empty;
    public bool DateFilterEnabled { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public long SearchedFiles { get; set; }
    public int FoundFiles { get; set; }
    public int TotalMatches { get; set; }
    public string ElapsedTime { get; set; } = string.Empty;

    public DateTime SearchedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 用于去重判断的组合键
    /// </summary>
    public string DedupKey => $"{SearchText}|{SearchPath}|{IsRegex}|{CaseSensitive}|{WholeWord}|{FileFilter}|{ExcludeDirs}";

    /// <summary>
    /// UI 显示用的选项摘要
    /// </summary>
    public string OptionsSummary
    {
        get
        {
            var parts = new List<string>();
            parts.Add(IsRegex ? "正则" : "文本");
            if (CaseSensitive) parts.Add("区分大小写");
            if (WholeWord) parts.Add("全词");
            if (!string.IsNullOrEmpty(FileFilter)) parts.Add(FileFilter);
            if (!string.IsNullOrEmpty(ExcludeDirs) && ExcludeDirs != "bin;obj")
                parts.Add($"排除: {ExcludeDirs}");
            return string.Join(", ", parts);
        }
    }

    /// <summary>
    /// UI 显示用的结果摘要
    /// </summary>
    public string ResultSummary => $"搜索 {SearchedFiles:N0} 文件，{FoundFiles} 命中，{TotalMatches} 匹配";
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build src/FastDog/FastDog.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add src/FastDog/Models/SearchHistoryEntry.cs
git commit -m "feat: add SearchHistoryEntry model for search history persistence"
```

---

### Task 2: 创建 SearchHistoryService 并编写测试

**Files:**
- Create: `src/FastDog/Services/SearchHistoryService.cs`
- Create: `tests/FastDog.Tests/SearchHistoryServiceTests.cs`

- [ ] **Step 1: 编写 SearchHistoryService 测试**

```csharp
using FastDog.Services;
using FastDog.Models;

namespace FastDog.Tests;

public class SearchHistoryServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly SearchHistoryService _service;

    public SearchHistoryServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"FastDogTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _service = new SearchHistoryService(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void AddEntry_FirstEntry_HistoryHasOneItem()
    {
        var entry = MakeEntry("hello", @"C:\test");
        _service.AddEntry(entry);

        var history = _service.LoadHistory();
        Assert.Single(history);
        Assert.Equal("hello", history[0].SearchText);
    }

    [Fact]
    public void AddEntry_Duplicate_ReplacesOld()
    {
        _service.AddEntry(MakeEntry("hello", @"C:\test", foundFiles: 3));
        _service.AddEntry(MakeEntry("hello", @"C:\test", foundFiles: 7));

        var history = _service.LoadHistory();
        Assert.Single(history);
        Assert.Equal(7, history[0].FoundFiles);
    }

    [Fact]
    public void AddEntry_DifferentConditions_BothKept()
    {
        _service.AddEntry(MakeEntry("hello", @"C:\test"));
        _service.AddEntry(MakeEntry("hello", @"C:\other"));

        var history = _service.LoadHistory();
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void AddEntry_ExceedsMaxLimit_TrimsOldest()
    {
        for (int i = 0; i < 55; i++)
            _service.AddEntry(MakeEntry($"search_{i}", @"C:\test"));

        var history = _service.LoadHistory();
        Assert.Equal(50, history.Count);
        // 最新的排在最前面
        Assert.Equal("search_54", history[0].SearchText);
        Assert.Equal("search_5", history[49].SearchText);
    }

    [Fact]
    public void SaveCurrentSession_LoadLastSession_RoundTrip()
    {
        var session = MakeEntry("session_test", @"C:\session", isRegex: false);
        _service.SaveCurrentSession(session);

        var loaded = _service.GetLastSession();
        Assert.NotNull(loaded);
        Assert.Equal("session_test", loaded.SearchText);
        Assert.Equal(@"C:\session", loaded.SearchPath);
        Assert.False(loaded.IsRegex);
    }

    [Fact]
    public void GetLastSession_NoData_ReturnsNull()
    {
        var loaded = _service.GetLastSession();
        Assert.Null(loaded);
    }

    [Fact]
    public void ClearHistory_RemovesAllEntries()
    {
        _service.AddEntry(MakeEntry("a", @"C:\a"));
        _service.AddEntry(MakeEntry("b", @"C:\b"));
        _service.ClearHistory();

        var history = _service.LoadHistory();
        Assert.Empty(history);
    }

    [Fact]
    public void DeleteEntry_RemovesSpecificEntry()
    {
        _service.AddEntry(MakeEntry("keep", @"C:\keep"));
        var toDelete = MakeEntry("delete", @"C:\delete");
        _service.AddEntry(toDelete);

        _service.DeleteEntry(toDelete);
        var history = _service.LoadHistory();
        Assert.Single(history);
        Assert.Equal("keep", history[0].SearchText);
    }

    [Fact]
    public void HistoryIsOrderedByTimeDescending()
    {
        _service.AddEntry(MakeEntry("old", @"C:\test", searchedAt: DateTime.Now.AddHours(-2)));
        _service.AddEntry(MakeEntry("new", @"C:\test2", searchedAt: DateTime.Now));

        var history = _service.LoadHistory();
        Assert.Equal("new", history[0].SearchText);
        Assert.Equal("old", history[1].SearchText);
    }

    private static SearchHistoryEntry MakeEntry(
        string searchText, string path, int foundFiles = 0, bool isRegex = true,
        DateTime? searchedAt = null)
    {
        return new SearchHistoryEntry
        {
            SearchText = searchText,
            SearchPath = path,
            IsRegex = isRegex,
            SearchedFiles = 100,
            FoundFiles = foundFiles,
            TotalMatches = foundFiles * 2,
            ElapsedTime = "0.5s",
            SearchedAt = searchedAt ?? DateTime.Now
        };
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test tests/FastDog.Tests/FastDog.Tests.csproj --filter "SearchHistoryServiceTests" --no-build`
Expected: 编译失败（SearchHistoryService 不存在）

- [ ] **Step 3: 实现 SearchHistoryService**

```csharp
using System.Text.Json;
using FastDog.Models;

namespace FastDog.Services;

public class SearchHistoryService
{
    private const int MaxHistoryCount = 50;
    private const string FileName = "search-history.json";

    private readonly string _filePath;
    private List<SearchHistoryEntry> _history = [];
    private SearchHistoryEntry? _lastSession;

    public SearchHistoryService() : this(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastDog"))
    {
    }

    public SearchHistoryService(string directory)
    {
        _filePath = Path.Combine(directory, FileName);
        Load();
    }

    public List<SearchHistoryEntry> LoadHistory() => _history;

    public SearchHistoryEntry? GetLastSession() => _lastSession;

    public void AddEntry(SearchHistoryEntry entry)
    {
        var existing = _history.FirstOrDefault(e => e.DedupKey == entry.DedupKey);
        if (existing is not null)
            _history.Remove(existing);

        _history.Insert(0, entry);

        if (_history.Count > MaxHistoryCount)
            _history = _history.Take(MaxHistoryCount).ToList();

        Save();
    }

    public void DeleteEntry(SearchHistoryEntry entry)
    {
        _history.RemoveAll(e => e.DedupKey == entry.DedupKey);
        Save();
    }

    public void ClearHistory()
    {
        _history.Clear();
        Save();
    }

    public void SaveCurrentSession(SearchHistoryEntry session)
    {
        _lastSession = session;
        Save();
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            _history = [];
            _lastSession = null;
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<HistoryData>(json);
            _history = data?.History ?? [];
            _lastSession = data?.LastSession;
        }
        catch
        {
            _history = [];
            _lastSession = null;
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);

        var data = new HistoryData
        {
            LastSession = _lastSession,
            History = _history
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_filePath, json);
    }

    private class HistoryData
    {
        public SearchHistoryEntry? LastSession { get; set; }
        public List<SearchHistoryEntry> History { get; set; } = [];
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test tests/FastDog.Tests/FastDog.Tests.csproj --filter "SearchHistoryServiceTests" -v normal`
Expected: ALL TESTS PASS

- [ ] **Step 5: Commit**

```bash
git add src/FastDog/Services/SearchHistoryService.cs tests/FastDog.Tests/SearchHistoryServiceTests.cs
git commit -m "feat: add SearchHistoryService with JSON persistence, dedup, and max 50 limit"
```

---

### Task 3: 集成 SearchHistoryService 到 MainViewModel

**Files:**
- Modify: `src/FastDog/ViewModels/MainViewModel.cs`

ViewModel 需要改动：
1. 注入 `SearchHistoryService`
2. 添加历史相关属性和命令
3. 搜索完成时记录历史
4. 提供 `RestoreFromSession` / `RestoreFromHistory` 方法
5. 提供 `SaveSession` 方法（窗口关闭时调用）

- [ ] **Step 1: 修改 MainViewModel — 添加字段和属性**

在 MainViewModel.cs 顶部添加 `SearchHistoryService` 字段，在构造函数中初始化。添加历史相关的 observable 属性。

在 `private readonly FilePreviewService _previewService = new();` 后面加：
```csharp
private readonly SearchHistoryService _historyService = new();
```

在 `// --- 文件预览 ---` 区域之后添加：
```csharp
// --- 搜索历史 ---
public ObservableCollection<SearchHistoryEntry> HistoryEntries { get; } = [];
[ObservableProperty] private SearchHistoryEntry? _selectedHistoryEntry;
[ObservableProperty] private bool _isSessionRestored;
```

在构造函数末尾添加：
```csharp
// 加载历史到 UI
foreach (var entry in _historyService.LoadHistory())
    HistoryEntries.Add(entry);

// 恢复上次会话条件
var lastSession = _historyService.GetLastSession();
if (lastSession is not null)
{
    RestoreFromEntry(lastSession);
    IsSessionRestored = true;
}
```

- [ ] **Step 2: 添加 RestoreFromEntry 和 SaveSession 方法**

在 `// --- 事件处理 ---` 区域之前添加：

```csharp
// --- 历史操作 ---

public void RestoreFromEntry(SearchHistoryEntry entry)
{
    SearchPath = entry.SearchPath;
    SearchText = entry.SearchText;
    IsRegex = entry.IsRegex;
    IsPlainText = !entry.IsRegex;
    CaseSensitive = entry.CaseSensitive;
    WholeWord = entry.WholeWord;
    FileFilter = entry.FileFilter;
    ExcludeDirs = entry.ExcludeDirs;
    DateFilterEnabled = entry.DateFilterEnabled;
    DateFrom = entry.DateFrom;
    DateTo = entry.DateTo;
}

public void SaveSession()
{
    _historyService.SaveCurrentSession(new SearchHistoryEntry
    {
        SearchPath = SearchPath,
        SearchText = SearchText,
        IsRegex = IsRegex,
        CaseSensitive = CaseSensitive,
        WholeWord = WholeWord,
        FileFilter = FileFilter,
        ExcludeDirs = ExcludeDirs,
        DateFilterEnabled = DateFilterEnabled,
        DateFrom = DateFrom,
        DateTo = DateTo
    });
}

[RelayCommand]
private void UseHistory()
{
    if (SelectedHistoryEntry is null) return;
    RestoreFromEntry(SelectedHistoryEntry);
}

[RelayCommand]
private void DeleteHistory()
{
    if (SelectedHistoryEntry is null) return;
    _historyService.DeleteEntry(SelectedHistoryEntry);
    HistoryEntries.Remove(SelectedHistoryEntry);
}

[RelayCommand]
private void ClearHistory()
{
    _historyService.ClearHistory();
    HistoryEntries.Clear();
}
```

- [ ] **Step 3: 在搜索完成时记录历史**

修改 `OnSearchCompleted` 方法，在 `StatusText = ...` 那行之后添加：

```csharp
// 记录搜索历史
var historyEntry = new SearchHistoryEntry
{
    SearchPath = SearchPath,
    SearchText = SearchText,
    IsRegex = IsRegex,
    CaseSensitive = CaseSensitive,
    WholeWord = WholeWord,
    FileFilter = FileFilter,
    ExcludeDirs = ExcludeDirs,
    DateFilterEnabled = DateFilterEnabled,
    DateFrom = DateFrom,
    DateTo = DateTo,
    SearchedFiles = stats.SearchedFiles,
    FoundFiles = stats.FoundFiles,
    TotalMatches = TotalMatches,
    ElapsedTime = stats.Elapsed,
    SearchedAt = DateTime.Now
};

// 去重：先移除旧的同键条目
var existing = HistoryEntries.FirstOrDefault(e => e.DedupKey == historyEntry.DedupKey);
if (existing is not null)
    HistoryEntries.Remove(existing);
HistoryEntries.Insert(0, historyEntry);
```

注意：上面代码已在 `Dispatcher.Invoke` 回调内部，可以直接操作 `HistoryEntries`。

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/FastDog/FastDog.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: 运行所有测试**

Run: `dotnet test tests/FastDog.Tests/FastDog.Tests.csproj -v normal`
Expected: ALL TESTS PASS

- [ ] **Step 6: Commit**

```bash
git add src/FastDog/ViewModels/MainViewModel.cs
git commit -m "feat: integrate SearchHistoryService into MainViewModel with history recording and session restore"
```

---

### Task 4: 修改 MainWindow.xaml — 标签栏、历史列表、会话恢复提示条

**Files:**
- Modify: `src/FastDog/MainWindow.xaml`

这是 UI 改动的核心。需要：
1. 在搜索条件区和结果区之间添加标签栏
2. 把现有结果区包裹在"搜索结果"标签内容中
3. 添加"搜索历史"标签内容（历史 DataGrid）
4. 在搜索条件区上方添加会话恢复提示条

- [ ] **Step 1: 添加会话恢复提示条**

在 `<DockPanel>` 开始标签之后、`<!-- 搜索条件区 -->` 之前插入：

```xml
<!-- 会话恢复提示条 -->
<Border DockPanel.Dock="Top" Background="#E8F5E9" BorderBrush="#C8E6C9"
        BorderThickness="0,0,0,1" Padding="10,6"
        Visibility="{Binding IsSessionRestored, Converter={StaticResource BoolToVis}}">
    <Grid>
        <TextBlock Text="已恢复上次关闭时的搜索状态" Foreground="#2E7D32" FontSize="12"
                   VerticalAlignment="Center"/>
        <Button HorizontalAlignment="Right" Content="关闭" Padding="8,2"
                Command="{Binding DismissSessionBarCommand}"
                Background="Transparent" BorderBrush="#A5D6A7" Foreground="#388E3C"
                FontSize="11" Cursor="Hand"/>
    </Grid>
</Border>
```

需要添加 `BoolToVis` 转换器和 `DismissSessionBar` 命令。在 `Window.Resources` 中添加：

```xml
<Window.Resources>
    <BooleanToVisibilityConverter x:Key="BoolToVis"/>
</Window.Resources>
```

- [ ] **Step 2: 用 TabControl 包裹结果区**

把原来的 `<!-- 中间：结果区 -->` 那个 `<Grid>` 改为 `<TabControl>`，包含两个 `TabItem`。

找到从 `<!-- 中间：结果区 -->` 的 `<Grid>` 到它对应的 `</Grid>`（即整个结果区），替换为：

```xml
<!-- 中间：标签页（搜索结果 / 搜索历史） -->
<TabControl SelectedIndex="0">
    <!-- 搜索结果标签 -->
    <TabItem Header="搜索结果">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="2*"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- 文件列表 (原样保留) -->
            <DataGrid Grid.Row="0"
                      ItemsSource="{Binding SearchResults}"
                      SelectedItem="{Binding SelectedResult}"
                      AutoGenerateColumns="False"
                      IsReadOnly="True"
                      SelectionMode="Single"
                      MouseDoubleClick="DataGrid_MouseDoubleClick">
                <DataGrid.ContextMenu>
                    <ContextMenu>
                        <MenuItem Header="打开文件" Command="{Binding OpenFileCommand}"/>
                        <MenuItem Header="复制路径" Command="{Binding CopyPathCommand}"/>
                        <MenuItem Header="复制文件名" Command="{Binding CopyFileNameCommand}"/>
                    </ContextMenu>
                </DataGrid.ContextMenu>
                <DataGrid.Columns>
                    <DataGridTextColumn Header="文件名" Binding="{Binding FileName}" Width="200"/>
                    <DataGridTextColumn Header="大小" Binding="{Binding FileSize, StringFormat='{}{0:N0}'}" Width="80"/>
                    <DataGridTextColumn Header="匹配数" Binding="{Binding MatchCount}" Width="60"/>
                    <DataGridTextColumn Header="路径" Binding="{Binding FilePath}" Width="*"/>
                    <DataGridTextColumn Header="修改时间" Binding="{Binding LastModified, StringFormat='{}{0:yyyy-MM-dd HH:mm}'}" Width="140"/>
                </DataGrid.Columns>
            </DataGrid>

            <GridSplitter Grid.Row="1" Height="4" HorizontalAlignment="Stretch"/>

            <!-- 行内容预览（左右拆分，原样保留） -->
            <Grid Grid.Row="2">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="35*" MinWidth="200"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="65*" MinWidth="300"/>
                </Grid.ColumnDefinitions>

                <!-- 左侧：匹配行列表 -->
                <Grid Grid.Column="0">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <Border Grid.Row="0" Background="#F0F0F0" Padding="8,4"
                            BorderBrush="#DDD" BorderThickness="0,0,0,1">
                        <TextBlock Text="匹配行" FontWeight="SemiBold" FontSize="12" Foreground="#555"/>
                    </Border>
                    <ListBox Grid.Row="1"
                             ItemsSource="{Binding MatchLines}"
                             SelectedItem="{Binding SelectedMatchLine}"
                             MouseDoubleClick="MatchList_MouseDoubleClick"
                             FontFamily="Consolas" FontSize="12"
                             BorderThickness="0">
                        <ListBox.ItemContainerStyle>
                            <Style TargetType="ListBoxItem">
                                <Setter Property="Padding" Value="2,1"/>
                            </Style>
                        </ListBox.ItemContainerStyle>
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="50"/>
                                        <ColumnDefinition Width="*"/>
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Grid.Column="0" Text="{Binding LineNumber}"
                                               Foreground="Gray"
                                               VerticalAlignment="Center"/>
                                    <TextBlock Grid.Column="1" Text="{Binding LineText}"
                                               VerticalAlignment="Center"
                                               TextTrimming="CharacterEllipsis"/>
                                </Grid>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                </Grid>

                <GridSplitter Grid.Column="1" Width="4" HorizontalAlignment="Stretch"
                              VerticalAlignment="Stretch"/>

                <!-- 右侧：文件预览 -->
                <Grid Grid.Column="2">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <Border Grid.Row="0" Background="#F0F0F0" Padding="8,4"
                            BorderBrush="#DDD" BorderThickness="0,0,0,1">
                        <TextBlock Text="文件预览" FontWeight="SemiBold" FontSize="12" Foreground="#555"/>
                    </Border>
                    <Grid Grid.Row="1">
                        <avalonedit:TextEditor Name="PreviewEditor"
                            IsReadOnly="True"
                            ShowLineNumbers="True"
                            WordWrap="False"
                            FontFamily="Consolas"
                            FontSize="12"/>
                        <TextBlock Text="二进制文件，无法预览"
                                   HorizontalAlignment="Center" VerticalAlignment="Center"
                                   Foreground="Gray" FontSize="14">
                            <TextBlock.Visibility>
                                <Binding Path="IsBinaryFile">
                                    <Binding.Converter>
                                        <BooleanToVisibilityConverter/>
                                    </Binding.Converter>
                                </Binding>
                            </TextBlock.Visibility>
                        </TextBlock>
                        <TextBlock Text="{Binding FileErrorMessage}"
                                   HorizontalAlignment="Center" VerticalAlignment="Center"
                                   Foreground="Gray" FontSize="14">
                            <TextBlock.Visibility>
                                <Binding Path="IsFileError">
                                    <Binding.Converter>
                                        <BooleanToVisibilityConverter/>
                                    </Binding.Converter>
                                </Binding>
                            </TextBlock.Visibility>
                        </TextBlock>
                        <TextBlock Text="文件过大，仅显示部分内容"
                                   HorizontalAlignment="Right" VerticalAlignment="Top"
                                   Foreground="Orange" FontSize="11" Margin="4"
                                   Panel.ZIndex="1">
                            <TextBlock.Visibility>
                                <Binding Path="IsFileTruncated">
                                    <Binding.Converter>
                                        <BooleanToVisibilityConverter/>
                                    </Binding.Converter>
                                </Binding>
                            </TextBlock.Visibility>
                        </TextBlock>
                    </Grid>
                </Grid>
            </Grid>
        </Grid>
    </TabItem>

    <!-- 搜索历史标签 -->
    <TabItem Header="搜索历史">
        <DataGrid ItemsSource="{Binding HistoryEntries}"
                  SelectedItem="{Binding SelectedHistoryEntry}"
                  AutoGenerateColumns="False"
                  IsReadOnly="True"
                  SelectionMode="Single"
                  MouseDoubleClick="HistoryDataGrid_MouseDoubleClick">
            <DataGrid.ContextMenu>
                <ContextMenu>
                    <MenuItem Header="使用此条件" Command="{Binding UseHistoryCommand}"/>
                    <MenuItem Header="使用此条件并搜索" Click="MenuItem_SearchWithHistory"/>
                    <Separator/>
                    <MenuItem Header="删除" Command="{Binding DeleteHistoryCommand}"/>
                    <MenuItem Header="清空所有历史" Command="{Binding ClearHistoryCommand}"/>
                </ContextMenu>
            </DataGrid.ContextMenu>
            <DataGrid.Columns>
                <DataGridTextColumn Header="搜索内容" Binding="{Binding SearchText}" Width="*"
                                    FontFamily="Consolas"/>
                <DataGridTextColumn Header="路径" Binding="{Binding SearchPath}" Width="250"/>
                <DataGridTextColumn Header="选项" Binding="{Binding OptionsSummary}" Width="160"/>
                <DataGridTextColumn Header="结果" Binding="{Binding ResultSummary}" Width="200"/>
                <DataGridTextColumn Header="时间" Binding="{Binding SearchedAt, StringFormat='{}{0:yyyy-MM-dd HH:mm}'}" Width="130"/>
            </DataGrid.Columns>
        </DataGrid>
    </TabItem>
</TabControl>
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build src/FastDog/FastDog.csproj`
Expected: BUILD SUCCEEDED（可能有缺少的事件处理程序警告，下一个 Task 处理）

- [ ] **Step 4: Commit**

```bash
git add src/FastDog/MainWindow.xaml
git commit -m "feat: add tab bar with search results and search history tabs, session restore banner"
```

---

### Task 5: 修改 MainWindow.xaml.cs — 事件处理和窗口生命周期

**Files:**
- Modify: `src/FastDog/MainWindow.xaml.cs`

需要添加：
1. `HistoryDataGrid_MouseDoubleClick` 事件处理
2. `MenuItem_SearchWithHistory` 事件处理
3. 窗口关闭时调用 `SaveSession()`
4. `DismissSessionBarCommand` 的处理

- [ ] **Step 1: 添加历史相关的事件处理和窗口关闭钩子**

在 MainWindow.xaml.cs 中添加以下方法：

```csharp
protected override void OnClosed(EventArgs e)
{
    if (DataContext is MainViewModel vm)
        vm.SaveSession();
    base.OnClosed(e);
}

private void HistoryDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    if (DataContext is MainViewModel vm)
        vm.UseHistoryCommand.Execute(null);
}

private void MenuItem_SearchWithHistory(object sender, RoutedEventArgs e)
{
    if (DataContext is MainViewModel vm)
    {
        vm.UseHistoryCommand.Execute(null);
        vm.SearchCommand.Execute(null);
    }
}
```

- [ ] **Step 2: 在 ViewModel 中添加 DismissSessionBar 命令**

在 MainViewModel.cs 的历史命令区域添加：

```csharp
[RelayCommand]
private void DismissSessionBar()
{
    IsSessionRestored = false;
}
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build src/FastDog/FastDog.csproj`
Expected: BUILD SUCCEEDED, 0 warnings

- [ ] **Step 4: 运行所有测试**

Run: `dotnet test tests/FastDog.Tests/FastDog.Tests.csproj -v normal`
Expected: ALL TESTS PASS

- [ ] **Step 5: Commit**

```bash
git add src/FastDog/MainWindow.xaml.cs src/FastDog/ViewModels/MainViewModel.cs
git commit -m "feat: add history event handlers, window close save, and session bar dismiss"
```

---

### Task 6: 手动集成测试

**Files:** 无新文件

- [ ] **Step 1: 运行程序**

Run: `dotnet run --project src/FastDog`

- [ ] **Step 2: 验证搜索历史记录**

1. 在搜索路径输入一个有效目录（如 `E:\demo\ai\my\fastdog`）
2. 搜索关键词 `SearchService`，使用正则模式
3. 等待搜索完成
4. 点击"搜索历史"标签页，确认有记录
5. 再搜索 `RipgrepBridge`
6. 确认历史标签页有 2 条记录，新的排在最前面

- [ ] **Step 3: 验证会话恢复**

1. 关闭程序
2. 重新运行 `dotnet run --project src/FastDog`
3. 确认：搜索条件已恢复（路径、关键词、选项），绿色提示条显示"已恢复上次关闭时的搜索状态"
4. 确认：没有自动执行搜索（结果区为空）
5. 点击"关闭"按钮，提示条消失

- [ ] **Step 4: 验证历史操作**

1. 切换到"搜索历史"标签
2. 双击一条历史 → 搜索条件填充到输入框
3. 右键 → "删除" → 该条消失
4. 右键 → "使用此条件并搜索" → 条件填充并自动搜索
5. 右键 → "清空所有历史" → 所有记录清除

- [ ] **Step 5: 验证去重**

1. 搜索 `test` → 完成
2. 再搜索 `test`（相同条件） → 完成
3. 查看历史标签，应只有 1 条 `test` 记录（统计信息更新为最新的）

- [ ] **Step 6: Final commit if any fixes needed**

```bash
git add -A
git commit -m "fix: address integration test findings for search history"
```

---

### Task 7: 更新 CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: 在"已实现功能"列表中添加搜索历史相关功能**

在 `## 已实现功能` 部分，在"状态栏"条目之后添加：

```markdown
- 搜索历史：记录最近 50 条搜索（去重合并），可查看/恢复/删除/清空
- 会话恢复：下次启动自动恢复上次关闭时的搜索条件（不自动搜索）
- 标签页：搜索结果 / 搜索历史 切换
- 历史右键菜单：使用条件、使用条件并搜索、删除、清空
```

在 `## 项目结构` 的 `Services/` 行中追加 `SearchHistoryService (搜索历史JSON持久化)`。

在 `## 架构` 部分追加：

```markdown
- **SearchHistoryService**: 搜索历史持久化（JSON，%APPDATA%\FastDog\），去重合并、50条上限、会话保存/恢复
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md with search history and session restore features"
```
