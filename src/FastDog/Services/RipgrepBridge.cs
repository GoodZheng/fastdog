global using System;
global using System.Collections.Generic;
global using System.IO;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FastDog.Models;

namespace FastDog.Services;

public enum RgEventType
{
    FileBegin,
    Match,
    FileEnd,
    Summary
}

public class RgEvent
{
    public RgEventType Type { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string LineText { get; set; } = string.Empty;
    public int MatchStart { get; set; }
    public int MatchEnd { get; set; }
    public long TotalMatches { get; set; }
    public long MatchedLines { get; set; }
    public string Elapsed { get; set; } = string.Empty;
}

public sealed class RipgrepBridge : IDisposable
{
    private Process? _process;

    public static string FindRgPath()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var localRg = Path.Combine(appDir, "tools", "rg.exe");
        if (File.Exists(localRg))
            return localRg;

        var pathRg = FindInPath("rg.exe");
        if (pathRg is not null)
            return pathRg;

        throw new FileNotFoundException(
            "找不到 rg.exe。请将其放入 tools/ 目录或添加到系统 PATH。");
    }

    private static string? FindInPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var separator = Path.PathSeparator;
        foreach (var dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }
        return null;
    }

    public static string BuildArguments(SearchQuery query)
    {
        var sb = new StringBuilder();
        sb.Append("--json --no-heading --stats ");

        if (!query.CaseSensitive)
            sb.Append("-i ");

        if (!query.IsRegex)
            sb.Append("-F ");

        if (query.WholeWord)
            sb.Append("-w ");

        BuildFilterArgs(sb, query.FileFilter, query.ExcludeDirs);

        sb.Append(EscapeArg(query.SearchText)).Append(' ');
        sb.Append(EscapeArg(query.SearchPath));

        return sb.ToString();
    }

    public static string BuildFileListArguments(SearchQuery query)
    {
        var sb = new StringBuilder();
        sb.Append("--files ");
        BuildFilterArgs(sb, query.FileFilter, query.ExcludeDirs);
        sb.Append(EscapeArg(query.SearchPath));
        return sb.ToString();
    }

    private static void BuildFilterArgs(StringBuilder sb, string fileFilter, string excludeDirs)
    {
        if (!string.IsNullOrWhiteSpace(fileFilter))
        {
            foreach (var pattern in fileFilter.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = pattern.Trim();
                if (trimmed.Length > 0)
                    sb.Append("--iglob ").Append(EscapeArg(NormalizeFilePattern(trimmed))).Append(' ');
            }
        }

        var dirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(excludeDirs))
        {
            foreach (var dir in excludeDirs.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = dir.Trim();
                if (trimmed.Length > 0)
                    dirs.Add(trimmed);
            }
        }
        if (!dirs.Contains(".git"))
            dirs.Add(".git");

        foreach (var dir in dirs)
            sb.Append("--glob ").Append(EscapeArg("!" + dir)).Append(' ');
    }

    /// <summary>
    /// 把用户输入的文件过滤模式归一化为 ripgrep --iglob 可识别的 glob。
    /// 用户常输入扩展名形式（".cs"、"cs"），而 ripgrep 会把 ".cs" 当作「文件名恰好为 .cs」，
    /// 导致匹配不到 Program.cs 等文件。这里统一补成 "*.cs"。
    /// 已含通配符（*、?）或看起来像完整文件名（含点且不以点开头，如 "Makefile.cs"）的则原样返回。
    /// </summary>
    private static string NormalizeFilePattern(string pattern)
    {
        // 已含通配符，视为完整 glob，原样返回
        if (pattern.Contains('*') || pattern.Contains('?') || pattern.Contains('['))
            return pattern;

        if (pattern.StartsWith('.'))
        {
            // ".cs" → "*.cs"
            return "*" + pattern;
        }

        // 不含点的纯扩展名（"cs"）→ "*.cs"；其余（"Makefile" 等）原样返回
        if (!pattern.Contains('.'))
            return "*." + pattern;

        return pattern;
    }

    /// <summary>
    /// 与 .NET ProcessStartInfo.ArgumentList 一致）。
    /// 若不转义，搜索词中的双引号会从中间截断命令行，导致匹配失败。
    /// </summary>
    private static string EscapeArg(string arg)
    {
        arg ??= string.Empty;
        if (arg.Length == 0)
            return "\"\"";

        // 不含需转义字符（空格/制表符/双引号/控制字符）时，原样返回
        bool needsQuoting = false;
        foreach (var c in arg)
        {
            if (c is ' ' or '\t' or '"' || c < 0x20)
            {
                needsQuoting = true;
                break;
            }
        }
        if (!needsQuoting)
            return arg;

        var sb = new StringBuilder(arg.Length + 2);
        sb.Append('"');
        int backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                // 双引号前的反斜杠需翻倍，再加一个转义双引号
                sb.Append('\\', backslashes * 2 + 1).Append('"');
            }
            else
            {
                sb.Append('\\', backslashes).Append(c);
            }
            backslashes = 0;
        }

        // 以反斜杠结尾时，结尾反斜杠需翻倍（否则会被当成结束引号的转义符）
        if (backslashes > 0)
            sb.Append('\\', backslashes * 2);

        sb.Append('"');
        return sb.ToString();
    }

    public async Task<int> CountFilesAsync(string arguments, CancellationToken ct = default)
    {
        var rgPath = FindRgPath();
        var psi = new ProcessStartInfo
        {
            FileName = rgPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 rg.exe");
        var count = 0;
        var reader = process.StandardOutput;
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is not null)
                count++;
        }
        await process.WaitForExitAsync(ct);
        return count;
    }

    public void Dispose()
    {
        _process?.Kill();
        _process?.Dispose();
    }

    public static RgEvent? ParseRgLine(string jsonLine)
    {
        if (string.IsNullOrWhiteSpace(jsonLine))
            return null;

        using var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        var type = root.GetProperty("type").GetString() ?? "";
        var data = root.GetProperty("data");

        return type switch
        {
            "begin" => new RgEvent
            {
                Type = RgEventType.FileBegin,
                FilePath = GetTextOrBase64(data, "path")
            },
            "match" => ParseMatch(data),
            "end" => new RgEvent
            {
                Type = RgEventType.FileEnd,
                FilePath = GetTextOrBase64(data, "path")
            },
            "summary" => ParseSummary(data),
            _ => null
        };
    }

    private static string GetTextOrBase64(JsonElement parent, string propertyName)
    {
        var elem = parent.GetProperty(propertyName);
        if (elem.TryGetProperty("text", out var textElem))
            return textElem.GetString() ?? "";
        // rg 对非 UTF-8 内容输出 base64 编码的 "bytes" 字段
        var bytes = Convert.FromBase64String(elem.GetProperty("bytes").GetString() ?? "");
        return Encoding.UTF8.GetString(bytes);
    }

    private static RgEvent ParseMatch(JsonElement data)
    {
        var submatches = data.GetProperty("submatches");
        int matchStart = 0, matchEnd = 0;
        if (submatches.GetArrayLength() > 0)
        {
            var first = submatches[0];
            matchStart = first.GetProperty("start").GetInt32();
            matchEnd = first.GetProperty("end").GetInt32();
        }

        return new RgEvent
        {
            Type = RgEventType.Match,
            FilePath = GetTextOrBase64(data, "path"),
            LineNumber = data.GetProperty("line_number").GetInt32(),
            LineText = GetTextOrBase64(data, "lines"),
            MatchStart = matchStart,
            MatchEnd = matchEnd
        };
    }

    private static RgEvent ParseSummary(JsonElement data)
    {
        var stats = data.GetProperty("stats");
        var elapsed = data.GetProperty("elapsed_total");
        return new RgEvent
        {
            Type = RgEventType.Summary,
            TotalMatches = stats.GetProperty("matches").GetInt64(),
            MatchedLines = stats.GetProperty("matched_lines").GetInt64(),
            Elapsed = $"{elapsed.GetProperty("secs").GetInt64() + elapsed.GetProperty("nanos").GetInt64() / 1_000_000_000.0:F2}s"
        };
    }

    public async IAsyncEnumerable<RgEvent> SearchAsync(
        string arguments,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var rgPath = FindRgPath();
        var psi = new ProcessStartInfo
        {
            FileName = rgPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        _process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 rg.exe");

        var reader = _process.StandardOutput;
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            var rgEvent = ParseRgLine(line);
            if (rgEvent is not null)
                yield return rgEvent;
        }

        if (!ct.IsCancellationRequested)
            await _process.WaitForExitAsync(ct);
        else
            KillProcess();
    }

    public void KillProcess()
    {
        try
        {
            _process?.Kill(entireProcessTree: true);
        }
        catch { }
    }
}
