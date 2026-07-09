using ICSharpCode.AvalonEdit.Search;

namespace FastDog.Helpers;

/// <summary>
/// AvalonEdit <see cref="SearchPanel"/> 查找面板的中文本地化。
/// 覆盖默认英文文本，使查找栏选项/提示与项目主搜索界面语言一致。
/// </summary>
public sealed class SearchPanelLocalization : Localization
{
    public override string MatchCaseText => "区分大小写";
    public override string MatchWholeWordsText => "全词匹配";
    public override string UseRegexText => "正则表达式";
    public override string FindNextText => "下一个 (F3)";
    public override string FindPreviousText => "上一个 (Shift+F3)";
    public override string ErrorText => "错误：";
    public override string NoMatchesFoundText => "未找到匹配项";
}
