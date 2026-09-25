using CommunityToolkit.Mvvm.ComponentModel;

namespace FastDog.Models;

/// <summary>
/// 后缀过滤下拉列表中的一项。Name 为不含点的小写后缀（如 "cs"）；
/// IsSelected 由 UI 勾选驱动，ViewModel 监听其变化以刷新按钮文案并持久化。
/// </summary>
public class FileExtensionItem : ObservableObject
{
    /// <summary>后缀名（不含点、小写）。</summary>
    public string Name { get; init; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
