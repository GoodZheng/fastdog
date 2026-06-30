using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using FastDog.Helpers;

namespace FastDog;

/// <summary>
/// 「关于」窗口：展示应用信息、版本、技术栈与仓库链接，内置「检查更新」入口。
/// 视觉风格与主窗口保持一致（自定义标题栏 + 关闭按钮）。
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"版本 v{Services.UpdateService.LocalVersionString}";
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

    private void GitHubLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/GoodZheng/fastdog") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageDialog.ShowError($"无法打开浏览器：{ex.Message}");
        }
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        try
        {
            await App.Current.CheckForUpdateAsync();
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void TitleCloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
