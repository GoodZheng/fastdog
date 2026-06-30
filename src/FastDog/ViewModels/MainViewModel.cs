using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FastDog.Models;
using FastDog.Services;

namespace FastDog.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SearchService _searchService = new();
    private readonly FilePreviewService _previewService = new();
    private readonly SearchHistoryService _historyService = new();
    private readonly InputHistoryService _inputHistoryService = new();

    // --- 搜索条件 ---
    [ObservableProperty] private string _searchPath = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isRegex = true;
    [ObservableProperty] private bool _isPlainText = false;
    [ObservableProperty] private bool _caseSensitive = false;
    [ObservableProperty] private bool _wholeWord = false;
    [ObservableProperty] private string _fileFilter = string.Empty;
    [ObservableProperty] private string _excludeDirs = "bin;obj";
    [ObservableProperty] private bool _dateFilterEnabled = false;
    [ObservableProperty] private DateTime? _dateFrom;
    [ObservableProperty] private DateTime? _dateTo;

    // --- 搜索结果 ---
    public ObservableCollection<SearchResult> SearchResults { get; } = [];
    public ObservableCollection<MatchLine> MatchLines { get; } = [];

    // --- 输入历史自动补全建议（搜索路径 / 搜索内容各自独立） ---
    public ObservableCollection<string> SearchPathSuggestions { get; } = [];
    public ObservableCollection<string> SearchTextSuggestions { get; } = [];

    [ObservableProperty] private SearchResult? _selectedResult;
    [ObservableProperty] private MatchLine? _selectedMatchLine;

    // --- 状态栏 ---
    [ObservableProperty] private bool _isSearching = false;
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private int _totalFiles = 0;
    [ObservableProperty] private int _totalMatches = 0;
    [ObservableProperty] private string _elapsedTime = string.Empty;
    [ObservableProperty] private long _searchedFiles = 0;

    // --- 文件预览 ---
    [ObservableProperty] private string _fileContent = string.Empty;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private bool _isBinaryFile = false;
    [ObservableProperty] private bool _isFileError = false;
    [ObservableProperty] private string _fileErrorMessage = string.Empty;
    [ObservableProperty] private bool _isFileTruncated = false;
    [ObservableProperty] private bool _previewWordWrap = false;

    // --- 搜索历史 ---
    public ObservableCollection<SearchHistoryEntry> HistoryEntries { get; } = [];
    [ObservableProperty] private SearchHistoryEntry? _selectedHistoryEntry;
    [ObservableProperty] private bool _isSessionRestored;
    [ObservableProperty] private bool _isHistoryTab;

    public string FileFilterDisplay => string.IsNullOrEmpty(FileFilter) ? "文件: *" : $"文件: {FileFilter}";
    public string ExcludeDirsDisplay => string.IsNullOrEmpty(ExcludeDirs) ? "排除: (无)" : $"排除: {ExcludeDirs}";

    public MainViewModel()
    {
        _searchService.ResultReceived += OnResultReceived;
        _searchService.SearchCompleted += OnSearchCompleted;
        _searchService.SearchCancelled += OnSearchCancelled;

        // 加载历史到 UI
        foreach (var entry in _historyService.LoadHistory())
            HistoryEntries.Add(entry);

        // 加载输入历史到补全建议
        foreach (var path in _inputHistoryService.LoadSearchPaths())
            SearchPathSuggestions.Add(path);
        foreach (var text in _inputHistoryService.LoadSearchTexts())
            SearchTextSuggestions.Add(text);

        // 恢复上次会话条件
        var lastSession = _historyService.GetLastSession();
        if (lastSession is not null)
        {
            RestoreFromEntry(lastSession);
            IsSessionRestored = true;
            StatusText = "已恢复上次关闭时的搜索状态";
        }
    }

    public event Action<int>? ScrollToLineRequested;

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

        // 默认选中第一个匹配行。必须在 Dispatcher 上延迟执行：此处正处于
        // MatchLines.Clear()+Add 的同一同步流程中，同步赋值会被 WPF 的
        // Selector 选择状态绑定短路，导致 ListBox 视觉上不生效。
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (MatchLines.Count > 0)
                SelectedMatchLine = MatchLines[0];
        }), System.Windows.Threading.DispatcherPriority.Background);

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

    partial void OnSelectedMatchLineChanged(MatchLine? value)
    {
        if (value is not null)
            ScrollToLineRequested?.Invoke(value.LineNumber);
    }

    partial void OnSearchPathChanged(string value) => ClearSessionRestore();
    partial void OnSearchTextChanged(string value) => ClearSessionRestore();
    partial void OnIsRegexChanged(bool value) => ClearSessionRestore();
    partial void OnCaseSensitiveChanged(bool value) => ClearSessionRestore();
    partial void OnWholeWordChanged(bool value) => ClearSessionRestore();
    partial void OnFileFilterChanged(string value)
    {
        ClearSessionRestore();
        OnPropertyChanged(nameof(FileFilterDisplay));
    }

    partial void OnExcludeDirsChanged(string value)
    {
        OnPropertyChanged(nameof(ExcludeDirsDisplay));
    }

    // 日期范围交叉校验：始终保证 DateFrom <= DateTo，避免出现倒置区间。
    // 任一边改变后若越过另一边，就把另一边拉齐到当前值。仅在确实越界时才
    // 写回，因此由它触发的对侧 OnXxxChanged 内的判断不会再次成立，不会
    // 形成无限递归。
    partial void OnDateFromChanged(DateTime? value)
    {
        if (value is not null && DateTo is not null && value > DateTo)
            DateTo = value;
    }

    partial void OnDateToChanged(DateTime? value)
    {
        if (value is not null && DateFrom is not null && value < DateFrom)
            DateFrom = value;
    }

    private void ClearSessionRestore()
    {
        if (IsSessionRestored)
        {
            IsSessionRestored = false;
            StatusText = "就绪";
        }
    }

    /// <summary>
    /// 将一次输入记入补全建议表：空值跳过，去重后倒序置顶，并同步落盘。
    /// </summary>
    private static void AddInputHistory(ObservableCollection<string> suggestions, string value, Action<string> persist)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        value = value.Trim();
        if (suggestions.Contains(value)) suggestions.Remove(value);
        suggestions.Insert(0, value);
        persist(value);
    }

    // --- 命令 ---

    [RelayCommand]
    private async Task SearchAsync()
    {
        // 去除搜索词首尾空白，避免误传给 ripgrep 影响匹配结果
        // （复制粘贴常带入前后空格）
        SearchText = SearchText.Trim();

        if (string.IsNullOrWhiteSpace(SearchPath) || string.IsNullOrWhiteSpace(SearchText))
            return;

        if (!Directory.Exists(SearchPath))
        {
            System.Windows.MessageBox.Show("搜索路径不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 记录输入历史（路径与内容各自去重，倒序置顶，供下次自动补全）
        AddInputHistory(SearchPathSuggestions, SearchPath, _inputHistoryService.AddSearchPath);
        AddInputHistory(SearchTextSuggestions, SearchText, _inputHistoryService.AddSearchText);

        SearchResults.Clear();
        MatchLines.Clear();
        TotalFiles = 0;
        TotalMatches = 0;
        SearchedFiles = 0;
        IsSearching = true;
        StatusText = "搜索中...";

        var query = new SearchQuery
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
        };

        try
        {
            await Task.Run(async () => await _searchService.SearchAsync(query, SearchPath));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"搜索出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            IsSearching = false;
            StatusText = "搜索失败";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _searchService.Cancel();
    }

    [RelayCommand]
    private void Browse()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = SearchPath
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            SearchPath = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private void OpenFile()
    {
        if (SelectedResult is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(SelectedResult.FilePath) { UseShellExecute = true });
        }
        catch { }
    }

    [RelayCommand]
    private void OpenFileAtLine()
    {
        if (SelectedResult is null || SelectedMatchLine is null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "code",
                Arguments = $"--goto \"{SelectedResult.FilePath}:{SelectedMatchLine.LineNumber}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo(SelectedResult.FilePath) { UseShellExecute = true });
            }
            catch { }
        }
    }

    [RelayCommand]
    private void CopyPath()
    {
        if (SelectedResult is null) return;
        System.Windows.Clipboard.SetText(SelectedResult.FilePath);
        StatusText = "已复制路径";
    }

    [RelayCommand]
    private void CopyFileName()
    {
        if (SelectedResult is null) return;
        System.Windows.Clipboard.SetText(SelectedResult.FileName);
        StatusText = "已复制文件名";
    }

    [RelayCommand]
    private void OpenInExplorer()
    {
        if (SelectedResult is null) return;
        try
        {
            var dir = System.IO.Path.GetDirectoryName(SelectedResult.FilePath);
            if (dir is not null)
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch { }
    }

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

    [RelayCommand]
    private void UseHistoryEntry(SearchHistoryEntry entry)
    {
        RestoreFromEntry(entry);
    }

    [RelayCommand]
    private void SearchWithHistory(SearchHistoryEntry entry)
    {
        RestoreFromEntry(entry);
        _ = SearchAsync();
    }

    // --- 事件处理 ---

    private void OnResultReceived(SearchResult result)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            SearchResults.Add(result);
            TotalFiles = SearchResults.Count;
            TotalMatches = SearchResults.Sum(r => r.MatchCount);
        });
    }

    private void OnSearchCompleted(SearchStats stats)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsSearching = false;
            SearchedFiles = stats.SearchedFiles;
            ElapsedTime = stats.Elapsed;
            StatusText = $"已搜索 {stats.SearchedFiles} 个文件，找到 {stats.FoundFiles} 个文件，共 {TotalMatches} 处匹配";

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
                FoundFiles = (int)stats.FoundFiles,
                TotalMatches = TotalMatches,
                ElapsedTime = stats.Elapsed,
                SearchedAt = DateTime.Now
            };
            var existing = HistoryEntries.FirstOrDefault(e => e.DedupKey == historyEntry.DedupKey);
            if (existing is not null)
                HistoryEntries.Remove(existing);
            HistoryEntries.Insert(0, historyEntry);
        });
    }

    private void OnSearchCancelled()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsSearching = false;
            StatusText = $"搜索已取消 | 找到 {TotalFiles} 个文件，共 {TotalMatches} 处匹配";
        });
    }
}
