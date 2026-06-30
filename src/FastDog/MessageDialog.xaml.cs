using System.Windows;
using FastDog.Helpers;

namespace FastDog;

/// <summary>
/// 统一风格的消息对话框，替代系统 MessageBox。
/// 视觉风格与主窗口保持一致（自定义标题栏 + 相同配色）。
/// </summary>
public partial class MessageDialog : Window
{
    public MessageDialog(string message, string title = "提示")
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // 应用 Win11 原生圆角
        if (DwmHelper.SupportsNativeCorners())
        {
            DwmHelper.SetWindowCornerPreference(this);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnButtonOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnButtonCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>显示信息弹窗，返回 true。</summary>
    public static bool ShowInfo(string message, string title = "提示", Window? owner = null)
        => Show(message, title, "ℹ️", MessageBoxButton.OK, owner);

    /// <summary>显示错误弹窗，返回 true。</summary>
    public static bool ShowError(string message, string title = "错误", Window? owner = null)
        => Show(message, title, "⚠️", MessageBoxButton.OK, owner);

    /// <summary>显示 Yes/No 弹窗，返回 true 表示 Yes。</summary>
    public static bool ShowYesNo(string message, string title = "确认", Window? owner = null)
        => Show(message, title, "❓", MessageBoxButton.YesNo, owner);

    private static bool Show(string message, string title, string icon,
        MessageBoxButton buttons, Window? owner)
    {
        var dialog = new MessageDialog(message, title);
        dialog.IconText.Text = icon;
        dialog.Owner = owner;
        dialog.WindowStartupLocation = owner is null
            ? WindowStartupLocation.CenterScreen
            : WindowStartupLocation.CenterOwner;

        // 根据按钮类型构建按钮
        if (buttons == MessageBoxButton.OK)
        {
            AddButton(dialog.ButtonPanel, "确定", 80, true, dialog.OnButtonOk, isPrimary: true);
        }
        else if (buttons == MessageBoxButton.YesNo)
        {
            AddButton(dialog.ButtonPanel, "是", 80, true, dialog.OnButtonOk, isPrimary: true);
            AddButton(dialog.ButtonPanel, "否", 80, false, dialog.OnButtonCancel, isPrimary: false);
        }

        dialog.ShowDialog();
        return dialog.DialogResult == true;
    }

    private static void AddButton(System.Windows.Controls.Panel panel, string text, int width,
        bool isDefault, RoutedEventHandler handler, bool isPrimary)
    {
        var btn = new System.Windows.Controls.Button
        {
            Content = text,
            Width = width,
            Height = 28,
            IsDefault = isDefault,
            IsCancel = !isPrimary && !isDefault,
            Style = isPrimary
                ? (System.Windows.Style)System.Windows.Application.Current.FindResource("SearchButton")
                : (System.Windows.Style)System.Windows.Application.Current.FindResource("CancelButton"),
            Margin = panel.Children.Count > 0 ? new Thickness(10, 0, 0, 0) : new Thickness(0)
        };
        btn.Click += handler;
        panel.Children.Add(btn);
    }
}
