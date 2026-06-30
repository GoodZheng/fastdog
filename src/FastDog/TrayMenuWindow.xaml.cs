using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Border = System.Windows.Controls.Border;
using TextBlock = System.Windows.Controls.TextBlock;
using Application = System.Windows.Application;
using Size = System.Windows.Size;

namespace FastDog;

/// <summary>
/// 系统托盘的右键菜单窗口。用 WPF 矢量渲染绘制圆角，规避 WinForms
/// <see cref="System.Windows.Forms.ContextMenuStrip"/> + Region 裁剪必然产生的锯齿。
/// 风格与主窗口（白底圆角 + 投影 + 蓝色高亮 #0e639c）一致。
/// 失焦（点击别处）自动关闭，点击菜单项后通过回调通知调用方并关闭。
/// </summary>
public partial class TrayMenuWindow : Window
{
    private static readonly Brush Accent = (Brush)Application.Current.FindResource("AccentBrush");

    // 守卫标志：防止 OnDeactivated 中的 Close() 与主动关闭重入。
    // 窗口主动 Close() 时会先失去激活→触发 OnDeactivated→再次 Close()，
    // 对正在关闭的窗口重入调用会抛 InvalidOperationException，导致进程崩溃。
    private bool _isClosing;

    public Action? OnShow { get; init; }
    public Action? OnCheckUpdate { get; init; }
    public Action? OnAbout { get; init; }
    public Action? OnExit { get; init; }

    public TrayMenuWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 在屏幕指定坐标（设备无关像素 DIP，通常为鼠标光标位置）弹出菜单。
    /// 定位策略模拟 Windows 原生托盘菜单：
    /// - 水平：默认右对齐到光标，超出右边界则左对齐；
    /// - 垂直：默认向上展开（菜单底边贴近光标，贴合屏幕底部托盘图标的上方），
    ///   若光标上方空间不足则向下展开。
    /// </summary>
    public void ShowAt(Point screenPoint)
    {
        // 先测量自身尺寸（DesiredSize 在 Measure 后即有效，无需等真实渲染）
        var (width, height) = MeasureSelf();
        var screen = SystemParameters.WorkArea;

        // 水平：默认让菜单右边贴光标（与原生右键菜单右对齐习惯一致），越界则左对齐到光标
        var x = screenPoint.X + width > screen.Right + 1 ? screenPoint.X : screenPoint.X - width;
        // 垂直：默认向上展开，菜单底边贴光标上方；上方空间不足才向下展开
        var y = screenPoint.Y - height < screen.Top - 1 ? screenPoint.Y : screenPoint.Y - height;

        Left = x;
        Top = y;
        Show();
        Activate();
    }

    /// <summary>
    /// 触发一次布局测量，返回菜单自身所需尺寸（宽、高，DIP）。
    /// 用 <see cref="FrameworkElement.DesiredSize"/>（Measure 后即有效），
    /// 而非 ActualWidth/Height（窗口未渲染时仍为 0）。
    /// </summary>
    private (double width, double height) MeasureSelf()
    {
        // ⚠ 不能对 Window 自身 Measure：窗口 Show 之前 WPF 返回 DesiredSize=0×0，
        // 会导致定位时 width/height 取 0，菜单左上角钉在光标、向右下展开到错误位置。
        // 改为测量内容根元素（Show 前内容即可测量）取高度，宽度用 Window 固定 Width。
        if (Content is FrameworkElement fe)
        {
            fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return (Width, fe.DesiredSize.Height);
        }
        return (Width, 0);
    }

    // 悬停高亮：整项填蓝底白字，移出还原
    private void Item_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border b)
        {
            b.Background = Accent;
            if (b.Child is TextBlock tb) tb.Foreground = Brushes.White;
        }
    }

    private void Item_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border b)
        {
            b.Background = Brushes.Transparent;
            if (b.Child is TextBlock tb) tb.Foreground = (Brush)FindResource("GrayTextBrush");
        }
    }

    private void ShowItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // 先关闭菜单，再异步执行回调：避免与目标窗口的 Show() 在同一调用栈
        // 上交互；且 OnDeactivated 会因关闭而触发，回调放其后更稳妥。
        CloseMenu();
        Application.Current.Dispatcher.BeginInvoke(OnShow);
    }

    private void CheckUpdateItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CloseMenu();
        Application.Current.Dispatcher.BeginInvoke(OnCheckUpdate);
    }

    private void AboutItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CloseMenu();
        Application.Current.Dispatcher.BeginInvoke(OnAbout);
    }

    private void ExitItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CloseMenu();
        Application.Current.Dispatcher.BeginInvoke(OnExit);
    }

    // 失焦（点击别处）自动关闭——模拟原生右键菜单行为。
    // 必须有 _isClosing 守卫：主动 Close() 会先触发本方法，否则重入再次
    // Close() 会抛异常导致进程崩溃（表现为鼠标转圈几秒后退出）。
    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        CloseMenu();
    }

    private void CloseMenu()
    {
        if (_isClosing) return;
        _isClosing = true;
        Close();
    }
}
