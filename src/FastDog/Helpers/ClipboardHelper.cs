using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FastDog.Helpers;

/// <summary>
/// 基于 Win32 API 的剪贴板写入，绕过 WPF/WinForms 的 OLE 路径。
///
/// 背景：Clipboard.SetText 底层是 SetDataObject(data, copy:true)，需经 OLE
/// 序列化注册数据对象，当其它进程（输入法、剪贴板工具、浏览器同步等）长时间
/// 持有剪贴板锁时会稳定抛 ExternalException，且内置重试也无济于事。本类直接
/// 把 UTF-16 文本拷进全局内存交给系统（GMEM_MOVEABLE + CF_UNICODETEXT），
/// 不走 OLE，争抢激烈时也能成功，且天然持久化（进程退出后内容仍保留）。
/// </summary>
public static class ClipboardHelper
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint CF_UNICODETEXT = 13;

    /// <summary>
    /// 设置剪贴板文本。先尝试原生 Win32 API（不经过 OLE，对剪贴板锁争抢更鲁棒），
    /// 失败再回退到 WPF Clipboard.SetText。任一步骤成功即返回 true。
    /// </summary>
    /// <param name="text">要写入的文本</param>
    /// <param name="owner">调用方窗口（用于以本窗口句柄打开剪贴板，减少与其它窗口的争抢；可空）</param>
    public static bool TrySetText(string text, Window? owner = null)
    {
        if (text == null) return false;

        // 取窗口句柄：有主窗口时用其 hwnd 调用 OpenClipboard，更符合“所属关系”
        var hwnd = IntPtr.Zero;
        if (owner != null)
            hwnd = new WindowInteropHelper(owner).Handle;
        if (hwnd == IntPtr.Zero && System.Windows.Application.Current?.MainWindow != null)
            hwnd = new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;

        // 重试：OpenClipboard 可能因其它进程短暂占用而失败，给几次机会
        for (int i = 0; i < 5; i++)
        {
            if (TrySetTextNative(text, hwnd))
                return true;
            System.Threading.Thread.Sleep(20);
        }

        // 回退到 WPF（含其内置重试）
        try
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetTextNative(string text, IntPtr hwnd)
    {
        // 字节数 = (字符数 + 结尾 \0) * 2（UTF-16 每字符 2 字节）
        var bytes = (UIntPtr)((text.Length + 1) * 2);

        IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, bytes);
        if (hGlobal == IntPtr.Zero) return false;

        try
        {
            IntPtr p = GlobalLock(hGlobal);
            if (p == IntPtr.Zero) return false;
            try
            {
                Marshal.Copy(text.ToCharArray(), 0, p, text.Length);
                // 写入结尾 \0（两个字节，已由 GlobalAlloc 清零，但显式写更稳妥）
                Marshal.WriteInt16(p + text.Length * 2, 0);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            if (!OpenClipboard(hwnd))
                return false;

            try
            {
                EmptyClipboard();
                // SetClipboardData 成功后会接管 hGlobal 的所有权，调用方不可再释放
                if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                    return false;
                hGlobal = IntPtr.Zero; // 标记已移交，跳过下面的释放
                return true;
            }
            finally
            {
                CloseClipboard();
            }
        }
        finally
        {
            // 仅当 SetClipboardData 未接管所有权时才需要释放
            if (hGlobal != IntPtr.Zero)
                GlobalFree(hGlobal);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
