using System.IO;

namespace FastDog.Models;

public class SearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public string RelativePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileSizeDisplay => FormatFileSize(FileSize);

    private static string FormatFileSize(long bytes)
    {
        const long KB = 1024;
        if (bytes < KB) return $"{bytes} B";
        if (bytes < KB * 1024) return $"{bytes / (double)KB:N1} KB";
        if (bytes < KB * 1024 * 1024) return $"{bytes / (double)(KB * 1024):N1} MB";
        return $"{bytes / (double)(KB * 1024 * 1024):N2} GB";
    }
    public int MatchCount { get; set; }
    public DateTime LastModified { get; set; }
    public List<MatchLine> Matches { get; set; } = [];
}