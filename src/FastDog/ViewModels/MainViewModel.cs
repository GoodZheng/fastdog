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

    [ObservableProperty] private SearchResult? _selectedResult;
    [ObservableProperty] private MatchLine? _selectedMatchLine;

    // --- 状态栏 ---
    [ObservableProperty] private bool _isSearching = false;
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private int _totalFiles = 0;
    [ObservableProperty] private int _totalMatches = 0;
    [ObservableProperty] private string _elapsedTime = string.Empty;

    public MainViewModel()
    {
        _searchService.ResultReceived += OnResultReceived;
        _searchService.SearchCompleted += OnSearchCompleted;
        _searchService.SearchCancelled += OnSearchCancelled;
    }

    partial void OnSelectedResultChanged(SearchResult? value)
    {
        MatchLines.Clear();
        if (value is not null)
        {
            foreach (var match in value.Matches)
                MatchLines.Add(match);
        }
    }

    // --- 命令 ---

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchPath) || string.IsNullOrWhiteSpace(SearchText))
            return;

        if (!Directory.Exists(SearchPath))
        {
            System.Windows.MessageBox.Show("搜索路径不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SearchResults.Clear();
        MatchLines.Clear();
        TotalFiles = 0;
        TotalMatches = 0;
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
            await _searchService.SearchAsync(query, SearchPath);
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

    private void OnSearchCompleted(string elapsed)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsSearching = false;
            ElapsedTime = elapsed;
            StatusText = $"搜索完成 | {TotalFiles} 个文件 | {TotalMatches} 处匹配 | {elapsed}";
        });
    }

    private void OnSearchCancelled()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsSearching = false;
            StatusText = $"搜索已取消 | {TotalFiles} 个文件 | {TotalMatches} 处匹配";
        });
    }
}
