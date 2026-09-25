namespace FastDog.Models;

/// <summary>
/// 文件搜索模式的查询条件。与文本搜索的 SearchQuery 平行，
/// 复用排除目录 / 隐藏目录白名单 / 日期过滤的语义。
/// </summary>
public class FileSearchQuery
{
    public string SearchPath { get; set; } = string.Empty;

    /// <summary>
    /// 文件/文件夹名称匹配串：空 = 全部；含 * 或 ? 时按通配符（整名匹配），
    /// 否则按子串包含。
    /// </summary>
    public string NamePattern { get; set; } = string.Empty;

    /// <summary>名称匹配是否区分大小写（默认不区分，Windows 文件名惯例）。</summary>
    public bool CaseSensitive { get; set; } = false;

    public bool IncludeFiles { get; set; } = true;
    public bool IncludeFolders { get; set; } = true;

    /// <summary>后缀过滤（不含点的小写后缀，多选）；空 = 不过滤。
    /// 由 C# 侧做扩展名匹配（FileSearchService.MatchesExtensions），与 rg 枚举解耦、可被内存缓存复用。</summary>
    public List<string> Extensions { get; set; } = [];

    public string ExcludeDirs { get; set; } = string.Empty;
    public string HiddenWhitelist { get; set; } = string.Empty;
    public bool DateFilterEnabled { get; set; } = false;
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// 查询条件签名：输入即搜用于跳过与上次已执行完全相同的自动搜索
    /// （防选项来回切、Trim 回写触发的冗余重跑）。
    /// </summary>
    public string BuildSignature() =>
        string.Join('\u0001',
            SearchPath,
            NamePattern,
            CaseSensitive ? "1" : "0",
            IncludeFiles ? "1" : "0",
            IncludeFolders ? "1" : "0",
            string.Join(",", Extensions),
            ExcludeDirs,
            HiddenWhitelist,
            DateFilterEnabled ? "1" : "0",
            DateFrom?.Ticks.ToString() ?? string.Empty,
            DateTo?.Ticks.ToString() ?? string.Empty);
}
