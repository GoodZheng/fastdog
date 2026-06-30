using System.Text.Json;

namespace FastDog.Services;

/// <summary>
/// 搜索路径与搜索内容的输入历史持久化（%APPDATA%\FastDog\input-history.json）。
/// 路径表与内容表相互独立，各自去重、各 50 条上限、按时间倒序。
/// 设计上与 <see cref="SearchHistoryService"/> 保持一致：双构造函数 +
/// 静默恢复（加载失败重置为空，绝不抛出）。
/// </summary>
public class InputHistoryService
{
    private const int MaxCount = 50;
    private const string FileName = "input-history.json";

    private readonly string _filePath;
    private List<string> _searchPaths = [];
    private List<string> _searchTexts = [];

    public InputHistoryService() : this(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastDog"))
    {
    }

    public InputHistoryService(string directory)
    {
        _filePath = Path.Combine(directory, FileName);
        Load();
    }

    public List<string> LoadSearchPaths() => _searchPaths;
    public List<string> LoadSearchTexts() => _searchTexts;

    public void AddSearchPath(string value) => AddInternal(ref _searchPaths, value);
    public void AddSearchText(string value) => AddInternal(ref _searchTexts, value);

    private void AddInternal(ref List<string> list, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        value = value.Trim();
        list.Remove(value);
        list.Insert(0, value);

        if (list.Count > MaxCount)
        {
            list = list.Take(MaxCount).ToList();
        }

        Save();
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            _searchPaths = [];
            _searchTexts = [];
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<HistoryData>(json);
            _searchPaths = data?.SearchPaths ?? [];
            _searchTexts = data?.SearchTexts ?? [];
        }
        catch
        {
            _searchPaths = [];
            _searchTexts = [];
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);

        var data = new HistoryData
        {
            SearchPaths = _searchPaths,
            SearchTexts = _searchTexts
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_filePath, json);
    }

    private class HistoryData
    {
        public List<string> SearchPaths { get; set; } = [];
        public List<string> SearchTexts { get; set; } = [];
    }
}
