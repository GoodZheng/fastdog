using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FastDog.Models;
using FastDog.ViewModels;
using FastDog.Services;
using FastDog.Helpers;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;

namespace FastDog;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;
    private TextMarkerService? _markerService;
    private readonly LayoutConfigService _layoutService = new();
    // 托盘退出时置 true，使 OnClosing 放行真正关闭；否则点 X 仅隐藏到托盘
    private bool _forceClose;

    /// <summary>
    /// 当前应用版本号（取自程序集 Version，与 csproj 的 &lt;Version&gt; 同源）。
    /// 标题栏绑定此属性，避免发版时人工改 XAML 硬编码字符串而遗漏。
    /// </summary>
    public string AppVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    // 最大化/还原图标几何
    private static readonly Geometry MaximizeGeom =
        Geometry.Parse("M1,1 L10,1 L10,10 L1,10 Z");
    private static readonly Geometry RestoreGeom =
        Geometry.Parse("M1,3 L1,1 L8,1 L8,3 M1,3 L1,10 L8,10 L8,3 M3,1 L3,3 M8,3 L3,3 M3,3 L3,10 M10,5 L10,10 L3,10");

    public MainWindow()
    {
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

        var config = _layoutService.Load();
        if (config is null) return;

        // 检查窗口是否在可见屏幕范围内
        if (config.Left + config.Width < 0 ||
            config.Top + config.Height < 0 ||
            config.Left > SystemParameters.VirtualScreenWidth ||
            config.Top > SystemParameters.VirtualScreenHeight)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        Left = config.Left;
        Top = config.Top;
        Width = config.Width;
        Height = config.Height;
        if (config.WindowState == (int)System.Windows.WindowState.Maximized)
            WindowState = System.Windows.WindowState.Maximized;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        _vm = DataContext as MainViewModel;
        if (_vm is null) return;

        // 绑定输入历史自动补全控制器（搜索路径 / 搜索内容各一个）
        SetupInputHistoryControllers();

        var editor = FindEditor();
        if (editor is null) return;

        _vm.PropertyChanged += (s, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(MainViewModel.FileContent):
                    LoadFileContent(editor, _vm);
                    break;
                case nameof(MainViewModel.FilePath):
                    ApplySyntaxHighlighting(editor, _vm.FilePath);
                    break;
            }
        };

        _vm.ScrollToLineRequested += lineNumber =>
        {
            Dispatcher.Invoke(() =>
            {
                if (lineNumber >= 1 && lineNumber <= editor.Document.LineCount)
                    editor.ScrollTo(lineNumber, 1);
            });
        };

        // 恢复 Grid 分割比例（延迟执行，等待布局完成）
        var layoutConfig = _layoutService.Load();
        if (layoutConfig is not null)
        {
            Dispatcher.BeginInvoke(() => ApplySplitRatios(layoutConfig));
        }
    }

    private void LoadFileContent(TextEditor editor, MainViewModel vm)
    {
        ClearMarkers(editor);

        if (string.IsNullOrEmpty(vm.FileContent))
        {
            editor.Document = new TextDocument();
            return;
        }

        editor.Document = new TextDocument(vm.FileContent);
        Dispatcher.BeginInvoke(() => ApplyMatchMarkers(editor, vm));
    }

    private void ApplyMatchMarkers(TextEditor editor, MainViewModel vm)
    {
        if (vm.SelectedResult is null) return;

        var textArea = editor.TextArea;
        _markerService = new TextMarkerService(textArea);
        textArea.TextView.BackgroundRenderers.Add(_markerService);
        textArea.TextView.LineTransformers.Add(_markerService);

        foreach (var match in vm.SelectedResult.Matches)
        {
            if (match.GlobalMatchStart < 0 || match.GlobalMatchEnd <= match.GlobalMatchStart)
                continue;
            if (match.GlobalMatchEnd > editor.Document.TextLength)
                continue;

            _markerService.Create(match.GlobalMatchStart,
                match.GlobalMatchEnd - match.GlobalMatchStart);
        }
    }

    private void ClearMarkers(TextEditor editor)
    {
        if (_markerService is null) return;

        editor.TextArea.TextView.BackgroundRenderers.Remove(_markerService);
        editor.TextArea.TextView.LineTransformers.Remove(_markerService);
        _markerService = null;
    }

    private static void ApplySyntaxHighlighting(TextEditor editor, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        var ext = System.IO.Path.GetExtension(filePath);
        var highlighting = HighlightingManager.Instance.GetDefinitionByExtension(ext);
        editor.SyntaxHighlighting = highlighting;
    }

    private TextEditor? FindEditor()
    {
        return this.FindName("PreviewEditor") as TextEditor;
    }

    /// <summary>
    /// 实例化两个输入历史自动补全控制器，分别绑定搜索路径 / 搜索内容输入框。
    /// 控件在 XAML 中通过 x:Name 命名，这里按名取出再交给控制器接管交互。
    /// </summary>
    private void SetupInputHistoryControllers()
    {
        if (FindName("SearchPathTextBox") is TextBox pathBox &&
            FindName("SearchPathPopup") is Popup pathPopup &&
            FindName("SearchPathList") is ListBox pathList &&
            DataContext is MainViewModel vm)
        {
            _ = new InputHistoryPopupController(pathBox, pathPopup, pathList,
                value => vm.SearchPath = value);
        }

        if (FindName("SearchTextTextBox") is TextBox textBox &&
            FindName("SearchTextPopup") is Popup textPopup &&
            FindName("SearchTextList") is ListBox textList &&
            DataContext is MainViewModel vm2)
        {
            _ = new InputHistoryPopupController(textBox, textPopup, textList,
                value => vm2.SearchText = value);
        }
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 只有双击数据行才打开文件，忽略表头/空白处
        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is null) return;

        if (DataContext is MainViewModel vm)
            vm.OpenFileCommand.Execute(null);
    }

    private void MatchList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 只有双击匹配行才跳转，忽略空白处
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is null) return;

        if (DataContext is MainViewModel vm)
            vm.OpenFileAtLineCommand.Execute(null);
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null && element is not T)
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        return element as T;
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files.Length > 0 && System.IO.Directory.Exists(files[0]))
            {
                if (DataContext is MainViewModel vm)
                    vm.SearchPath = files[0];
            }
        }
    }

    private void TabResults_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsHistoryTab = false;
    }

    private void TabHistory_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsHistoryTab = true;
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // 点击窗口空白处时，将焦点移到 Window 自身，使 TextBox 失去焦点并收回
        if (e.OriginalSource is not System.Windows.Controls.TextBox)
            FocusManager.SetFocusedElement(this, this);
    }

    // ==================== 自定义标题栏按钮 ====================

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        // 最大化时切换图标为「还原」
        if (MaximizeIcon is not null)
        {
            MaximizeIcon.Data = WindowState == WindowState.Maximized
                ? RestoreGeom
                : MaximizeGeom;
        }
    }

    private void FileFilterButton_Click(object sender, RoutedEventArgs e)
    {
        FileFilterButton.Visibility = Visibility.Collapsed;
        FileFilterTextBox.Visibility = Visibility.Visible;
        FileFilterTextBox.Focus();
        FileFilterTextBox.SelectAll();
    }

    private void FileFilterTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        FileFilterButton.Visibility = Visibility.Visible;
        FileFilterTextBox.Visibility = Visibility.Collapsed;
    }

    private void FileFilterTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FileFilterButton.Visibility = Visibility.Visible;
            FileFilterTextBox.Visibility = Visibility.Collapsed;
        }
    }

    private void ExcludeButton_Click(object sender, RoutedEventArgs e)
    {
        ExcludeButton.Visibility = Visibility.Collapsed;
        ExcludeTextBox.Visibility = Visibility.Visible;
        ExcludeTextBox.Focus();
        ExcludeTextBox.SelectAll();
    }

    private void ExcludeTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ExcludeButton.Visibility = Visibility.Visible;
        ExcludeTextBox.Visibility = Visibility.Collapsed;
    }

    private void ExcludeTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExcludeButton.Visibility = Visibility.Visible;
            ExcludeTextBox.Visibility = Visibility.Collapsed;
        }
    }

    private void HistoryCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SearchHistoryEntry entry)
        {
            if (DataContext is MainViewModel vm)
                vm.UseHistoryEntryCommand.Execute(entry);
        }
    }

    private void Splitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        SaveLayout();
    }

    private void ApplySplitRatios(LayoutConfig config)
    {
        // 上下分割：Row 0 vs Row 2
        var rowDefs = ResultsView.RowDefinitions;
        if (rowDefs.Count >= 3)
        {
            rowDefs[0].Height = new GridLength(config.VerticalSplitRatio, GridUnitType.Star);
            rowDefs[2].Height = new GridLength(1.0 - config.VerticalSplitRatio, GridUnitType.Star);
        }

        // 左右分割：Column 0 vs Column 2
        var colDefs = BottomPanelGrid.ColumnDefinitions;
        if (colDefs.Count >= 3)
        {
            colDefs[0].Width = new GridLength(config.HorizontalSplitRatio, GridUnitType.Star);
            colDefs[2].Width = new GridLength(1.0 - config.HorizontalSplitRatio, GridUnitType.Star);
        }
    }

    private LayoutConfig CaptureCurrentLayout()
    {
        var state = WindowState;
        // 最大化/最小化时使用 RestoreBounds 获取 Normal 状态下的位置
        double left, top, width, height;
        if (state == System.Windows.WindowState.Normal)
        {
            left = Left;
            top = Top;
            width = Width;
            height = Height;
        }
        else
        {
            left = RestoreBounds.Left;
            top = RestoreBounds.Top;
            width = RestoreBounds.Width;
            height = RestoreBounds.Height;
        }

        var rowDefs = ResultsView.RowDefinitions;
        var colDefs = BottomPanelGrid.ColumnDefinitions;

        double verticalRatio = 0.4;
        double horizontalRatio = 0.35;

        if (rowDefs.Count >= 3 &&
            rowDefs[0].Height.GridUnitType == GridUnitType.Star &&
            rowDefs[2].Height.GridUnitType == GridUnitType.Star)
        {
            var total = rowDefs[0].Height.Value + rowDefs[2].Height.Value;
            if (total > 0)
                verticalRatio = rowDefs[0].Height.Value / total;
        }

        if (colDefs.Count >= 3 &&
            colDefs[0].Width.GridUnitType == GridUnitType.Star &&
            colDefs[2].Width.GridUnitType == GridUnitType.Star)
        {
            var total = colDefs[0].Width.Value + colDefs[2].Width.Value;
            if (total > 0)
                horizontalRatio = colDefs[0].Width.Value / total;
        }

        return new LayoutConfig
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            WindowState = (int)state,
            VerticalSplitRatio = verticalRatio,
            HorizontalSplitRatio = horizontalRatio
        };
    }

    private void SaveLayout()
    {
        try
        {
            var config = CaptureCurrentLayout();
            _layoutService.Save(config);
        }
        catch
        {
            // 静默失败，布局保存不应影响正常使用
        }
    }

    /// <summary>
    /// 由托盘「退出」菜单调用：置标志后真正关闭，触发 OnClosed 持久化会话/布局。
    /// </summary>
    public void Quit()
    {
        _forceClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 非强制关闭（用户点 X 或 Alt+F4）→ 隐藏到托盘而非退出进程
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SaveSession();
        SaveLayout();
        base.OnClosed(e);
    }
}
