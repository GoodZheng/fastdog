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
        sb.Append("--json --no-heading ");

        if (!query.CaseSensitive)
            sb.Append("-i ");

        if (!query.IsRegex)
            sb.Append("-F ");

        if (query.WholeWord)
            sb.Append("-w ");

        if (!string.IsNullOrWhiteSpace(query.FileFilter))
        {
            foreach (var pattern in query.FileFilter.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = pattern.Trim();
                if (trimmed.Length > 0)
                    sb.Append($"--iglob \"{trimmed}\" ");
            }
        }

        var excludeDirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.ExcludeDirs))
        {
            foreach (var dir in query.ExcludeDirs.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = dir.Trim();
                if (trimmed.Length > 0)
                    excludeDirs.Add(trimmed);
            }
        }
        if (!excludeDirs.Contains(".git"))
            excludeDirs.Add(".git");

        foreach (var dir in excludeDirs)
            sb.Append($"--glob \"!{dir}\" ");

        sb.Append($"\"{query.SearchPath}\" ");
        sb.Append($"\"{query.SearchText}\"");

        return sb.ToString();
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
                FilePath = data.GetProperty("path").GetProperty("text").GetString() ?? ""
            },
            "match" => ParseMatch(data),
            "end" => new RgEvent
            {
                Type = RgEventType.FileEnd,
                FilePath = data.GetProperty("path").GetProperty("text").GetString() ?? ""
            },
            "summary" => ParseSummary(data),
            _ => null
        };
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
            FilePath = data.GetProperty("path").GetProperty("text").GetString() ?? "",
            LineNumber = data.GetProperty("line_number").GetInt32(),
            LineText = data.GetProperty("lines").GetProperty("text").GetString() ?? "",
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
            Elapsed = elapsed.GetProperty("human").GetString() ?? ""
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
