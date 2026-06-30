using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using FastDog.Services;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;

namespace FastDog;

public partial class App : System.Windows.Application
{
    // 单实例：全局互斥量（跨会话，所有用户共享同一 Mutex）
    private const string MutexName = "Global\\FastDog_SingleInstance_Mutex";
    // 单实例：用于新进程通知已有实例"把窗口激活"的命名事件
    private const string EventName = "Global\\FastDog_ShowWindow_Event";

    private Mutex? _mutex;
    private EventWaitHandle? _showWindowEvent;
    // 标记当前进程是否为首次启动的实例（拥有 Mutex 的即为首次）
    private bool _isFirstInstance;

    private MainWindow? _mainWindow;
    private Forms.NotifyIcon? _notifyIcon;
    private readonly UpdateService _updateService = new();

    /// <summary>
    /// 把 <see cref="Application.Current"/> 转型为 <see cref="App"/>，
    /// 便于子窗口（如 <see cref="AboutWindow"/>）调用 <see cref="CheckForUpdateAsync"/>
    /// 等方法，避免每处都写转型。
    /// </summary>
    public new static App Current => (App)System.Windows.Application.Current;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetDpiForSystem();

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    // 定位光标物理坐标所在的显示器（混合 DPI 多屏下按所在屏 DPI 换算）
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    // 取指定显示器的有效 DPI（per-monitor API，需进程 DPI 感知才返回真实值）
    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const int MDT_EFFECTIVE_DPI = 0;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // —— 单实例检测 ——
        // 尝试获取全局 Mutex；createdNew=true 表示当前进程是首次启动的实例
        _mutex = new Mutex(true, MutexName, out _isFirstInstance);

        if (!_isFirstInstance)
        {
            // 已有实例正在运行：通过命名事件通知它激活窗口
            try
            {
                using var ev = EventWaitHandle.OpenExisting(EventName);
                ev.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // 已有实例刚启动还未创建事件，忽略——它马上就会创建
            }
            // 新进程直接退出，让用户继续使用已有窗口
            Shutdown();
            return;
        }

        // 首次实例：创建命名事件，供后续重复启动的进程触发
        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        // 后台线程持续监听该事件，被触发后把主窗口激活到前台
        var waitThread = new Thread(() =>
        {
            while (_showWindowEvent?.WaitOne(Timeout.Infinite) == true)
            {
                Dispatcher.BeginInvoke(new Action(ShowMainWindow));
            }
        })
        {
            IsBackground = true,
            Name = "SingleInstance-Listener"
        };
        waitThread.Start();

        // 主窗口由代码手动创建（已移除 StartupUri），ShutdownMode=OnExplicitShutdown
        // 保证主窗口隐藏到托盘时进程不会退出。
        _mainWindow = new MainWindow();
        _mainWindow.Show();

        InitTrayIcon();
    }

    /// <summary>
    /// 创建系统托盘图标：复用 Assets/newLogo1.2.ico，双击恢复窗口；
    /// 右键弹出 <see cref="TrayMenuWindow"/>（WPF 圆角菜单，规避 WinForms
    /// Region 裁剪的锯齿）。
    /// </summary>
    private void InitTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "FastDog",
            Visible = true,
            Icon = LoadTrayIcon()
        };

        _notifyIcon.MouseUp += (_, e) =>
        {
            // 左键单击 → 直接显示主窗口（更符合常见托盘图标交互习惯）
            // 右键单击 → 弹出「显示 / 退出」菜单
            if (e.Button == Forms.MouseButtons.Left)
            {
                ShowMainWindow();
            }
            else if (e.Button == Forms.MouseButtons.Right)
            {
                ShowTrayMenu();
            }
        };
    }

    /// <summary>
    /// 在系统托盘图标处弹出 WPF 托盘菜单窗口。鼠标物理坐标用【光标所在显示器】的
    /// 有效 DPI 换算成 DIP——混合 DPI 多屏下若用系统（主屏）DPI 换算副屏坐标，
    /// 菜单会整体偏移。菜单位置计算见 <see cref="TrayMenuWindow.ShowAt"/>。
    /// </summary>
    private void ShowTrayMenu()
    {
        // WinForms 物理坐标（屏幕坐标系，物理像素）
        var cursor = Forms.Cursor.Position;

        // 像素 → DIP：用光标所在屏的 DPI，混合 DPI 多屏下才不偏移
        var dpiFactor = GetDpiAtCursor(cursor) / 96.0;
        var point = new Point(cursor.X / dpiFactor, cursor.Y / dpiFactor);

        var menu = new TrayMenuWindow
        {
            OnShow = ShowMainWindow,
            OnCheckUpdate = () => _ = CheckForUpdateAsync(),
            OnAbout = ShowAbout,
            OnExit = QuitApplication
        };
        menu.ShowAt(point);
    }

    /// <summary>
    /// 取光标物理坐标所在显示器的有效 DPI。
    /// <see cref="MonitorFromPoint"/> 定位到光标所在屏，<see cref="GetDpiForMonitor"/>
    /// 取该屏 DPI——避免用系统（主屏）DPI 换算副屏坐标导致的整体偏移。
    /// 取不到（极旧系统）时回退系统 DPI，保证不崩。
    /// </summary>
    private static uint GetDpiAtCursor(System.Drawing.Point cursor)
    {
        var pt = new POINT { X = cursor.X, Y = cursor.Y };
        var hmon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        if (hmon != IntPtr.Zero &&
            GetDpiForMonitor(hmon, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0) // S_OK
        {
            return dpiX;
        }
        return GetDpiForSystem();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        // Assets/newLogo1.2.ico 已在 csproj 声明为 <Resource>
        var uri = new Uri("pack://application:,,,/Assets/newLogo1.2.ico");
        using var stream = GetResourceStream(uri)?.Stream;
        return stream is not null
            ? new System.Drawing.Icon(stream)
            : System.Drawing.SystemIcons.Application;
    }

    /// <summary>
    /// 从托盘恢复并激活主窗口。
    /// </summary>
    public void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    /// <summary>
    /// 显示「关于」窗口，居中屏幕。
    /// 不设 Owner，避免 WPF 把已隐藏到托盘的主窗口一起带出来。
    /// </summary>
    public void ShowAbout()
    {
        var about = new AboutWindow
        {
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
        };
        about.ShowDialog();
    }

    /// <summary>
    /// 检查更新：调用 GitHub API 比对版本，发现新版本则提示并下载安装包，
    /// 已是最新则提示。网络失败时显示错误信息。
    /// </summary>
    public async Task CheckForUpdateAsync()
    {
        try
        {
            var info = await _updateService.CheckAsync();
            if (!info.HasUpdate)
            {
                MessageDialog.ShowInfo(
                    $"当前已是最新版本 v{UpdateService.LocalVersionString}",
                    "检查更新");
                return;
            }

            var latestVer = info.LatestVersion?.ToString(3) ?? info.RawTagName;
            var result = MessageDialog.ShowYesNo(
                $"发现新版本 v{latestVer}\n\n是否立即下载安装包？",
                "检查更新");

            if (!result) return;

            // 下载路径：%TEMP%\FastDog-Setup-{version}.exe
            var tempDir = Path.GetTempPath();
            var fileName = $"FastDog-Setup-{latestVer}.exe";
            var destPath = Path.Combine(tempDir, fileName);

            // 弹出进度窗口
            var title = $"正在下载 FastDog v{latestVer}";
            var progressWin = new UpdateProgressWindow(_updateService, info.DownloadUrl!, destPath, title)
            {
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
            };
            var downloadOk = progressWin.ShowDialog() == true;

            if (downloadOk && File.Exists(destPath))
            {
                // 启动安装包
                try
                {
                    Process.Start(new ProcessStartInfo(destPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageDialog.ShowError($"无法启动安装包：{ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            var logPath = LogError("CheckForUpdateAsync", ex);
            MessageDialog.ShowError(
                $"检查更新失败\n\n{ex.Message}\n\n日志: {logPath}",
                "检查更新");
        }
    }

    /// <summary>
    /// 真正退出：关闭主窗口（触发会话/布局持久化）后调用 Shutdown。
    /// </summary>
    private void QuitApplication()
    {
        _mainWindow?.Quit();
        Shutdown();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _showWindowEvent?.Dispose();
        _showWindowEvent = null;

        // 仅首次实例释放 Mutex（非首次实例在 OnStartup 已退出，此处 _isFirstInstance=false）
        if (_isFirstInstance)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            _mutex = null;
        }

        _updateService.Dispose();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        base.OnExit(e);
    }

    /// <summary>
    /// 记录错误日志到 %LOCALAPPDATA%\FastDog\logs\error.log
    /// </summary>
    private static string LogError(string context, Exception ex)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FastDog", "logs");
            Directory.CreateDirectory(logDir);

            var logPath = Path.Combine(logDir, "error.log");
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var message = $"[{timestamp}] {context}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";

            File.AppendAllText(logPath, message);
            return logPath;
        }
        catch
        {
            return "(日志写入失败)";
        }
    }
}
