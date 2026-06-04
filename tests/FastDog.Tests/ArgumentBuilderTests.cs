using FastDog.Services;
using FastDog.Models;
using Xunit;

namespace FastDog.Tests;

public class ArgumentBuilderTests
{
    [Fact]
    public void BuildArguments_BasicRegexSearch()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "class",
            IsRegex = true
        };
        var args = RipgrepBridge.BuildArguments(query);
        Assert.Contains("--json", args);
        Assert.Contains(" -i ", args);
        Assert.Contains(@"""E:\code""", args);
        Assert.Contains(@"""class""", args);
        Assert.DoesNotContain("-F", args);
    }

    [Fact]
    public void BuildArguments_PlainTextSearch()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "hello world",
            IsRegex = false
        };
        var args = RipgrepBridge.BuildArguments(query);
        Assert.Contains("-F", args);
    }

    [Fact]
    public void BuildArguments_CaseSensitive()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "test",
            CaseSensitive = true
        };
        var args = RipgrepBridge.BuildArguments(query);
        Assert.DoesNotContain(" -i ", args);
    }

    [Fact]
    public void BuildArguments_WholeWord()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "test",
            WholeWord = true
        };
        var args = RipgrepBridge.BuildArguments(query);
        Assert.Contains("-w", args);
    }

    [Fact]
    public void BuildArguments_FileFilter()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "test",
            FileFilter = "*.cs;*.txt"
        };
        var args = RipgrepBridge.BuildArguments(query);
        Assert.Contains("--iglob", args);
        Assert.Contains("*.cs", args);
        Assert.Contains("*.txt", args);
    }

    [Fact]
    public void BuildArguments_ExcludeDirs()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "test",
            ExcludeDirs = "bin;obj;.git"
        };
        var args = RipgrepBridge.BuildArguments(query);
        Assert.Contains("--glob", args);
        Assert.Contains("!bin", args);
        Assert.Contains("!obj", args);
        Assert.Contains("!.git", args);
    }

    [Fact]
    public void BuildArguments_DefaultExcludesGit()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "test",
            ExcludeDirs = "bin;obj"
        };
        var args = RipgrepBridge.BuildArguments(query);
        Assert.Contains("!.git", args);
    }

    [Fact]
    public void BuildArguments_AllOptionsCombined()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code\project",
            SearchText = @"\bclass\b",
            IsRegex = true,
            CaseSensitive = true,
            WholeWord = false,
            FileFilter = "*.cs",
            ExcludeDirs = "bin;obj"
        };
        var args = RipgrepBridge.BuildArguments(query);
        Assert.Contains("--json", args);
        Assert.DoesNotContain(" -i ", args);
        Assert.DoesNotContain("-F", args);
        Assert.Contains("--iglob", args);
        Assert.Contains("*.cs", args);
        Assert.Contains("!.git", args);
    }
}
