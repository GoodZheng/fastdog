namespace FastDog.Models;

public class SearchHistoryEntry
{
    public string SearchPath { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public bool IsRegex { get; set; } = true;
    public bool CaseSensitive { get; set; }
    public bool WholeWord { get; set; }
    public string FileFilter { get; set; } = string.Empty;
    public string ExcludeDirs { get; set; } = string.Empty;
    public bool DateFilterEnabled { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public long SearchedFiles { get; set; }
    public int FoundFiles { get; set; }
    public int TotalMatches { get; set; }
    public string ElapsedTime { get; set; } = string.Empty;

    public DateTime SearchedAt { get; set; } = DateTime.Now;

    public string DedupKey => $"{SearchText}|{SearchPath}|{IsRegex}|{CaseSensitive}|{WholeWord}|{FileFilter}|{ExcludeDirs}";

    public string OptionsSummary
    {
        get
        {
            var parts = new List<string>();
            parts.Add(IsRegex ? "正则" : "文本");
            if (CaseSensitive) parts.Add("区分大小写");
            if (WholeWord) parts.Add("全词");
            if (!string.IsNullOrEmpty(FileFilter)) parts.Add(FileFilter);
            if (!string.IsNullOrEmpty(ExcludeDirs) && ExcludeDirs != "bin;obj")
                parts.Add($"排除: {ExcludeDirs}");
            return string.Join(", ", parts);
        }
    }

    public string ResultSummary => $"搜索 {SearchedFiles:N0} 文件，{FoundFiles} 命中，{TotalMatches} 匹配";
}