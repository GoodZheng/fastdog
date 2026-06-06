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
