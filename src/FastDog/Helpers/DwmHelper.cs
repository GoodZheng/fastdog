using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FastDog.Helpers;

/// <summary>
/// DWM (Desktop Window Manager) 辅助类，提供原生窗口效果支持
/// </summary>
public static class DwmHelper
{
    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    [DllImport("kernel32.dll")]
    private static extern uint GetVersion();

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    // DWM Window Attributes
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    // DWM Window Corner Preferences
    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWCP_ROUNDSMALL = 3;

    // DWM System Backdrop Types (Win11 22H2+)
    private const int DWMSBT_MAINWINDOW = 2; // Mica
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
    private const int DWMSBT_TABBEDWINDOW = 4; // Tabbed

    /// <summary>
    /// 获取 Windows 构建号
    /// </summary>
    public static int GetWindowsBuildNumber()
    {
        var version = GetVersion();
        return (int)(version >> 16);
    }

    /// <summary>
    /// 检查是否支持原生圆角（Windows 11 22000+）
    /// </summary>
    public static bool SupportsNativeCorners()
    {
        return GetWindowsBuildNumber() >= 22000;
    }

    /// <summary>
    /// 检查是否支持 Mica 效果（Windows 11 22000+）
    /// </summary>
    public static bool SupportsMica()
    {
        return GetWindowsBuildNumber() >= 22000;
    }

    /// <summary>
    /// 检查是否支持新的 Mica 效果（Windows 11 22621+）
    /// </summary>
    public static bool SupportsMicaAlt()
    {
        return GetWindowsBuildNumber() >= 22621;
    }

    /// <summary>
    /// 为窗口应用原生圆角（Win11）
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="preference">圆角偏好</param>
    /// <returns>是否成功</returns>
    public static bool SetWindowCornerPreference(Window window, int preference = DWMWCP_ROUND)
    {
        if (!SupportsNativeCorners())
            return false;

        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            var result = DwmSetWindowAttribute(
                hwnd,
                DWMWA_WINDOW_CORNER_PREFERENCE,
                ref preference,
                sizeof(int));

            return result == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 为窗口应用 Mica 效果（Win11 22H2+）
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="backdropType">背景类型：2=Mica, 3=Acrylic, 4=Tabbed</param>
    /// <returns>是否成功</returns>
    public static bool SetSystemBackdrop(Window window, int backdropType = DWMSBT_MAINWINDOW)
    {
        if (!SupportsMicaAlt())
            return false;

        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            var result = DwmSetWindowAttribute(
                hwnd,
                DWMWA_SYSTEMBACKDROP_TYPE,
                ref backdropType,
                sizeof(int));

            return result == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 移除窗口原生圆角
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <returns>是否成功</returns>
    public static bool RemoveWindowCorner(Window window)
    {
        return SetWindowCornerPreference(window, DWMWCP_DEFAULT);
    }

    /// <summary>
    /// 移除窗口 Mica 效果
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <returns>是否成功</returns>
    public static bool RemoveSystemBackdrop(Window window)
    {
        if (!SupportsMicaAlt())
            return false;

        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            var backdropType = 0; // DWMSBT_NONE
            var result = DwmSetWindowAttribute(
                hwnd,
                DWMWA_SYSTEMBACKDROP_TYPE,
                ref backdropType,
                sizeof(int));

            return result == 0;
        }
        catch
        {
            return false;
        }
    }
}
