namespace FastDog.Models;

public class SearchQuery
{
    public string SearchPath { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public bool IsRegex { get; set; } = true;
    public bool CaseSensitive { get; set; } = false;
    public bool WholeWord { get; set; } = false;
    public string FileFilter { get; set; } = string.Empty;
    public string ExcludeDirs { get; set; } = string.Empty;
    /// <summary>隐藏目录白名单：分号分隔的 "." 开头目录名（如 ".config;.vscode"），这些目录会被搜索，其余点目录仍跳过。</summary>
    public string HiddenWhitelist { get; set; } = string.Empty;
    public bool DateFilterEnabled { get; set; } = false;
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}