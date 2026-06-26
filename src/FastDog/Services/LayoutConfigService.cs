using System.Text.Json;
using FastDog.Models;

namespace FastDog.Services;

public class LayoutConfigService
{
    private const string FileName = "layout-config.json";

    private readonly string _filePath;

    public LayoutConfigService() : this(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastDog"))
    {
    }

    public LayoutConfigService(string directory)
    {
        _filePath = Path.Combine(directory, FileName);
    }

    public LayoutConfig? Load()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<LayoutConfig>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Save(LayoutConfig config)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_filePath, json);
    }
}
