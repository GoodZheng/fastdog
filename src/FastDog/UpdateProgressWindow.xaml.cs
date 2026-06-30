using System.IO;
using System.Windows;
using FastDog.Services;
using FastDog.Helpers;

namespace FastDog;

/// <summary>
/// 下载更新安装包的进度窗口。模态显示，下载完成后 DialogResult = true，
/// 失败或取消为 false。
/// </summary>
public partial class UpdateProgressWindow : Window
{
    private readonly UpdateService _service;
    private readonly string _downloadUrl;
    private readonly string _destPath;
    private readonly CancellationTokenSource _cts = new();

    public UpdateProgressWindow(UpdateService service, string downloadUrl, string destPath, string title)
    {
        _service = service;
        _downloadUrl = downloadUrl;
        _destPath = destPath;
        Title = title;
        InitializeComponent();
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

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var progress = new Progress<double>(p => UpdateProgress(p));
            await _service.DownloadAsync(_downloadUrl, _destPath, progress, _cts.Token)
                .ConfigureAwait(true); // 回到 UI 线程完成收尾

            StatusText.Text = "下载完成，正在启动安装包…";
            ProgressDetail.Text = $"已保存到 {Path.GetFileName(_destPath)}";
            DownloadProgress.Value = 100;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "下载已取消";
            ProgressDetail.Text = "";
            DialogResult = false;
        }
        catch (Exception ex)
        {
            MessageDialog.ShowError($"下载失败：{ex.Message}");
            DialogResult = false;
        }
    }

    private void UpdateProgress(double ratio)
    {
        var percent = Math.Clamp(ratio * 100, 0, 100);
        DownloadProgress.Value = percent;
        StatusText.Text = $"正在下载… {percent:0.0}%";

        // 尝试从 URL 的文件名推断已下载大小（UpdateService 不暴露 totalBytes，
        // 这里仅作为 UI 反馈：按百分比估算"总大小"的近似显示；
        // 真实总大小在首次收到 Content-Length 后可用，但本窗口不做精细区分，
        // 仅显示百分比与进度条即可）。
        ProgressDetail.Text = $"{percent:0} %";
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
    }

    private void TitleCloseButton_Click(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts.Dispose();
        base.OnClosed(e);
    }
}
