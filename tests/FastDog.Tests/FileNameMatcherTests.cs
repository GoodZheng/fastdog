using FastDog.Services;
using Xunit;

namespace FastDog.Tests;

public class FileNameMatcherTests
{
    [Theory]
    [InlineData("", "anything.cs", true)]
    [InlineData("   ", "anything.cs", true)]      // 空白模式视为匹配全部
    [InlineData("main", "MainViewModel.cs", true)]
    [InlineData("main", "App.xaml", false)]
    [InlineData("MAIN", "MainViewModel.cs", true)] // 默认不区分大小写
    [InlineData("View", "MainViewModel.cs", true)] // 子串包含（非前缀）
    public void IsMatch_Substring(string pattern, string fileName, bool expected)
    {
        var matcher = new FileNameMatcher(pattern, caseSensitive: false);
        Assert.Equal(expected, matcher.IsMatch(fileName));
    }

    [Theory]
    [InlineData("main*", "MainViewModel.cs", true)]
    [InlineData("main*", "AppMain.cs", false)]      // 通配符整名锚定：* 不含"前缀中出现"的语义
    [InlineData("app*.cs", "AppMain.cs", true)]
    [InlineData("*.cs", "Program.cs", true)]
    [InlineData("*.cs", "Program.xaml", false)]
    [InlineData("main*el.cs", "MainViewModel.cs", true)]  // * 跨中间任意字符
    [InlineData("main*vm.cs", "MainViewModel.cs", false)] // 结尾不匹配（...odel.cs）
    [InlineData("file?.txt", "file1.txt", true)]
    [InlineData("file?.txt", "file10.txt", false)]  // ? 恰好一个字符
    [InlineData("*.cs", "Program.CS", true)]        // 通配符默认也不区分大小写
    public void IsMatch_Wildcard(string pattern, string fileName, bool expected)
    {
        var matcher = new FileNameMatcher(pattern, caseSensitive: false);
        Assert.Equal(expected, matcher.IsMatch(fileName));
    }

    [Fact]
    public void IsMatch_WildcardSpecialCharsEscaped()
    {
        // 模式中的正则元字符（点号）应按字面量匹配
        var matcher = new FileNameMatcher("v1.0.txt", caseSensitive: false);
        Assert.True(matcher.IsMatch("v1.0.txt"));
        Assert.False(matcher.IsMatch("v1x0.txt"));
    }

    [Fact]
    public void IsMatch_CaseSensitive()
    {
        var matcher = new FileNameMatcher("Main", caseSensitive: true);
        Assert.True(matcher.IsMatch("MainViewModel.cs"));
        Assert.False(matcher.IsMatch("mainViewModel.cs"));

        var wildcard = new FileNameMatcher("*.CS", caseSensitive: true);
        Assert.True(wildcard.IsMatch("a.CS"));
        Assert.False(wildcard.IsMatch("a.cs"));
    }

    [Fact]
    public void IsMatch_WildcardAnchoredToWholeName()
    {
        // 通配符是整名匹配而非子串：vm.cs 不应命中 "vm.cs" 以外的前后缀组合
        var matcher = new FileNameMatcher("vm.cs", caseSensitive: false);
        Assert.True(matcher.IsMatch("vm.cs"));
        Assert.False(matcher.IsMatch("MainViewModel.cs"));
    }

    [Theory]
    [InlineData("a*b*c", "abc")]
    [InlineData("a*b*c", "aXbYc")]
    [InlineData("a?c", "abc")]
    public void WildcardToRegex_ProducesAnchoredRegex(string pattern, string shouldMatch)
    {
        var regex = new System.Text.RegularExpressions.Regex(
            FileNameMatcher.WildcardToRegex(pattern));
        Assert.Matches(regex, shouldMatch);
    }
}
