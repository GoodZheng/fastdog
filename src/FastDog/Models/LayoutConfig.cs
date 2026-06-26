namespace FastDog.Models;

public class LayoutConfig
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 800;
    public int WindowState { get; set; } = 0;  // 0=Normal, 2=Maximized

    // Row0/(Row0+Row2)，默认 2*/5* = 0.4
    public double VerticalSplitRatio { get; set; } = 0.4;

    // Col0/(Col0+Col2)，默认 35*/100* = 0.35
    public double HorizontalSplitRatio { get; set; } = 0.35;
}
