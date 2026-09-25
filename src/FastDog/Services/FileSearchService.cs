using System.Collections.Concurrent;
using System.Diagnostics;
using FastDog.Models;

namespace FastDog.Services;

public record FileSearchStats(int FileCount, int FolderCount, string Elapsed);

/// <summary>缓存命中时的一次性批量结果，已按显示顺序排好（文件在前、文件夹在后，同类按文件名升序）。</summary>
public record FileSearchBatch(IReadOnlyList<FileSearchResult> Items);

/// <summary>
/// 文件搜索服务：基于 rg --files 流式枚举路径（不做内容匹配），
/// C# 侧做文件名匹配（子串/通配符）、后缀过滤与日期过滤。
///
/// 输入即搜优化（Everything 思路）：目录遍历只做一次，枚举结果按
/// (root, ExcludeDirs, HiddenWhitelist) 缓存在内存；之后关键词/后缀/日期的
/// 变化走缓存纯内存过滤，毫秒级出结果。缓存与过滤条件完全解耦：
/// - 后缀过滤由 C# 完成（不再交给 rg --iglob），枚举与后缀选择解耦；
/// - 大小/修改时间对命中项实时 stat，日期过滤不受缓存陈旧影响；
/// - 命中项的 Exists 检查使已删除文件自动从结果消失（自愈）；
/// - 仅新建文件在缓存重建前不可见（手动「搜索」强制刷新由此可见）。
///
/// 文件夹结果由每条文件路径的祖先目录推导——rg 走到的每个目录必然含有文件，
/// 因此推导集合即全部非空文件夹（完全空的目录不在枚举结果中，属已知边界）。
/// </summary>
public class FileSearchService
{
    /// <summary>缓存有效性 key：仅包含影响 rg 遍历范围的输入（路径取全路径，规避相对路径歧义）。</summary>
    public readonly record struct CacheKey(string Root, string ExcludeDirs, string HiddenWhitelist);

    private sealed record EnumerationCache(List<string> Files, HashSet<string> Folders);

    // 单字段整体换引用，避免并发收尾时缓存与 key 错配
    private sealed record CacheState(CacheKey Key, EnumerationCache Cache);

    private readonly RipgrepBridge _bridge = new();
    private CancellationTokenSource? _cts;
    private CacheState? _cacheState;

    public bool IsSearching => _cts is not null;

    /// <summary>冷构建（rg 枚举）过程中的流式命中，供 UI 渐进显示。</summary>
    public event Action<FileSearchResult>? ResultReceived;

    /// <summary>缓存命中搜索的一次性批量结果（不启动 rg，纯内存过滤）。</summary>
    public event Action<FileSearchBatch>? ResultsReady;

    public event Action<FileSearchStats>? SearchCompleted;
    public event Action? SearchCancelled;

    /// <summary>
    /// 后缀过滤（C# 侧）：接受 "cs" / ".cs" / "*.cs" 等写法（统一剥掉 * 与点），
    /// 与路径扩展名做忽略大小写的尾部匹配（"tar.gz" 这类多段后缀也能正确命中）；
    /// 空列表 = 不过滤。
    /// </summary>
    public static bool MatchesExtensions(string path, IEnumerable<string>? extensions)
    {
        if (extensions is null) return true;
        var hasAny = false;
        foreach (var raw in extensions)
        {
            var ext = raw.Trim().TrimStart('*', '.');
            if (ext.Length == 0) continue;
            hasAny = true;
            if (path.EndsWith("." + ext, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return !hasAny; // 空列表（或全是无效写法）= 不过滤
    }

    /// <summary>
    /// 执行文件搜索。forceRefresh=true 时无视缓存强制重新枚举（手动「搜索」按钮语义：
    /// 缓存建立后新建/删除的文件借此可见）。
    /// </summary>
    public async Task SearchAsync(FileSearchQuery query, bool forceRefresh = false)
    {
        // 输入即搜下新查询会频繁顶掉旧查询：先取消旧运行并杀死 rg，
        // 迟到的旧事件由 VM 的代际守卫过滤
        if (_cts is not null)
        {
            _cts.Cancel();
            _bridge.KillProcess();
        }

        var cts = new CancellationTokenSource();
        _cts = cts;

        try
        {
            var root = Path.GetFullPath(query.SearchPath);
            var key = new CacheKey(root, query.ExcludeDirs ?? string.Empty, query.HiddenWhitelist ?? string.Empty);
            var cached = forceRefresh ? null : _cacheState;

            if (cached is not null && cached.Key == key)
                await SearchFromCacheAsync(query, root, cached.Cache, cts.Token);
            else
                await EnumerateAndSearchAsync(query, root, key, cts.Token);
        }
        catch (OperationCanceledException)
        {
            SearchCancelled?.Invoke();
        }
        finally
        {
            // 只有仍是当前运行时才清场，避免误释放顶掉者（新运行）的 CTS
            if (ReferenceEquals(_cts, cts))
            {
                _cts.Dispose();
                _cts = null;
            }
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _bridge.KillProcess();
    }

    // --- 冷构建：rg 枚举 + 边过滤边推送；完整走完才落缓存 ---

    private async Task EnumerateAndSearchAsync(FileSearchQuery query, string root, CacheKey key, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        // 枚举与过滤解耦：paths/folders 是全量收集（与关键词/后缀/日期无关），
        // 完整走完即缓存，被取消则不落（防半截缓存污染输入即搜）
        var paths = new List<string>();
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matcher = new FileNameMatcher(query.NamePattern, query.CaseSensitive);
        var fileCount = 0;

        var arguments = RipgrepBridge.BuildFileSearchArguments(query);
        await foreach (var path in _bridge.ListFilesAsync(arguments, ct))
        {
            // 文件夹集合独立于文件名匹配：目录是独立的搜索对象，
            // 不因其子文件是否命中而增删
            CollectFolders(path, root, folders);
            paths.Add(path);

            if (!query.IncludeFiles) continue;
            if (!matcher.IsMatch(Path.GetFileName(path))) continue;
            if (!MatchesExtensions(path, query.Extensions)) continue;

            var info = new FileInfo(path);
            if (!info.Exists) continue; // 枚举与 stat 之间被删除的文件
            if (!PassDateFilter(info.LastWriteTime, query)) continue;

            fileCount++;
            ResultReceived?.Invoke(CreateFileResult(path, root, info));
        }

        var folderCount = 0;
        if (query.IncludeFolders)
        {
            var folderResults = CollectFolderResults(query, root, folders, matcher);
            folderCount = folderResults.Count;
            foreach (var folder in folderResults)
                ResultReceived?.Invoke(folder);
        }

        _cacheState = new CacheState(key, new EnumerationCache(paths, folders));

        stopwatch.Stop();
        SearchCompleted?.Invoke(new FileSearchStats(
            fileCount, folderCount, $"{stopwatch.Elapsed.TotalSeconds:F2}s"));
    }

    // --- 缓存命中：纯内存过滤 + 命中项并行 stat，毫秒级 ---

    private async Task SearchFromCacheAsync(FileSearchQuery query, string root, EnumerationCache cache, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var matcher = new FileNameMatcher(query.NamePattern, query.CaseSensitive);

        var fileResults = new ConcurrentBag<FileSearchResult>();
        if (query.IncludeFiles)
        {
            await Task.Run(() =>
            {
                // stat 是逐文件系统调用，命中量大时并行拉平耗时；
                // matcher 构造后无状态（Regex.IsMatch 线程安全），可并发调用
                Parallel.For(0, cache.Files.Count, new ParallelOptions { CancellationToken = ct }, i =>
                {
                    var path = cache.Files[i];
                    if (!matcher.IsMatch(Path.GetFileName(path))) return;
                    if (!MatchesExtensions(path, query.Extensions)) return;

                    var info = new FileInfo(path);
                    if (!info.Exists) return; // 缓存建立后被删除的文件：Exists 检查自愈
                    if (!PassDateFilter(info.LastWriteTime, query)) return;

                    fileResults.Add(CreateFileResult(path, root, info));
                });
            }, ct);
        }

        var items = fileResults.ToList();
        if (query.IncludeFolders)
            items.AddRange(CollectFolderResults(query, root, cache.Folders, matcher));
        items.Sort(DisplayOrderComparison);

        stopwatch.Stop();
        ResultsReady?.Invoke(new FileSearchBatch(items));
        SearchCompleted?.Invoke(new FileSearchStats(
            items.Count(r => !r.IsFolder),
            items.Count(r => r.IsFolder),
            $"{stopwatch.Elapsed.TotalSeconds:F2}s"));
    }

    /// <summary>文件在前、文件夹在后，同类按文件名升序（与 VM.SortFileResults 同序）。</summary>
    private static readonly Comparison<FileSearchResult> DisplayOrderComparison = (a, b) =>
    {
        var folderA = a.IsFolder ? 1 : 0;
        var folderB = b.IsFolder ? 1 : 0;
        if (folderA != folderB) return folderA.CompareTo(folderB);
        return string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
    };

    private static FileSearchResult CreateFileResult(string path, string root, FileInfo info) => new()
    {
        FilePath = path,
        RelativePath = Path.GetRelativePath(root, path),
        IsFolder = false,
        Extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
        FileSize = info.Length,
        LastModified = info.LastWriteTime
    };

    /// <summary>遍历文件夹候选集，套用名称/日期过滤，按相对路径排序（冷构建路径的推送顺序）。</summary>
    private static List<FileSearchResult> CollectFolderResults(
        FileSearchQuery query, string root, HashSet<string> folders, FileNameMatcher matcher)
    {
        var folderResults = new List<FileSearchResult>();
        foreach (var folder in folders)
        {
            if (!matcher.IsMatch(Path.GetFileName(folder))) continue;
            if (!Directory.Exists(folder)) continue; // 缓存/枚举间隙被删除的目录
            var modified = Directory.GetLastWriteTime(folder);
            if (!PassDateFilter(modified, query)) continue;

            folderResults.Add(new FileSearchResult
            {
                FilePath = folder,
                RelativePath = Path.GetRelativePath(root, folder),
                IsFolder = true,
                LastModified = modified
            });
        }
        folderResults.Sort((a, b) => string.Compare(
            a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));
        return folderResults;
    }

    /// <summary>
    /// 把 path 的所有位于 root 之下的祖先目录收进 folders（root 本身除外）。
    /// 剪枝：某目录已收录时其祖先必然也已收录，直接跳出。
    /// </summary>
    private static void CollectFolders(string path, string root, HashSet<string> folders)
    {
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        while (!string.IsNullOrEmpty(dir))
        {
            if (!dir.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) break;
            if (!folders.Add(dir)) break;
            dir = Path.GetDirectoryName(dir);
        }
    }

    private static bool PassDateFilter(DateTime modified, FileSearchQuery query)
    {
        if (!query.DateFilterEnabled) return true;
        if (query.DateFrom.HasValue && modified < query.DateFrom.Value.Date) return false;
        if (query.DateTo.HasValue && modified > query.DateTo.Value.Date.AddDays(1)) return false;
        return true;
    }
}
