using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FastDog.Models;
using FastDog.ViewModels;
using FastDog.Services;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;

namespace FastDog;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;
    private TextMarkerService? _markerService;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        _vm = DataContext as MainViewModel;
        if (_vm is null) return;

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
        ApplyMatchMarkers(editor, vm);
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

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OpenFileCommand.Execute(null);
    }

    private void MatchList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.OpenFileAtLineCommand.Execute(null);
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

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SaveSession();
        base.OnClosed(e);
    }
}
