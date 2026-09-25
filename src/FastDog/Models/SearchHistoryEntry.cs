namespace FastDog.Models;

public class SearchHistoryEntry
{
    /// <summary>搜索模式："text"（全文搜索）或 "file"（文件名搜索）。旧 JSON 无此字段时反序列化为 null，按文本处理。</summary>
    public string SearchMode { get; set; } = "text";

    public string SearchPath { get; set; } = string.Empty;
    /// <summary>文本模式为搜索内容；文件模式为文件名匹配串。</summary>
    public string SearchText { get; set; } = string.Empty;
    public bool IsRegex { get; set; } = true;
    public bool CaseSensitive { get; set; }
    public bool WholeWord { get; set; }
    public string FileFilter { get; set; } = string.Empty;
    public string ExcludeDirs { get; set; } = string.Empty;
    public string HiddenWhitelist { get; set; } = string.Empty;

    // --- 文件模式专用 ---
    /// <summary>勾选的后缀过滤（";" 连接的小写后缀）。</summary>
    public string FileExtensions { get; set; } = string.Empty;
    public bool IncludeFiles { get; set; } = true;
    public bool IncludeFolders { get; set; } = true;

    public bool DateFilterEnabled { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public long SearchedFiles { get; set; }
    public int FoundFiles { get; set; }
    public int TotalMatches { get; set; }
    public string ElapsedTime { get; set; } = string.Empty;

    public DateTime SearchedAt { get; set; } = DateTime.Now;

    /// <summary>是否文件搜索模式（null 按文本处理，兼容旧历史数据）。</summary>
    public bool IsFileMode => SearchMode == "file";

    public string DedupKey =>
        $"{SearchMode}|{SearchText}|{SearchPath}|{IsRegex}|{CaseSensitive}|{WholeWord}|{FileFilter}|{ExcludeDirs}|{HiddenWhitelist}|{FileExtensions}";

    /// <summary>历史卡片模式标签：文件名 / 正则 / 文本。</summary>
    public string SearchModeLabel => IsFileMode ? "文件名" : (IsRegex ? "正则" : "文本");

    public string OptionsSummary
    {
        get
        {
            var parts = new List<string> { SearchModeLabel };
            if (CaseSensitive) parts.Add("区分大小写");
            if (IsFileMode)
            {
                if (!IncludeFiles) parts.Add("仅文件夹");
                if (!IncludeFolders) parts.Add("仅文件");
                if (!string.IsNullOrEmpty(FileExtensions)) parts.Add($"后缀: {FileExtensions}");
            }
            else
            {
                if (WholeWord) parts.Add("全词");
                if (!string.IsNullOrEmpty(FileFilter)) parts.Add(FileFilter);
            }
            if (!string.IsNullOrEmpty(ExcludeDirs) && ExcludeDirs != "bin;obj")
                parts.Add($"排除: {ExcludeDirs}");
            if (!string.IsNullOrEmpty(HiddenWhitelist))
                parts.Add($"隐藏: {HiddenWhitelist}");
            return string.Join(", ", parts);
        }
    }

    public string ResultSummary => IsFileMode
        ? $"{SearchedFiles:N0} 项：{FoundFiles} 文件 / {TotalMatches} 文件夹"
        : $"搜索 {SearchedFiles:N0} 文件，{FoundFiles} 命中，{TotalMatches} 匹配";

    /// <summary>历史卡片统计标签（随模式变化）。</summary>
    public string LabelA => IsFileMode ? " 项：" : " 文件";
    public string LabelB => IsFileMode ? " 文件" : " 命中";
    public string LabelC => IsFileMode ? " 文件夹" : " 匹配";
}