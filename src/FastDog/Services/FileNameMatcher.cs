using System.Text;
using System.Text.RegularExpressions;

namespace FastDog.Services;

/// <summary>
/// 文件名匹配器（文件搜索模式）：
/// 空模式匹配全部；含 * 或 ? 时按通配符整名匹配；否则按子串包含。
/// 区分大小写由选项控制（默认不区分，Windows 文件名惯例）。
/// </summary>
public sealed class FileNameMatcher
{
    private readonly Regex? _wildcard;
    private readonly string _substring;
    private readonly StringComparison _comparison;

    public FileNameMatcher(string pattern, bool caseSensitive)
    {
        _comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        pattern = pattern?.Trim() ?? string.Empty;

        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            _wildcard = new Regex(
                WildcardToRegex(pattern),
                caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(2));
            _substring = string.Empty;
        }
        else
        {
            _substring = pattern;
        }
    }

    public bool IsMatch(string fileName)
    {
        if (_wildcard is not null)
            return _wildcard.IsMatch(fileName);
        if (_substring.Length == 0)
            return true;
        return fileName.Contains(_substring, _comparison);
    }

    /// <summary>把 * / ? 通配符模式翻译为整名锚定的正则；其余字符按字面量转义。</summary>
    public static string WildcardToRegex(string pattern)
    {
        var sb = new StringBuilder(pattern.Length + 8);
        sb.Append('^');
        foreach (var c in pattern)
        {
            switch (c)
            {
                case '*': sb.Append(".*"); break;
                case '?': sb.Append('.'); break;
                default: sb.Append(Regex.Escape(c.ToString())); break;
            }
        }
        sb.Append('$');
        return sb.ToString();
    }
}
