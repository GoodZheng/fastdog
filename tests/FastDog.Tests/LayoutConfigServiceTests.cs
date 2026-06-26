using FastDog.Services;
using FastDog.Models;

namespace FastDog.Tests;

public class LayoutConfigServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly LayoutConfigService _service;

    public LayoutConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"FastDogLayoutTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _service = new LayoutConfigService(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Load_NoFile_ReturnsNull()
    {
        var result = _service.Load();
        Assert.Null(result);
    }

    [Fact]
    public void SaveThenLoad_RoundTrip()
    {
        var config = new LayoutConfig
        {
            Left = 100,
            Top = 200,
            Width = 1200,
            Height = 800,
            WindowState = 0,
            VerticalSplitRatio = 0.6,
            HorizontalSplitRatio = 0.5
        };

        _service.Save(config);
        var loaded = _service.Load();

        Assert.NotNull(loaded);
        Assert.Equal(100, loaded.Left);
        Assert.Equal(200, loaded.Top);
        Assert.Equal(1200, loaded.Width);
        Assert.Equal(800, loaded.Height);
        Assert.Equal(0, loaded.WindowState);
        Assert.Equal(0.6, loaded.VerticalSplitRatio, 0.001);
        Assert.Equal(0.5, loaded.HorizontalSplitRatio, 0.001);
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_testDir, "layout-config.json"), "{invalid json");
        var result = _service.Load();
        Assert.Null(result);
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        var nestedDir = Path.Combine(_testDir, "nested", "sub");
        var service = new LayoutConfigService(nestedDir);
        var config = new LayoutConfig();

        service.Save(config);

        Assert.True(Directory.Exists(nestedDir));
        Assert.True(File.Exists(Path.Combine(nestedDir, "layout-config.json")));
    }

    [Fact]
    public void SaveThenLoad_PreservesMaximizedState()
    {
        var config = new LayoutConfig
        {
            Left = 50,
            Top = 50,
            Width = 1920,
            Height = 1080,
            WindowState = 2  // Maximized
        };

        _service.Save(config);
        var loaded = _service.Load();

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.WindowState);
    }

    [Fact]
    public void Load_OverwriteSave_ReturnsLatest()
    {
        var config1 = new LayoutConfig { Left = 100 };
        var config2 = new LayoutConfig { Left = 200 };

        _service.Save(config1);
        _service.Save(config2);

        var loaded = _service.Load();
        Assert.NotNull(loaded);
        Assert.Equal(200, loaded.Left);
    }
}
