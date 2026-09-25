using System.Windows.Threading;

namespace FastDog.Helpers;

/// <summary>
/// UI 线程防抖定时器：连续 Ping 只保留最后一次，静默达到 Delay 后回调一次。
/// 用于文件模式输入即搜——把逐键触发合并为停顿后的一次搜索。
/// 依赖 DispatcherTimer 语义，须在 UI 线程创建与调用。
/// </summary>
public sealed class DebounceTimer
{
    private readonly DispatcherTimer _timer;
    private Action? _callback;

    public DebounceTimer(TimeSpan delay)
    {
        _timer = new DispatcherTimer { Interval = delay };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            _callback?.Invoke();
        };
    }

    /// <summary>重置计时；到期后执行 callback（以最后一次注册的为准）。</summary>
    public void Ping(Action callback)
    {
        _callback = callback;
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>取消挂起的回调（不影响已开始执行的动作）。</summary>
    public void Cancel() => _timer.Stop();
}
