using FastDog.Models;
using FastDog.Services;
using Xunit;

namespace FastDog.Tests;

public class FileExtensionConfigServiceTests : IDisposable
{
    private readonly string _dir;

    public FileExtensionConfigServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"fastdog-ext-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var service = new FileExtensionConfigService(_dir);
        var config = service.Load();
        Assert.Equal(FileExtensionConfigService.DefaultExtensions, config.Items);
        Assert.Empty(config.Selected);
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsDefaults()
    {
        File.WriteAllText(Path.Combine(_dir, "extension-config.json"), "{ not valid json");
        var service = new FileExtensionConfigService(_dir);
        var config = service.Load();
        Assert.Equal(FileExtensionConfigService.DefaultExtensions, config.Items);
    }

    [Fact]
    public void SaveLoad_RoundTrip_PreservesItemsAndSelection()
    {
        var service = new FileExtensionConfigService(_dir);
        service.Save(new FileExtensionConfig
        {
            Items = ["cs", "md", "log"],   // 删除了部分内置 + 添加了自定义
            Selected = ["cs", "log"]
        });

        var loaded = new FileExtensionConfigService(_dir).Load();
        Assert.Equal(["cs", "md", "log"], loaded.Items);
        Assert.Equal(["cs", "log"], loaded.Selected);
    }

    [Fact]
    public void Load_EmptyItemsFile_FallsBackToDefaults()
    {
        // Items 为空的配置视为无效（正常使用不会产生），回退默认列表
        var service = new FileExtensionConfigService(_dir);
        service.Save(new FileExtensionConfig { Items = [], Selected = [] });

        var loaded = new FileExtensionConfigService(_dir).Load();
        Assert.Equal(FileExtensionConfigService.DefaultExtensions, loaded.Items);
    }
}
