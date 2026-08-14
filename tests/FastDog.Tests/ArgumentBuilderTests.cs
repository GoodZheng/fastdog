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
        Assert.Contains(@"E:\code", args);   // 路径出现在参数中（无空格时不加引号）
        Assert.Contains("class", args);       // 搜索词出现在参数中
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

    [Theory]
    [InlineData(".cs", "*.cs")]      // 扩展名点号写法 → 补星号
    [InlineData("cs", "*.cs")]       // 纯扩展名无点 → 补星号和点
    [InlineData("*.cs", "*.cs")]     // 已是完整 glob → 原样
    [InlineData("Program.cs", "Program.cs")] // 含点的文件名 → 原样
    public void BuildArguments_FileFilter_NormalizesExtensionToGlob(string input, string expected)
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "test",
            FileFilter = input
        };
        var args = RipgrepBridge.BuildArguments(query);
        // 归一化后的 glob 必须出现在参数中，原样输入不应误把 ".cs" 当成完整 glob
        Assert.Contains($"--iglob {expected}", args);
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
    public void BuildArguments_HiddenWhitelist_EnablesHiddenAndGlobs()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "test",
            HiddenWhitelist = ".config;.vscode"
        };
        var args = RipgrepBridge.BuildArguments(query);
        // 白名单非空时必须开 --hidden，否则点目录在遍历阶段被剪枝，正向 glob 也搜不到
        Assert.Contains("--hidden", args);
        // 无 FileFilter 时用 '*' 拉回普通文件（避免 include 模式排除普通文件）
        Assert.Contains("--glob *", args);
        // 兜底：排除所有 "." 开头路径
        Assert.Contains("--glob !.*", args);
        // 白名单正向 glob：每个目录两条（dir 覆盖剪枝 + dir/** 包含内容）
        Assert.Contains("--glob .config", args);
        Assert.Contains("--glob .config/**", args);
        Assert.Contains("--glob .vscode", args);
        Assert.Contains("--glob .vscode/**", args);
        // 顺序：兜底 !.* 必须在白名单正向 glob 之前
        // （ripgrep glob 遵循 later overrides earlier，正向 glob 在后才能覆盖否定）
        Assert.True(args.IndexOf("--glob !.*") < args.IndexOf("--glob .config"),
            "兜底 !.* 必须在白名单正向 glob 之前，否则白名单会被否定覆盖而失效");
    }

    [Fact]
    public void BuildArguments_NoWhitelist_KeepsDefaultBehavior()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "test"
        };
        var args = RipgrepBridge.BuildArguments(query);
        // 白名单为空时不应改变默认行为：不开 hidden、不加兜底 !.*
        Assert.DoesNotContain("--hidden", args);
        Assert.DoesNotContain("!.*", args);
        Assert.DoesNotContain("--glob *", args);
        // .git 硬排除保留
        Assert.Contains("!.git", args);
    }

    [Fact]
    public void BuildFileListArguments_HiddenWhitelist()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "test",
            HiddenWhitelist = ".config"
        };
        var args = RipgrepBridge.BuildFileListArguments(query);
        Assert.Contains("--files", args);
        Assert.Contains("--hidden", args);
        Assert.Contains("--glob *", args);
        Assert.Contains("--glob .config", args);
        Assert.Contains("--glob .config/**", args);
        Assert.True(args.IndexOf("--glob !.*") < args.IndexOf("--glob .config"));
    }

    [Fact]
    public void BuildArguments_HiddenWhitelist_WithFileFilter_NoStarGlob()
    {
        // FileFilter 非空时由 --iglob 承担 include，不应再加 '*'（否则会破坏文件过滤）
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "test",
            FileFilter = "*.md",
            HiddenWhitelist = ".config"
        };
        var args = RipgrepBridge.BuildArguments(query);
        Assert.Contains("--hidden", args);
        Assert.DoesNotContain("--glob *", args);
        Assert.Contains("--iglob *.md", args);
        Assert.Contains("--glob .config", args);
        Assert.Contains("--glob .config/**", args);
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

    [Fact]
    public void BuildArguments_EscapesDoubleQuotesInSearchText()
    {
        // 复现 bug：搜索词含双引号（如 C# 内插字符串）时，
        // 不转义会导致命令行被从中间截断，rg 匹配失败。
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = @"Error($""添加失败：{ex.Message}"")",
            IsRegex = false
        };
        var args = RipgrepBridge.BuildArguments(query);
        // 内部双引号必须被转义为 \"，否则会破坏命令行
        // 完整搜索词被包在引号内，形如 "Error($\"添加失败：{ex.Message}\")"
        Assert.Contains(@"Error($\""添加失败", args);
        Assert.Contains(@"{ex.Message}\"")", args);
    }

    [Fact]
    public void BuildArguments_QuotesSearchTextWithSpaces()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "hello world",
            IsRegex = false
        };
        var args = RipgrepBridge.BuildArguments(query);
        // 含空格的搜索词必须用双引号整体包裹
        Assert.Contains(@"""hello world""", args);
    }

    [Fact]
    public void BuildArguments_DoesNotQuoteSimpleSearchText()
    {
        var query = new SearchQuery
        {
            SearchPath = @"E:\code",
            SearchText = "class",
            IsRegex = true
        };
        var args = RipgrepBridge.BuildArguments(query);
        // 无空格/特殊字符的搜索词不需引号，但应作为独立 token 出现
        Assert.Contains(" class ", args);
        Assert.DoesNotContain(@"""class""", args);
    }
}
