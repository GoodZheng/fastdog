using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;

namespace FastDog;

/// <summary>
/// 把「TextBox + Popup + ListBox」三者串成一个输入历史自动补全控件。
/// 行为：聚焦即展开全部历史项；输入时按前缀过滤；↑/↓ 选择、Enter 提交、Esc 关闭；
/// 鼠标点击项即提交并回焦输入框、光标置尾。
/// 一个实例对应一个输入框，搜索路径/搜索内容各建一个，消除重复逻辑。
/// </summary>
public class InputHistoryPopupController
{
    private readonly TextBox _textBox;
    private readonly Popup _popup;
    private readonly ListBox _listBox;
    private readonly Action<string> _onCommit;

    private bool _suppressFilter; // 提交回写时避免再次触发过滤

    public InputHistoryPopupController(TextBox textBox, Popup popup, ListBox listBox, Action<string> onCommit)
    {
        _textBox = textBox;
        _popup = popup;
        _listBox = listBox;
        _onCommit = onCommit;

        _textBox.GotFocus += OnGotFocus;
        _textBox.LostFocus += OnLostFocus;
        _textBox.TextChanged += OnTextChanged;
        _textBox.PreviewKeyDown += OnPreviewKeyDown;
        _listBox.PreviewMouseLeftButtonDown += OnListBoxMouseDown;
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
        UpdatePopupVisibility();
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        // 与既有 Window_MouseDown（点击非 TextBox 收回焦点）配合：失焦即关闭
        _popup.IsOpen = false;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFilter) return;
        if (_textBox.IsFocused)
        {
            ApplyFilter();
            UpdatePopupVisibility();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (_popup.IsOpen && _listBox.HasItems)
                {
                    MoveSelection(+1);
                    e.Handled = true;
                }
                break;
            case Key.Up:
                if (_popup.IsOpen && _listBox.HasItems)
                {
                    MoveSelection(-1);
                    e.Handled = true;
                }
                break;
            case Key.Enter:
                if (_popup.IsOpen && _listBox.HasItems)
                {
                    CommitSelection();
                    e.Handled = true;
                }
                break;
            case Key.Escape:
                if (_popup.IsOpen)
                {
                    _popup.IsOpen = false;
                    e.Handled = true;
                }
                break;
        }
    }

    private void OnListBoxMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 命中哪一项就提交哪一项：从命中元素沿可视化树上溯到 ListBoxItem
        if (e.OriginalSource is DependencyObject source &&
            FindListBoxItem(source) is { } item && item.DataContext is string value)
        {
            CommitValue(value);
            e.Handled = true;
        }
    }

    private void ApplyFilter()
    {
        var view = CollectionViewSource.GetDefaultView(_listBox.ItemsSource);
        if (view is null) return;

        var prefix = _textBox.Text ?? string.Empty;
        view.Filter = prefix.Length == 0
            ? null
            : obj => obj is string s && s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdatePopupVisibility()
    {
        var view = CollectionViewSource.GetDefaultView(_listBox.ItemsSource);
        var count = view?.OfType<object>().Count() ?? 0;
        _popup.IsOpen = count > 0;
    }

    private void MoveSelection(int delta)
    {
        var count = _listBox.Items.Count;
        if (count == 0) return;

        var index = _listBox.SelectedIndex;
        index = index < 0 ? (delta > 0 ? 0 : count - 1) : index + delta;
        index = Math.Clamp(index, 0, count - 1);
        _listBox.SelectedIndex = index;
        _listBox.ScrollIntoView(_listBox.SelectedItem);
    }

    private void CommitSelection()
    {
        if (_listBox.SelectedItem is string value)
            CommitValue(value);
        else if (_listBox.Items.Count > 0 && _listBox.Items[0] is string first)
            CommitValue(first);
    }

    private void CommitValue(string value)
    {
        _popup.IsOpen = false;
        _suppressFilter = true;
        try
        {
            _onCommit(value);
            _textBox.Text = value;
            _textBox.CaretIndex = _textBox.Text.Length;
            _textBox.Focus();
        }
        finally
        {
            _suppressFilter = false;
        }
    }

    /// <summary>
    /// 从命中元素沿可视化树上溯到 ListBoxItem（鼠标点击命中时使用）。
    /// </summary>
    private static ListBoxItem? FindListBoxItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListBoxItem lbi)
                return lbi;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }
}
