namespace FastDog.Models;

public class MatchLine
{
    public int LineNumber { get; set; }
    public string LineText { get; set; } = string.Empty;
    public int MatchStart { get; set; }
    public int MatchEnd { get; set; }

    // AvalonEdit 全局偏移（由 FilePreviewService.ComputeGlobalOffset 填充）
    public int GlobalMatchStart { get; set; }
    public int GlobalMatchEnd { get; set; }
}