using FastDog.Services;

namespace FastDog.Tests;

public class InputHistoryServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly InputHistoryService _service;

    public InputHistoryServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"FastDogTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _service = new InputHistoryService(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void AddSearchPath_FirstItem_ListHasOneItem()
    {
        _service.AddSearchPath(@"C:\test");

        var paths = _service.LoadSearchPaths();
        Assert.Single(paths);
        Assert.Equal(@"C:\test", paths[0]);
    }

    [Fact]
    public void AddSearchPath_Duplicate_ReplacesOld()
    {
        _service.AddSearchPath(@"C:\test");
        _service.AddSearchPath(@"C:\other");
        _service.AddSearchPath(@"C:\test");

        var paths = _service.LoadSearchPaths();
        Assert.Equal(2, paths.Count);
        Assert.Equal(@"C:\test", paths[0]);
        Assert.Equal(@"C:\other", paths[1]);
    }

    [Fact]
    public void AddSearchPath_NullOrWhitespace_NotRecorded()
    {
        _service.AddSearchPath("");
        _service.AddSearchPath("   ");

        Assert.Empty(_service.LoadSearchPaths());
    }

    [Fact]
    public void AddSearchPath_ValueIsTrimmed()
    {
        _service.AddSearchPath(@"  C:\test  ");

        Assert.Equal(@"C:\test", _service.LoadSearchPaths()[0]);
    }

    [Fact]
    public void AddSearchPath_ExceedsMaxLimit_TrimsOldest()
    {
        for (int i = 0; i < 55; i++)
            _service.AddSearchPath($@"C:\dir_{i}");

        var paths = _service.LoadSearchPaths();
        Assert.Equal(50, paths.Count);
        Assert.Equal(@"C:\dir_54", paths[0]);
        Assert.Equal(@"C:\dir_5", paths[49]);
    }

    [Fact]
    public void AddSearchText_Duplicate_ReplacesOld()
    {
        _service.AddSearchText("hello");
        _service.AddSearchText("world");
        _service.AddSearchText("hello");

        var texts = _service.LoadSearchTexts();
        Assert.Equal(2, texts.Count);
        Assert.Equal("hello", texts[0]);
        Assert.Equal("world", texts[1]);
    }

    [Fact]
    public void PathAndTextTablesAreIndependent()
    {
        _service.AddSearchPath(@"C:\path");
        _service.AddSearchText("query");

        Assert.Single(_service.LoadSearchPaths());
        Assert.Single(_service.LoadSearchTexts());
        Assert.Equal(@"C:\path", _service.LoadSearchPaths()[0]);
        Assert.Equal("query", _service.LoadSearchTexts()[0]);
    }

    [Fact]
    public void Add_RoundTrip_PersistsAcrossInstances()
    {
        _service.AddSearchPath(@"C:\persist_path");
        _service.AddSearchText("persist_query");

        // 用同一目录新建实例，验证确实落盘
        var reloaded = new InputHistoryService(_testDir);
        Assert.Equal(@"C:\persist_path", reloaded.LoadSearchPaths()[0]);
        Assert.Equal("persist_query", reloaded.LoadSearchTexts()[0]);
    }

    [Fact]
    public void Load_CorruptJson_ReturnsEmpty()
    {
        // 先写入正常数据
        _service.AddSearchPath(@"C:\test");
        // 再用损坏内容覆盖
        File.WriteAllText(Path.Combine(_testDir, "input-history.json"), "{ this is not json");

        var reloaded = new InputHistoryService(_testDir);
        Assert.Empty(reloaded.LoadSearchPaths());
        Assert.Empty(reloaded.LoadSearchTexts());
    }

    [Fact]
    public void ItemsAreOrderedByTimeDescending()
    {
        _service.AddSearchText("first");
        _service.AddSearchText("second");
        _service.AddSearchText("third");

        var texts = _service.LoadSearchTexts();
        Assert.Equal("third", texts[0]);
        Assert.Equal("second", texts[1]);
        Assert.Equal("first", texts[2]);
    }
}
