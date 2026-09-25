using System.Text.Json;
using FastDog.Models;

namespace FastDog.Services;

/// <summary>
/// 文件搜索「后缀过滤」列表持久化（%APPDATA%\FastDog\extension-config.json）：
/// 记录内置后缀的删除结果、用户添加的自定义后缀与勾选状态。
/// </summary>
public class FileExtensionConfigService
{
    /// <summary>内置默认后缀（不含点、小写）。「恢复默认」以此还原。</summary>
    public static readonly string[] DefaultExtensions =
        ["cs", "xaml", "js", "ts", "py", "java", "md", "txt", "json", "xml", "html", "css"];

    private const string FileName = "extension-config.json";
    private readonly string _filePath;

    public FileExtensionConfigService() : this(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastDog"))
    {
    }

    public FileExtensionConfigService(string directory)
    {
        _filePath = Path.Combine(directory, FileName);
    }

    /// <summary>读取配置；文件缺失或损坏时返回默认列表（全部未勾选）。</summary>
    public FileExtensionConfig Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var data = JsonSerializer.Deserialize<FileExtensionConfig>(File.ReadAllText(_filePath));
                if (data is not null && data.Items.Count > 0)
                    return data;
            }
        }
        catch
        {
            // 损坏即回退默认，与搜索历史服务的容错策略一致
        }
        return new FileExtensionConfig { Items = [.. DefaultExtensions], Selected = [] };
    }

    public void Save(FileExtensionConfig config)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}
