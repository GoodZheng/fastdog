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
    public bool DateFilterEnabled { get; set; } = false;
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}