using System.Collections.Concurrent;
using FastDog.Models;

namespace FastDog.Services;

public class SearchService
{
    private readonly RipgrepBridge _bridge = new();
    private CancellationTokenSource? _cts;

    public bool IsSearching => _cts is not null;

    public event Action<SearchResult>? ResultReceived;
    public event Action<string>? SearchCompleted;
    public event Action? SearchCancelled;

    public async Task SearchAsync(SearchQuery query, string basePath)
    {
        _cts = new CancellationTokenSource();
        try
        {
            var arguments = RipgrepBridge.BuildArguments(query);
            var fileResults = new ConcurrentDictionary<string, SearchResult>();

            await foreach (var rgEvent in _bridge.SearchAsync(arguments, _cts.Token))
            {
                switch (rgEvent.Type)
                {
                    case RgEventType.Match:
                        var result = fileResults.GetOrAdd(rgEvent.FilePath, path => new SearchResult
                        {
                            FilePath = path,
                            RelativePath = Path.GetRelativePath(basePath, path),
                            LastModified = File.GetLastWriteTime(path),
                            FileSize = new FileInfo(path).Length
                        });
                        result.Matches.Add(new MatchLine
                        {
                            LineNumber = rgEvent.LineNumber,
                            LineText = rgEvent.LineText,
                            MatchStart = rgEvent.MatchStart,
                            MatchEnd = rgEvent.MatchEnd
                        });
                        result.MatchCount = result.Matches.Count;
                        break;

                    case RgEventType.FileEnd:
                        if (fileResults.TryRemove(rgEvent.FilePath, out var fileResult))
                        {
                            if (query.DateFilterEnabled && !PassDateFilter(fileResult, query))
                                continue;
                            ResultReceived?.Invoke(fileResult);
                        }
                        break;

                    case RgEventType.Summary:
                        SearchCompleted?.Invoke(rgEvent.Elapsed);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            SearchCancelled?.Invoke();
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
        _bridge.KillProcess();
    }

    public static List<SearchResult> FilterByDate(
        List<SearchResult> results, DateTime? from, DateTime? to)
    {
        return results.Where(r =>
        {
            if (from.HasValue && r.LastModified < from.Value.Date) return false;
            if (to.HasValue && r.LastModified > to.Value.Date.AddDays(1)) return false;
            return true;
        }).ToList();
    }

    private static bool PassDateFilter(SearchResult result, SearchQuery query)
    {
        if (query.DateFrom.HasValue && result.LastModified < query.DateFrom.Value.Date) return false;
        if (query.DateTo.HasValue && result.LastModified > query.DateTo.Value.Date.AddDays(1)) return false;
        return true;
    }
}
