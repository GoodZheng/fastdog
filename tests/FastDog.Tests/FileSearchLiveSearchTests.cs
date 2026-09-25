using FastDog.Models;
using FastDog.Services;
using Xunit;

namespace FastDog.Tests;

/// <summary>
/// 文件模式输入即搜的纯逻辑单测：C# 侧后缀过滤、查询条件签名。
/// </summary>
public class FileSearchLiveSearchTests
{
    // --- MatchesExtensions：后缀过滤从 rg --iglob 移到 C# 后的语义 ---

    [Theory]
    [InlineData("cs", @"E:\code\a.cs", true)]
    [InlineData("cs", @"E:\code\a.CS", true)]     // 忽略大小写
    [InlineData("cs", @"E:\code\a.txt", false)]
    [InlineData("cs", @"E:\code\sub\a.cs", true)] // 子目录同样按扩展名命中
    public void MatchesExtensions_SingleExtension(string ext, string path, bool expected)
    {
        Assert.Equal(expected, FileSearchService.MatchesExtensions(path, [ext]));
    }

    [Theory]
    [InlineData("*.cs", @"E:\code\a.cs", true)]  // 用户自定义的 *.cs 写法
    [InlineData(".cs", @"E:\code\a.cs", true)]   // .cs 写法
    [InlineData("tar.gz", @"E:\code\x.tar.gz", true)]  // 多段后缀
    [InlineData("tar.gz", @"E:\code\x.gz", false)]
    public void MatchesExtensions_NormalizesUserInput(string ext, string path, bool expected)
    {
        Assert.Equal(expected, FileSearchService.MatchesExtensions(path, [ext]));
    }

    [Fact]
    public void MatchesExtensions_EmptyOrNull_AcceptsAll()
    {
        Assert.True(FileSearchService.MatchesExtensions(@"E:\code\a.cs", []));
        Assert.True(FileSearchService.MatchesExtensions(@"E:\code\a.cs", null));
    }

    [Fact]
    public void MatchesExtensions_MultiSelect_IsOrSemantics()
    {
        var exts = new[] { "cs", "md" };
        Assert.True(FileSearchService.MatchesExtensions(@"E:\code\a.cs", exts));
        Assert.True(FileSearchService.MatchesExtensions(@"E:\code\b.md", exts));
        Assert.False(FileSearchService.MatchesExtensions(@"E:\code\c.py", exts));
    }

    [Fact]
    public void MatchesExtensions_FileWithoutExtension_Rejected()
    {
        Assert.False(FileSearchService.MatchesExtensions(@"E:\code\Makefile", ["cs"]));
    }

    // --- BuildSignature：输入即搜的签名去重 ---

    private static FileSearchQuery SampleQuery() => new()
    {
        SearchPath = @"E:\code",
        NamePattern = "dog",
        CaseSensitive = false,
        IncludeFiles = true,
        IncludeFolders = true,
        Extensions = ["cs", "md"],
        ExcludeDirs = "bin;obj",
        HiddenWhitelist = "",
        DateFilterEnabled = false,
        DateFrom = null,
        DateTo = null
    };

    [Fact]
    public void BuildSignature_SameInputs_Equal()
    {
        Assert.Equal(SampleQuery().BuildSignature(), SampleQuery().BuildSignature());
    }

    [Fact]
    public void BuildSignature_EachFilterField_ChangesSignature()
    {
        var baseline = SampleQuery().BuildSignature();

        var pattern = SampleQuery();
        pattern.NamePattern = "cat";
        Assert.NotEqual(baseline, pattern.BuildSignature());

        var path = SampleQuery();
        path.SearchPath = @"E:\other";
        Assert.NotEqual(baseline, path.BuildSignature());

        var caseSensitive = SampleQuery();
        caseSensitive.CaseSensitive = true;
        Assert.NotEqual(baseline, caseSensitive.BuildSignature());

        var foldersOff = SampleQuery();
        foldersOff.IncludeFolders = false;
        Assert.NotEqual(baseline, foldersOff.BuildSignature());

        var extensions = SampleQuery();
        extensions.Extensions = ["cs"];
        Assert.NotEqual(baseline, extensions.BuildSignature());

        var exclude = SampleQuery();
        exclude.ExcludeDirs = "bin";
        Assert.NotEqual(baseline, exclude.BuildSignature());

        var dated = SampleQuery();
        dated.DateFrom = new DateTime(2026, 1, 1);
        Assert.NotEqual(baseline, dated.BuildSignature());
    }
}
