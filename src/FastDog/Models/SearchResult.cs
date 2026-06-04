using System.IO;

namespace FastDog.Models;

public class SearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public string RelativePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int MatchCount { get; set; }
    public DateTime LastModified { get; set; }
    public List<MatchLine> Matches { get; set; } = [];
}