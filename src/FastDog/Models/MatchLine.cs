namespace FastDog.Models;

public class MatchLine
{
    public int LineNumber { get; set; }
    public string LineText { get; set; } = string.Empty;
    public int MatchStart { get; set; }
    public int MatchEnd { get; set; }
}