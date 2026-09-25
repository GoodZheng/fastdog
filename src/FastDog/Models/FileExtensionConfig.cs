namespace FastDog.Models;

/// <summary>文件搜索「后缀过滤」下拉列表的持久化模型。</summary>
public class FileExtensionConfig
{
    /// <summary>当前列表（内置后缀减去用户删除的 + 用户添加的自定义后缀，按显示顺序）。</summary>
    public List<string> Items { get; set; } = [];

    /// <summary>勾选中的后缀。</summary>
    public List<string> Selected { get; set; } = [];
}
