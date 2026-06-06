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
