using FastDog.Models;
using FastDog.Services;
using Xunit;

namespace FastDog.Tests;

/// <summary>
/// 文件搜索集成测试（真实 rg.exe，验证枚举缓存与输入即搜双路径）：
/// 冷构建走 rg 流式推送并落缓存；条件变化走缓存纯内存过滤（ResultsReady 批量、无流式事件）。
/// </summary>
public class FileSearchServiceIntegrationTests : IDisposable
{
    private readonly string _root;
    private readonly FileSearchService _service = new();

    public FileSearchServiceIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fastdog-it-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        // 4 个文件 → 2 个文件夹候选（sub 与 sub/deep）
        File.WriteAllText(Path.Combine(_root, "a.cs"), "x");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "sub", "c.cs"), "x");
        Directory.CreateDirectory(Path.Combine(_root, "sub", "deep"));
        File.WriteAllText(Path.Combine(_root, "sub", "deep", "d.py"), "x");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private FileSearchQuery Query(string pattern = "", List<string>? extensions = null) => new()
    {
        SearchPath = _root,
        NamePattern = pattern,
        IncludeFiles = true,
        IncludeFolders = true,
        Extensions = extensions ?? []
    };

    [Fact]
    public async Task ColdBuild_StreamsResults_AndCompletes()
    {
        var streamed = new List<FileSearchResult>();
        FileSearchBatch? batch = null;
        FileSearchStats? stats = null;
        _service.ResultReceived += r => streamed.Add(r);
        _service.ResultsReady += b => batch = b;
        _service.SearchCompleted += s => stats = s;

        await _service.SearchAsync(Query());

        // 冷构建：流式推送 4 个文件 + 2 个文件夹，无批量事件
        Assert.Null(batch);
        Assert.Equal(4, streamed.Count(r => !r.IsFolder));
        Assert.Equal(2, streamed.Count(r => r.IsFolder));
        Assert.NotNull(stats);
        Assert.Equal(4, stats!.FileCount);
        Assert.Equal(2, stats.FolderCount);
    }

    [Fact]
    public async Task CacheHit_FiltersInMemory_NoStreamEvents()
    {
        FileSearchStats? first = null;
        _service.SearchCompleted += s => first = s;
        await _service.SearchAsync(Query());
        Assert.NotNull(first);

        var streamed = new List<FileSearchResult>();
        FileSearchBatch? batch = null;
        FileSearchStats? stats = null;
        _service.ResultReceived += r => streamed.Add(r);
        _service.ResultsReady += b => batch = b;
        _service.SearchCompleted += s => stats = s;

        // 只改关键词 → 走缓存：批量一次性推送，无流式事件
        await _service.SearchAsync(Query("a"));

        Assert.NotNull(batch);
        Assert.Empty(streamed);
        var itemNames = batch!.Items.Select(r => r.FileName).ToList();
        Assert.Contains("a.cs", itemNames);
        Assert.DoesNotContain("b.txt", itemNames);
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.FileCount);
    }

    [Fact]
    public async Task CacheHit_ExtensionFilter_AppliesInMemory()
    {
        await _service.SearchAsync(Query());

        FileSearchBatch? batch = null;
        _service.ResultsReady += b => batch = b;

        // 后缀过滤在 C# 侧（rg 参数不再含 --iglob）：缓存命中路径下同样生效
        await _service.SearchAsync(Query(extensions: ["cs"]));

        Assert.NotNull(batch);
        var fileNames = batch!.Items.Where(r => !r.IsFolder).Select(r => r.FileName).ToList();
        Assert.Equal(["a.cs", "c.cs"], fileNames);
    }

    [Fact]
    public async Task ForceRefresh_RebuildsCache_WithColdPath()
    {
        await _service.SearchAsync(Query());

        var streamed = new List<FileSearchResult>();
        FileSearchBatch? batch = null;
        _service.ResultReceived += r => streamed.Add(r);
        _service.ResultsReady += b => batch = b;

        // 手动「搜索」按钮语义：强制重建 → 回到流式路径
        await _service.SearchAsync(Query(), forceRefresh: true);

        Assert.Null(batch);
        Assert.Equal(4, streamed.Count(r => !r.IsFolder));
    }
}
