namespace FastDog.Models;

public class MatchLine
{
    public int LineNumber { get; set; }
    public string LineText { get; set; } = string.Empty;

    /// <summary>用于 UI 显示的文本（去除首尾空白，避免列表中出现大量缩进空白）</summary>
    public string DisplayText { get; set; } = string.Empty;
    public int MatchStart { get; set; }
    public int MatchEnd { get; set; }

    // AvalonEdit 全局偏移（由 FilePreviewService.ComputeGlobalOffset 填充）
    public int GlobalMatchStart { get; set; }
    public int GlobalMatchEnd { get; set; }
}