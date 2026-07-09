using System;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Search;

namespace FastDog.Helpers;

/// <summary>
/// 预览内查找栏的"N / M"计数器。
/// <para>SearchPanel 内部的匹配结果集合（renderer.CurrentResults）为 internal，外部访问不到，
/// 故用公开 API <see cref="SearchStrategyFactory"/> 自行重算匹配数与当前序号。</para>
/// <para>订阅三类信号：搜索词/选项变化、FindNext/Prev 跳转后 caret 移动、切换文件。
/// 通过回调回传 "N / M" 字符串；无匹配或面板关闭时回传 null（由调用方隐藏标签）。</para>
/// </summary>
public sealed class PreviewFindCounter : IDisposable
{
    private readonly SearchPanel _panel;
    private readonly TextArea _area;
    private readonly Action<string?> _update;

    /// <summary>当前匹配的起始 offset 升序列表（每次重算时重建）。</summary>
    private List<int> _matchStarts = new();

    private bool _disposed;

    public PreviewFindCounter(SearchPanel panel, TextArea area, Action<string?> update)
    {
        _panel = panel;
        _area = area;
        _update = update;
    }

    /// <summary>订阅事件。调用方负责在窗口关闭时 <see cref="Dispose"/>。</summary>
    public void Attach()
    {
        _panel.SearchOptionsChanged += Recompute;
        _area.Caret.PositionChanged += UpdateCurrent;
        _area.DocumentChanged += Recompute;
    }

    /// <summary>
    /// 搜索参数或文档变化时，重建匹配列表并刷新计数。
    /// </summary>
    private void Recompute(object? sender, EventArgs e)
    {
        // 面板关闭或无搜索词 → 清空、隐藏
        if (_panel.IsClosed || string.IsNullOrEmpty(_panel.SearchPattern))
        {
            _matchStarts = new List<int>();
            _update(null);
            return;
        }

        _matchStarts = BuildMatchStarts();
        UpdateCurrent(sender, e);
    }

    /// <summary>
    /// caret 移动后（含 FindNext/Prev 跳转），重算当前序号 N。
    /// </summary>
    private void UpdateCurrent(object? sender, EventArgs e)
    {
        if (_panel.IsClosed || _matchStarts.Count == 0)
        {
            _update(null);
            return;
        }

        int m = _matchStarts.Count;
        int n = CurrentIndex(_area.Caret.Offset, _matchStarts);
        _update($"{n}/{m}");
    }

    /// <summary>
    /// 用当前搜索参数构建全部匹配的起始 offset 升序列表。
    /// </summary>
    private List<int> BuildMatchStarts()
    {
        var pattern = _panel.SearchPattern ?? string.Empty;
        var mode = _panel.UseRegex ? SearchMode.RegEx : SearchMode.Normal;
        var starts = new List<int>();

        try
        {
            var strategy = SearchStrategyFactory.Create(
                pattern, !_panel.MatchCase, _panel.WholeWords, mode);
            var doc = _area.Document;
            if (doc is null) return starts;

            var source = new TextDocument(doc.Text);
            foreach (var result in strategy.FindAll(source, 0, source.TextLength))
                starts.Add(result.Offset);
        }
        catch (SearchPatternException)
        {
            // 非法正则 → 视作无匹配
        }

        // FindAll 契约保证升序，这里防御性排序一次
        starts.Sort();
        return starts;
    }

    /// <summary>
    /// 计算当前项序号（1-based）。取第一个 startOffset &gt;= caret.Offset 的匹配；
    /// 若 caret 在所有匹配之后，回绕到第 1 项（对齐 VS Code 行为）。
    /// </summary>
    private static int CurrentIndex(int caretOffset, List<int> matchStarts)
    {
        int idx = matchStarts.BinarySearch(caretOffset);
        if (idx < 0)
            idx = ~idx; // 第一个 >= caretOffset 的位置

        if (idx >= matchStarts.Count)
            idx = 0; // 回绕

        return idx + 1; // 1-based
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _panel.SearchOptionsChanged -= Recompute;
        _area.Caret.PositionChanged -= UpdateCurrent;
        _area.DocumentChanged -= Recompute;
    }
}
