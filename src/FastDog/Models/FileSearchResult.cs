using System.IO;

namespace FastDog.Models;

/// <summary>文件搜索模式的结果行：文件或文件夹。</summary>
public class FileSearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public bool IsFolder { get; set; }

    public string FileName => Path.GetFileName(
        FilePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    /// <summary>小写、不含点的后缀；文件夹为空串。</summary>
    public string Extension { get; set; } = string.Empty;

    public long FileSize { get; set; }
    public string FileSizeDisplay => IsFolder ? "—" : FormatFileSize(FileSize);

    public DateTime LastModified { get; set; }

    private static string FormatFileSize(long bytes)
    {
        const long KB = 1024;
        if (bytes < KB) return $"{bytes} B";
        if (bytes < KB * 1024) return $"{bytes / (double)KB:N1} KB";
        if (bytes < KB * 1024 * 1024) return $"{bytes / (double)(KB * 1024):N1} MB";
        return $"{bytes / (double)(KB * 1024 * 1024):N2} GB";
    }
}
