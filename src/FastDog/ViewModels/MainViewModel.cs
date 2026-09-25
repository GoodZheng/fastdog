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
    private readonly FileSearchService _fileSearchService = new();
    private readonly FilePreviewService _previewService = new();
    private readonly SearchHistoryService _historyService = new();
    private readonly InputHistoryService _inputHistoryService = new();
    private readonly FileExtensionConfigService _extensionConfigService = new();

    // --- 文件模式输入即搜 ---
    // 逐键触发合并为停顿 200ms 后的一次搜索；代际号使被顶掉的旧搜索事件全部作废；
    // 签名记录上次已执行条件，防止 Trim 回写/选项来回切导致的冗余重跑
    private readonly Helpers.DebounceTimer _fileSearchDebounce = new(TimeSpan.FromMilliseconds(200));
    private int _fileSearchGeneration;
    private string? _lastFileQuerySignature;
    private bool _allowLiveFileSearch;
    private Action? _detachFileSearchHandlers;
    /// <summary>本轮结果是否已由批量事件按显示顺序排好（免二次 SortFileResults）。</summary>
    private bool _fileResultsPreSorted;

    // --- 搜索条件 ---
    [ObservableProperty] private string _searchPath = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isRegex = true;
    [ObservableProperty] private bool _isPlainText = false;
    [ObservableProperty] private bool _caseSensitive = false;
    [ObservableProperty] private bool _wholeWord = false;
    [ObservableProperty] private string _fileFilter = string.Empty;
    [ObservableProperty] private string _excludeDirs = "bin;obj";
    [ObservableProperty] private string _hiddenWhitelist = string.Empty;
    [ObservableProperty] private bool _dateFilterEnabled = false;
    [ObservableProperty] private DateTime? _dateFrom;
    [ObservableProperty] private DateTime? _dateTo;

    // --- 文件搜索模式条件 ---
    /// <summary>文件/文件夹名称匹配串：空=全部；含 * 或 ? 按通配符，否则按子串。</summary>
    [ObservableProperty] private string _fileSearchText = string.Empty;
    [ObservableProperty] private bool _includeFiles = true;
    [ObservableProperty] private bool _includeFolders = true;
    [ObservableProperty] private bool _fileCaseSensitive = false;
    /// <summary>后缀下拉「添加」输入框内容。</summary>
    [ObservableProperty] private string _extensionInput = string.Empty;

    // --- 模式切换：false=文本搜索（全文内容），true=文件搜索（文件名/后缀） ---
    [ObservableProperty] private bool _isFileSearchMode;

    /// <summary>文本模式。与 IsFileSearchMode 反相，供「文本搜索」RadioButton 双向绑定。</summary>
    public bool IsTextSearchMode
    {
        get => !IsFileSearchMode;
        set
        {
            // 仅响应"勾选文本模式"；WPF 组互斥导致的取消勾选（value=false）忽略
            if (value && IsFileSearchMode)
                IsFileSearchMode = false;
        }
    }

    // --- 搜索结果 ---
    public ObservableCollection<SearchResult> SearchResults { get; } = [];
    public ObservableCollection<MatchLine> MatchLines { get; } = [];

    // --- 输入历史自动补全建议（搜索路径 / 搜索内容各自独立） ---
    public ObservableCollection<string> SearchPathSuggestions { get; } = [];
    public ObservableCollection<string> SearchTextSuggestions { get; } = [];

    [ObservableProperty] private SearchResult? _selectedResult;
    [ObservableProperty] private MatchLine? _selectedMatchLine;

    // --- 文件多选 ---
    // DataGrid.SelectedItems 不是 DependencyProperty，无法直接绑定，由 MainWindow.xaml.cs
    // 在 SelectionChanged 中同步过来。OpenInExplorer 仅在单选时可用。
    public ObservableCollection<SearchResult> SelectedResults { get; } = [];

    // --- 文件搜索结果 ---
    public ObservableCollection<FileSearchResult> FileResults { get; } = [];
    // 文件模式多选桥接，语义同 SelectedResults
    public ObservableCollection<FileSearchResult> SelectedFileResults { get; } = [];
    [ObservableProperty] private FileSearchResult? _selectedFileResult;
    [ObservableProperty] private int _fileResultCount;
    [ObservableProperty] private int _folderResultCount;

    // 选中文件夹时的信息面板（替代文件预览区）
    [ObservableProperty] private bool _isFolderSelected;
    [ObservableProperty] private string _folderInfoPath = string.Empty;
    [ObservableProperty] private string _folderInfoModified = string.Empty;

    // --- 后缀过滤下拉 ---
    public ObservableCollection<FileExtensionItem> ExtensionItems { get; } = [];
    public string ExtensionFilterDisplay { get; private set; } = "后缀: 全部";
    // 批量变更勾选（清空/历史恢复）时抑制逐项持久化，结束后统一保存一次
    private bool _suppressExtensionPersist;

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
    public string HiddenWhitelistDisplay => string.IsNullOrEmpty(HiddenWhitelist) ? "含隐藏目录" : $"含: {HiddenWhitelist}";

    public MainViewModel()
    {
        _searchService.ResultReceived += OnResultReceived;
        _searchService.SearchCompleted += OnSearchCompleted;
        _searchService.SearchCancelled += OnSearchCancelled;
        // 文件搜索事件按次挂接（AttachFileSearchHandlers）：处理器需捕获本次代际，
        // 输入即搜下旧搜索的迟到事件才能被准确丢弃

        // 加载历史到 UI
        foreach (var entry in _historyService.LoadHistory())
            HistoryEntries.Add(entry);

        // 加载输入历史到补全建议
        foreach (var path in _inputHistoryService.LoadSearchPaths())
            SearchPathSuggestions.Add(path);
        foreach (var text in _inputHistoryService.LoadSearchTexts())
            SearchTextSuggestions.Add(text);

        // 多选集合变化时刷新 OpenInExplorer 的可用状态（多选时禁用）
        SelectedResults.CollectionChanged += (_, _) => OpenInExplorerCommand.NotifyCanExecuteChanged();
        SelectedFileResults.CollectionChanged += (_, _) => OpenInExplorerCommand.NotifyCanExecuteChanged();

        // 加载后缀过滤列表（内置常用后缀 + 上次会话的增删与勾选状态）
        var extConfig = _extensionConfigService.Load();
        foreach (var name in extConfig.Items)
            AddExtensionItem(new FileExtensionItem { Name = name, IsSelected = extConfig.Selected.Contains(name) });
        UpdateExtensionFilterDisplay();

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

        // 焦点项变化时刷新“在资源管理器中打开”的可用状态（清空选中时应灰显）
        OpenInExplorerCommand.NotifyCanExecuteChanged();

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

    /// <summary>
    /// 文件模式选中行联动：文件 → 复用文本模式的预览管线（FileContent/FilePath
    /// 由 MainWindow 监听渲染）；文件夹 → 显示信息面板，清空预览。
    /// </summary>
    partial void OnSelectedFileResultChanged(FileSearchResult? value)
    {
        IsFolderSelected = false;
        FolderInfoPath = string.Empty;
        FolderInfoModified = string.Empty;
        FileContent = string.Empty;
        FilePath = string.Empty;
        IsBinaryFile = false;
        IsFileError = false;
        FileErrorMessage = string.Empty;
        IsFileTruncated = false;

        OpenInExplorerCommand.NotifyCanExecuteChanged();

        if (value is null) return;

        if (value.IsFolder)
        {
            IsFolderSelected = true;
            FolderInfoPath = value.FilePath;
            FolderInfoModified = value.LastModified.ToString("yyyy-MM-dd HH:mm");
            return;
        }

        if (_previewService.IsBinaryFile(value.FilePath))
        {
            IsBinaryFile = true;
            return;
        }

        var content = _previewService.LoadFileContent(value.FilePath, out var truncated);
        if (content is null)
        {
            IsFileError = true;
            FileErrorMessage = File.Exists(value.FilePath) ? "无法读取文件" : "文件未找到";
            return;
        }

        IsFileTruncated = truncated;
        FilePath = value.FilePath;
        FileContent = content;
    }

    partial void OnSearchPathChanged(string value)
    {
        ClearSessionRestore();
        ScheduleLiveFileSearch();
    }
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
        // 排除目录影响 rg 遍历范围（缓存 key），变化后自动重搜并重建缓存
        ScheduleLiveFileSearch();
    }

    partial void OnHiddenWhitelistChanged(string value)
    {
        ClearSessionRestore();
        OnPropertyChanged(nameof(HiddenWhitelistDisplay));
        ScheduleLiveFileSearch();
    }

    partial void OnIsFileSearchModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsTextSearchMode));
        SearchCommand.NotifyCanExecuteChanged();
        ClearSessionRestore();
        // 模式切换后旧防抖/旧签名无意义；进行中的文件搜索一并停掉
        _fileSearchDebounce.Cancel();
        _lastFileQuerySignature = null;
        _fileSearchService.Cancel();
    }

    partial void OnFileSearchTextChanged(string value)
    {
        ClearSessionRestore();
        ScheduleLiveFileSearch();
    }

    partial void OnFileCaseSensitiveChanged(bool value)
    {
        ClearSessionRestore();
        ScheduleLiveFileSearch();
    }

    partial void OnIncludeFilesChanged(bool value)
    {
        ClearSessionRestore();
        SearchCommand.NotifyCanExecuteChanged();
        ScheduleLiveFileSearch();
    }

    partial void OnIncludeFoldersChanged(bool value)
    {
        ClearSessionRestore();
        SearchCommand.NotifyCanExecuteChanged();
        ScheduleLiveFileSearch();
    }

    // 日期范围交叉校验：始终保证 DateFrom <= DateTo，避免出现倒置区间。
    // 任一边改变后若越过另一边，就把另一边拉齐到当前值。仅在确实越界时才
    // 写回，因此由它触发的对侧 OnXxxChanged 内的判断不会再次成立，不会
    // 形成无限递归。
    partial void OnDateFilterEnabledChanged(bool value)
    {
        ClearSessionRestore();
        ScheduleLiveFileSearch();
    }

    partial void OnDateFromChanged(DateTime? value)
    {
        if (value is not null && DateTo is not null && value > DateTo)
            DateTo = value;
        ScheduleLiveFileSearch();
    }

    partial void OnDateToChanged(DateTime? value)
    {
        if (value is not null && DateFrom is not null && value < DateFrom)
            DateFrom = value;
        ScheduleLiveFileSearch();
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

    /// <summary>
    /// 设置剪贴板文本，对剪贴板锁争抢鲁棒。先走原生 Win32 API（不经 OLE），
    /// 失败再回退到 WPF Clipboard.SetText。仍失败则静默返回 false，
    /// 避免 ExternalException 未捕获导致整个进程崩溃。
    /// </summary>
    private static bool TrySetClipboardText(string text)
        => Helpers.ClipboardHelper.TrySetText(text, System.Windows.Application.Current?.MainWindow);

    // --- 命令 ---

    // 文件模式下「文件」「文件夹」全关时没有搜索对象，按钮置灰
    [RelayCommand(CanExecute = nameof(CanExecuteSearch))]
    private async Task SearchAsync()
    {
        if (IsFileSearchMode)
        {
            await SearchFilesCoreAsync(isAutoSearch: false);
            return;
        }

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
            HiddenWhitelist = HiddenWhitelist,
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

    private bool CanExecuteSearch() => !IsFileSearchMode || IncludeFiles || IncludeFolders;

    // --- 文件模式输入即搜 ---

    /// <summary>窗口渲染完成、会话恢复写入完毕后由 MainWindow 调用，此后输入才触发自动搜索。</summary>
    public void EnableLiveFileSearch() => _allowLiveFileSearch = true;

    /// <summary>文件模式相关输入/选项变化后调用：防抖 200ms 合并连击，静默期结束后自动搜索一次。</summary>
    private void ScheduleLiveFileSearch()
    {
        if (!_allowLiveFileSearch || !IsFileSearchMode) return;
        _fileSearchDebounce.Ping(RunLiveFileSearch);
    }

    private void RunLiveFileSearch()
    {
        if (!_allowLiveFileSearch || !IsFileSearchMode) return;
        if (!IncludeFiles && !IncludeFolders) return; // 没有搜索对象（按钮此时也置灰）
        _ = SearchFilesCoreAsync(isAutoSearch: true);
    }

    /// <summary>文件搜索模式：rg --files 枚举 + 文件名/后缀/日期过滤。
    /// isAutoSearch=true（输入即搜）时：不弹错误框、签名相同则跳过、命中缓存不强制重建；
    /// false（手动按钮/回车）时：强制重建枚举缓存——缓存建立后新建/删除的文件由此可见。</summary>
    private async Task SearchFilesCoreAsync(bool isAutoSearch)
    {
        var namePattern = FileSearchText.Trim();

        if (string.IsNullOrWhiteSpace(SearchPath))
            return;

        if (!Directory.Exists(SearchPath))
        {
            if (isAutoSearch)
            {
                // 输入即搜路径不弹框（逐键输入路径时必然经过不存在的中间态）
                StatusText = "搜索路径不存在";
                return;
            }
            System.Windows.MessageBox.Show("搜索路径不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var query = new FileSearchQuery
        {
            SearchPath = SearchPath,
            NamePattern = namePattern,
            CaseSensitive = FileCaseSensitive,
            IncludeFiles = IncludeFiles,
            IncludeFolders = IncludeFolders,
            Extensions = ExtensionItems.Where(i => i.IsSelected).Select(i => i.Name).ToList(),
            ExcludeDirs = ExcludeDirs,
            HiddenWhitelist = HiddenWhitelist,
            DateFilterEnabled = DateFilterEnabled,
            DateFrom = DateFrom,
            DateTo = DateTo
        };

        // 签名去重：条件与上次已执行完全相同的自动搜索直接跳过；
        // 手动搜索不跳（语义=强制刷新缓存）
        var signature = query.BuildSignature();
        if (isAutoSearch && signature == _lastFileQuerySignature)
            return;

        // 文件模式没有"搜索内容"输入，仅记录路径输入历史
        // （自动搜索不在此记，防逐键刷屏；改到完成且代际最新时补记）
        if (!isAutoSearch)
            AddInputHistory(SearchPathSuggestions, SearchPath, _inputHistoryService.AddSearchPath);

        // 代际守卫：本轮搜索的流式事件以 gen 验身，被顶掉的旧搜索事件全部作废
        var gen = ++_fileSearchGeneration;
        _lastFileQuerySignature = signature;
        _fileResultsPreSorted = false;
        AttachFileSearchHandlers(gen, isAutoSearch, query);

        FileResults.Clear();
        FileResultCount = 0;
        FolderResultCount = 0;
        IsSearching = true;
        StatusText = "搜索中...";

        try
        {
            await Task.Run(async () => await _fileSearchService.SearchAsync(query, forceRefresh: !isAutoSearch));
        }
        catch (Exception ex)
        {
            IsSearching = false;
            if (isAutoSearch)
                StatusText = $"搜索失败: {ex.Message}";
            else
            {
                System.Windows.MessageBox.Show($"搜索出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "搜索失败";
            }
        }
    }

    /// <summary>
    /// 为一次文件搜索挂接事件处理器（捕获本次代际与查询）。挂接前先摘除上一轮。
    /// 代际不匹配的迟到事件直接丢弃：防旧搜索污染新结果、防中间查询写入历史。
    /// 代际校验放在 Dispatcher.Invoke 内层，与 UI 线程上的代际递增/清空串行化，
    /// 关闭"校验通过后、执行前被新一轮顶掉"的窗口。
    /// 正常收尾（完成/取消）自解绑；被顶掉时由新一轮挂接统一摘除旧处理器。
    /// </summary>
    private void AttachFileSearchHandlers(int gen, bool isAutoSearch, FileSearchQuery query)
    {
        DetachFileSearchHandlers();

        void AddToResults(FileSearchResult r)
        {
            FileResults.Add(r);
            if (r.IsFolder) FolderResultCount++;
            else FileResultCount++;
        }

        void OnResult(FileSearchResult result)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (gen != _fileSearchGeneration) return;
                AddToResults(result);
            });
        }

        void OnBatch(FileSearchBatch batch)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (gen != _fileSearchGeneration) return;
                foreach (var r in batch.Items)
                    AddToResults(r);
                _fileResultsPreSorted = true;
            });
        }

        void OnCompleted(FileSearchStats stats)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (gen != _fileSearchGeneration) return;
                DetachFileSearchHandlers();

                IsSearching = false;
                ElapsedTime = stats.Elapsed;
                if (_fileResultsPreSorted)
                    _fileResultsPreSorted = false; // 批量已按显示顺序排好，免二次重排
                else
                    SortFileResults();
                StatusText = $"搜索完成：{stats.FileCount} 个文件 / {stats.FolderCount} 个文件夹";

                RecordFileSearchHistory(query, stats, isAutoSearch);
            });
        }

        void OnCancelled()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (gen != _fileSearchGeneration) return;
                DetachFileSearchHandlers();

                IsSearching = false;
                SortFileResults();
                StatusText = $"搜索已取消 | 找到 {FileResultCount} 个文件 / {FolderResultCount} 个文件夹";
            });
        }

        _fileSearchService.ResultReceived += OnResult;
        _fileSearchService.ResultsReady += OnBatch;
        _fileSearchService.SearchCompleted += OnCompleted;
        _fileSearchService.SearchCancelled += OnCancelled;
        _detachFileSearchHandlers = () =>
        {
            _fileSearchService.ResultReceived -= OnResult;
            _fileSearchService.ResultsReady -= OnBatch;
            _fileSearchService.SearchCompleted -= OnCompleted;
            _fileSearchService.SearchCancelled -= OnCancelled;
        };
    }

    private void DetachFileSearchHandlers()
    {
        if (_detachFileSearchHandlers is null) return;
        _detachFileSearchHandlers();
        _detachFileSearchHandlers = null;
    }

    /// <summary>记录本次文件搜索：输入补全历史（仅自动搜索，完成且代际最新时补记）+ 搜索历史。</summary>
    private void RecordFileSearchHistory(FileSearchQuery query, FileSearchStats stats, bool isAutoSearch)
    {
        if (isAutoSearch)
            AddInputHistory(SearchPathSuggestions, query.SearchPath, _inputHistoryService.AddSearchPath);

        // 搜索历史（与文本模式共用列表，卡片以「文件名」标签区分）
        var historyEntry = new SearchHistoryEntry
        {
            SearchMode = "file",
            SearchPath = query.SearchPath,
            SearchText = query.NamePattern,
            IsRegex = false,
            CaseSensitive = query.CaseSensitive,
            FileExtensions = string.Join(';', query.Extensions),
            IncludeFiles = query.IncludeFiles,
            IncludeFolders = query.IncludeFolders,
            ExcludeDirs = query.ExcludeDirs,
            HiddenWhitelist = query.HiddenWhitelist,
            DateFilterEnabled = query.DateFilterEnabled,
            DateFrom = query.DateFrom,
            DateTo = query.DateTo,
            SearchedFiles = stats.FileCount + stats.FolderCount,
            FoundFiles = stats.FileCount,
            TotalMatches = stats.FolderCount,
            ElapsedTime = stats.Elapsed,
            SearchedAt = DateTime.Now
        };
        // 持久化到 JSON（service 内完成去重与 50 条截断），UI 集合同步保持一致
        _historyService.AddEntry(historyEntry);

        var existing = HistoryEntries.FirstOrDefault(e => e.DedupKey == historyEntry.DedupKey);
        if (existing is not null)
            HistoryEntries.Remove(existing);
        HistoryEntries.Insert(0, historyEntry);
        while (HistoryEntries.Count > SearchHistoryService.MaxHistoryCount)
            HistoryEntries.RemoveAt(HistoryEntries.Count - 1);
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsFileSearchMode)
        {
            _fileSearchDebounce.Cancel();
            // 签名清空：取消后若重新输入相同条件，自动搜索仍会执行（不误判为重复）
            _lastFileQuerySignature = null;
            _fileSearchService.Cancel();
        }
        else
        {
            _searchService.Cancel();
        }
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
        if (IsFileSearchMode)
        {
            OpenFileResults();
            return;
        }

        // 多选时打开所有选中文件；单选时打开焦点项
        var targets = SelectedResults.Count > 0 ? SelectedResults.ToList()
                   : SelectedResult is not null ? [SelectedResult]
                   : [];
        foreach (var r in targets)
        {
            try { Process.Start(new ProcessStartInfo(r.FilePath) { UseShellExecute = true }); }
            catch { }
        }
    }

    /// <summary>
    /// 文件模式打开：文件优先用 VS Code（不可用时回退系统默认程序），
    /// 文件夹用资源管理器打开。
    /// </summary>
    private void OpenFileResults()
    {
        var targets = SelectedFileResults.Count > 0 ? SelectedFileResults.ToList()
                   : SelectedFileResult is not null ? [SelectedFileResult]
                   : [];
        foreach (var r in targets)
        {
            try
            {
                if (r.IsFolder)
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{r.FilePath}\"") { UseShellExecute = true });
                }
                else
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo("code", $"\"{r.FilePath}\"") { UseShellExecute = true });
                    }
                    catch
                    {
                        Process.Start(new ProcessStartInfo(r.FilePath) { UseShellExecute = true });
                    }
                }
            }
            catch { }
        }
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
        // 多选时换行拼接所有路径；单选时复制焦点项
        var paths = new List<string>();
        if (IsFileSearchMode)
        {
            if (SelectedFileResults.Count > 0) paths.AddRange(SelectedFileResults.Select(r => r.FilePath));
            else if (SelectedFileResult is not null) paths.Add(SelectedFileResult.FilePath);
        }
        else
        {
            if (SelectedResults.Count > 0) paths.AddRange(SelectedResults.Select(r => r.FilePath));
            else if (SelectedResult is not null) paths.Add(SelectedResult.FilePath);
        }
        if (paths.Count == 0) return;
        if (TrySetClipboardText(string.Join(Environment.NewLine, paths)))
            StatusText = paths.Count > 1 ? $"已复制 {paths.Count} 个路径" : "已复制路径";
        else
            StatusText = "复制失败：剪贴板被占用，请重试";
    }

    [RelayCommand]
    private void CopyFileName()
    {
        // 多选时换行拼接所有文件名；单选时复制焦点项
        var names = new List<string>();
        if (IsFileSearchMode)
        {
            if (SelectedFileResults.Count > 0) names.AddRange(SelectedFileResults.Select(r => r.FileName));
            else if (SelectedFileResult is not null) names.Add(SelectedFileResult.FileName);
        }
        else
        {
            if (SelectedResults.Count > 0) names.AddRange(SelectedResults.Select(r => r.FileName));
            else if (SelectedResult is not null) names.Add(SelectedResult.FileName);
        }
        if (names.Count == 0) return;
        if (TrySetClipboardText(string.Join(Environment.NewLine, names)))
            StatusText = names.Count > 1 ? $"已复制 {names.Count} 个文件名" : "已复制文件名";
        else
            StatusText = "复制失败：剪贴板被占用，请重试";
    }

    // 多选时禁用：explorer /select 不支持同时高亮多个文件
    [RelayCommand(CanExecute = nameof(CanOpenInExplorer))]
    private void OpenInExplorer()
    {
        var path = IsFileSearchMode ? SelectedFileResult?.FilePath : SelectedResult?.FilePath;
        if (path is null) return;
        try
        {
            // /select,<path> 让资源管理器打开父目录并定位选中该文件（文件夹则定位文件夹）
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                { UseShellExecute = true });
        }
        catch { }
    }

    private bool CanOpenInExplorer()
    {
        return IsFileSearchMode
            ? SelectedFileResults.Count <= 1 && SelectedFileResult is not null
            : SelectedResults.Count <= 1 && SelectedResult is not null;
    }

    // --- 历史操作 ---

    public void RestoreFromEntry(SearchHistoryEntry entry)
    {
        if (entry.IsFileMode)
        {
            IsFileSearchMode = true;
            SearchPath = entry.SearchPath;
            FileSearchText = entry.SearchText;
            FileCaseSensitive = entry.CaseSensitive;
            IncludeFiles = entry.IncludeFiles;
            IncludeFolders = entry.IncludeFolders;
            ExcludeDirs = entry.ExcludeDirs;
            HiddenWhitelist = entry.HiddenWhitelist;
            DateFilterEnabled = entry.DateFilterEnabled;
            DateFrom = entry.DateFrom;
            DateTo = entry.DateTo;
            ApplyExtensionSelection(entry.FileExtensions);
            return;
        }

        IsFileSearchMode = false;
        SearchPath = entry.SearchPath;
        SearchText = entry.SearchText;
        IsRegex = entry.IsRegex;
        IsPlainText = !entry.IsRegex;
        CaseSensitive = entry.CaseSensitive;
        WholeWord = entry.WholeWord;
        FileFilter = entry.FileFilter;
        ExcludeDirs = entry.ExcludeDirs;
        HiddenWhitelist = entry.HiddenWhitelist;
        DateFilterEnabled = entry.DateFilterEnabled;
        DateFrom = entry.DateFrom;
        DateTo = entry.DateTo;
    }

    public void SaveSession()
    {
        var entry = new SearchHistoryEntry
        {
            SearchPath = SearchPath,
            ExcludeDirs = ExcludeDirs,
            HiddenWhitelist = HiddenWhitelist,
            DateFilterEnabled = DateFilterEnabled,
            DateFrom = DateFrom,
            DateTo = DateTo
        };

        if (IsFileSearchMode)
        {
            entry.SearchMode = "file";
            entry.SearchText = FileSearchText;
            entry.IsRegex = false;
            entry.CaseSensitive = FileCaseSensitive;
            entry.FileExtensions = string.Join(';', ExtensionItems.Where(i => i.IsSelected).Select(i => i.Name));
            entry.IncludeFiles = IncludeFiles;
            entry.IncludeFolders = IncludeFolders;
        }
        else
        {
            entry.SearchMode = "text";
            entry.SearchText = SearchText;
            entry.IsRegex = IsRegex;
            entry.CaseSensitive = CaseSensitive;
            entry.WholeWord = WholeWord;
            entry.FileFilter = FileFilter;
        }

        _historyService.SaveCurrentSession(entry);
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
                HiddenWhitelist = HiddenWhitelist,
                DateFilterEnabled = DateFilterEnabled,
                DateFrom = DateFrom,
                DateTo = DateTo,
                SearchedFiles = stats.SearchedFiles,
                FoundFiles = (int)stats.FoundFiles,
                TotalMatches = TotalMatches,
                ElapsedTime = stats.Elapsed,
                SearchedAt = DateTime.Now
            };
            // 持久化到 JSON（service 内完成去重与 50 条截断），UI 集合同步保持一致
            _historyService.AddEntry(historyEntry);

            var existing = HistoryEntries.FirstOrDefault(e => e.DedupKey == historyEntry.DedupKey);
            if (existing is not null)
                HistoryEntries.Remove(existing);
            HistoryEntries.Insert(0, historyEntry);
            while (HistoryEntries.Count > SearchHistoryService.MaxHistoryCount)
                HistoryEntries.RemoveAt(HistoryEntries.Count - 1);
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

    // --- 文件搜索事件处理 ---
    // 处理器由 AttachFileSearchHandlers 按次挂接（捕获代际），不再使用固定订阅

    /// <summary>搜索完成后统一重排：文件夹优先，同类按文件名升序（不区分大小写）。</summary>
    private void SortFileResults()
    {
        var sorted = FileResults
            .OrderBy(r => r.IsFolder ? 1 : 0)
            .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        FileResults.Clear();
        foreach (var r in sorted)
            FileResults.Add(r);
    }

    // --- 后缀过滤 ---

    /// <summary>创建后缀项并入列：订阅勾选变化（刷新按钮文案并持久化，批量变更期间抑制）。</summary>
    private void AddExtensionItem(FileExtensionItem item)
    {
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(FileExtensionItem.IsSelected)) return;
            UpdateExtensionFilterDisplay();
            if (!_suppressExtensionPersist)
                SaveExtensionConfig();
            ScheduleLiveFileSearch(); // 后缀勾选变化（C# 侧过滤）→ 防抖自动重搜（走缓存）
        };
        ExtensionItems.Add(item);
    }

    private void UpdateExtensionFilterDisplay()
    {
        var selected = ExtensionItems.Where(i => i.IsSelected)
            .Select(i => "*." + i.Name)
            .ToList();
        ExtensionFilterDisplay = selected.Count == 0
            ? "后缀: 全部"
            : selected.Count <= 2
                ? $"后缀: {string.Join(", ", selected)}"
                : $"后缀: {selected[0]}, {selected[1]} +{selected.Count - 2}";
        OnPropertyChanged(nameof(ExtensionFilterDisplay));
    }

    private void SaveExtensionConfig()
    {
        _extensionConfigService.Save(new FileExtensionConfig
        {
            Items = ExtensionItems.Select(i => i.Name).ToList(),
            Selected = ExtensionItems.Where(i => i.IsSelected).Select(i => i.Name).ToList()
        });
    }

    /// <summary>按历史条目恢复勾选状态（";" 连接的后缀名；空 = 全部取消勾选）。</summary>
    private void ApplyExtensionSelection(string? joined)
    {
        var names = (joined ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _suppressExtensionPersist = true;
        try
        {
            foreach (var item in ExtensionItems)
                item.IsSelected = names.Contains(item.Name);
        }
        finally
        {
            _suppressExtensionPersist = false;
        }
        UpdateExtensionFilterDisplay();
        SaveExtensionConfig();
    }

    /// <summary>
    /// 添加自定义后缀。接受 log / .log / *.log 三种写法，统一为不含点的小写后缀；
    /// 已存在（内置或自定义）时忽略。
    /// </summary>
    [RelayCommand]
    private void AddExtension()
    {
        var name = ExtensionInput.Trim().ToLowerInvariant().TrimStart('*', '.');
        ExtensionInput = string.Empty;
        if (name.Length == 0 || ExtensionItems.Any(i => i.Name == name)) return;

        var item = new FileExtensionItem { Name = name, IsSelected = true };
        AddExtensionItem(item);
        UpdateExtensionFilterDisplay();
        SaveExtensionConfig();
    }

    /// <summary>从列表移除后缀（内置项与自定义项均可，可用「恢复默认」还原内置）。</summary>
    [RelayCommand]
    private void RemoveExtension(FileExtensionItem? item)
    {
        if (item is null) return;
        ExtensionItems.Remove(item);
        UpdateExtensionFilterDisplay();
        SaveExtensionConfig();
    }

    [RelayCommand]
    private void ClearExtensionSelection()
    {
        _suppressExtensionPersist = true;
        try
        {
            foreach (var item in ExtensionItems)
                item.IsSelected = false;
        }
        finally
        {
            _suppressExtensionPersist = false;
        }
        UpdateExtensionFilterDisplay();
        SaveExtensionConfig();
    }

    [RelayCommand]
    private void RestoreDefaultExtensions()
    {
        ExtensionItems.Clear();
        foreach (var name in FileExtensionConfigService.DefaultExtensions)
            AddExtensionItem(new FileExtensionItem { Name = name });
        UpdateExtensionFilterDisplay();
        SaveExtensionConfig();
    }
}
