using System.IO;
using FastDog.Models;

namespace FastDog.Services;

public class FilePreviewService
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
    private const int MaxLines = 5000;

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".obj", ".o", ".so", ".dylib",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".tif", ".tiff", ".webp",
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz",
        ".mp3", ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flac", ".wav",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".bin", ".dat", ".db", ".sqlite", ".mdb",
        ".class", ".jar", ".war", ".nupkg", ".snk",
        ".woff", ".woff2", ".ttf", ".eot",
    };

    public bool IsBinaryFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return BinaryExtensions.Contains(ext);
    }

    public string? LoadFileContent(string filePath, out bool truncated)
    {
        truncated = false;

        if (!File.Exists(filePath))
            return null;

        try
        {
            if (IsBinaryFile(filePath))
                return null;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSize)
            {
                truncated = true;
                return ReadFirstLines(filePath, MaxLines);
            }

            return File.ReadAllText(filePath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public (int start, int end) ComputeGlobalOffset(int[] lineLengths, MatchLine match)
    {
        int lineIndex = match.LineNumber - 1;
        int offset = 0;
        for (int i = 0; i < lineIndex; i++)
            offset += lineLengths[i];

        // ripgrep 的 submatches.start/end 是 UTF-8 字节偏移量，
        // 但 AvalonEdit 使用字符偏移量，必须转换。
        int charStart = ByteToCharOffset(match.LineText, match.MatchStart);
        int charEnd = ByteToCharOffset(match.LineText, match.MatchEnd);

        return (offset + charStart, offset + charEnd);
    }

    /// <summary>
    /// 将 UTF-8 字节偏移量转换为 .NET 字符串的字符偏移量。
    /// 对于 ASCII 文本两者相同；对于中文等多字节字符，字节偏移 > 字符偏移。
    /// </summary>
    public static int ByteToCharOffset(string text, int byteOffset)
    {
        if (byteOffset <= 0) return 0;
        if (string.IsNullOrEmpty(text)) return byteOffset; // 回退：无行文本时假定 ASCII
        var utf8 = System.Text.Encoding.UTF8;
        var bytes = utf8.GetBytes(text);
        if (byteOffset >= bytes.Length) return text.Length;
        return utf8.GetCharCount(bytes, 0, byteOffset);
    }

    public int[] ComputeLineLengths(string content)
    {
        var lines = content.Split('\n');
        // Trailing \n produces an empty final element; exclude it
        int count = lines.Length;
        if (count > 0 && lines[count - 1].Length == 0)
            count--;
        var lengths = new int[count];
        for (int i = 0; i < count; i++)
        {
            lengths[i] = lines[i].Length;
            if (i < count - 1)
                lengths[i] += 1; // \n
        }
        return lengths;
    }

    private static string ReadFirstLines(string filePath, int maxLines)
    {
        var lines = new List<string>(maxLines);
        using var reader = new StreamReader(filePath);
        for (int i = 0; i < maxLines; i++)
        {
            var line = reader.ReadLine();
            if (line is null) break;
            lines.Add(line);
        }
        return string.Join("\n", lines) + "\n";
    }
}
