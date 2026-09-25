using FastDog.Models;
using FastDog.Services;
using Xunit;

namespace FastDog.Tests;

public class FileSearchArgumentTests
{
    [Fact]
    public void BuildFileSearchArguments_ContainsFilesFlag()
    {
        var query = new FileSearchQuery { SearchPath = @"E:\code" };
        var args = RipgrepBridge.BuildFileSearchArguments(query);
        Assert.Contains("--files", args);
        Assert.DoesNotContain("--json", args);   // 文件模式不是 JSON 输出
        Assert.Contains(@"E:\code", args);
    }

    [Fact]
    public void BuildFileSearchArguments_Extensions_NotEmittedAsGlobs()
    {
        // 后缀过滤已移到 C# 侧（FileSearchService.MatchesExtensions）：
        // 枚举参数与后缀选择解耦，rg 参数不再出现 --iglob
        var query = new FileSearchQuery
        {
            SearchPath = @"E:\code",
            Extensions = ["cs", "md"]
        };
        var args = RipgrepBridge.BuildFileSearchArguments(query);
        Assert.DoesNotContain("--iglob", args);
    }

    [Fact]
    public void BuildFileSearchArguments_EmptyExtensions_NoExtGlob()
    {
        var query = new FileSearchQuery { SearchPath = @"E:\code" };
        var args = RipgrepBridge.BuildFileSearchArguments(query);
        Assert.DoesNotContain("--iglob", args);
    }

    [Fact]
    public void BuildFileSearchArguments_AlwaysExcludesGit()
    {
        var query = new FileSearchQuery { SearchPath = @"E:\code" };
        var args = RipgrepBridge.BuildFileSearchArguments(query);
        Assert.Contains("--glob !.git", args);
    }

    [Fact]
    public void BuildFileSearchArguments_ExcludeDirs()
    {
        var query = new FileSearchQuery
        {
            SearchPath = @"E:\code",
            ExcludeDirs = "bin;obj"
        };
        var args = RipgrepBridge.BuildFileSearchArguments(query);
        Assert.Contains("--glob !bin", args);
        Assert.Contains("--glob !obj", args);
    }

    [Fact]
    public void BuildFileSearchArguments_HiddenWhitelistWithoutExtensions()
    {
        // 白名单开启隐藏搜索；无后缀过滤时需用 '*' 把普通文件拉回 include 集
        var query = new FileSearchQuery
        {
            SearchPath = @"E:\code",
            HiddenWhitelist = ".config"
        };
        var args = RipgrepBridge.BuildFileSearchArguments(query);
        Assert.Contains("--hidden", args);
        Assert.Contains("--glob !.*", args);
        Assert.Contains("--glob .config", args);
        Assert.Contains("--glob .config/**", args);
        Assert.Contains("--glob * ", args);   // '*' 拉回普通文件
    }

    [Fact]
    public void BuildFileSearchArguments_HiddenWhitelist_AlwaysAddsStarGlob()
    {
        // 白名单开启隐藏搜索后 rg 进入 include 模式，无论有无后缀过滤
        // （后缀已移到 C# 侧）都需要 '*' 把普通文件拉回 include 集
        var query = new FileSearchQuery
        {
            SearchPath = @"E:\code",
            Extensions = ["cs"],
            HiddenWhitelist = ".config"
        };
        var args = RipgrepBridge.BuildFileSearchArguments(query);
        Assert.Contains("--hidden", args);
        Assert.Contains("--glob * ", args);
    }

    [Fact]
    public void BuildFileSearchArguments_PathWithSpacesQuoted()
    {
        var query = new FileSearchQuery { SearchPath = @"E:\My Files\code" };
        var args = RipgrepBridge.BuildFileSearchArguments(query);
        Assert.Contains("\"E:\\My Files\\code\"", args);
    }

    [Fact]
    public void BuildFileSearchArguments_Quoting_PathWithTrailingBackslash()
    {
        // 尾反斜杠需翻倍转义，否则结束引号被转义导致 rg 参数解析错误；
        // 中间不挨引号的反斜杠保持单写
        var query = new FileSearchQuery { SearchPath = @"E:\My Files\" };
        var args = RipgrepBridge.BuildFileSearchArguments(query);
        Assert.Contains("\"E:\\My Files\\\\\"", args);
    }
}
