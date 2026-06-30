using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FastDog.Services;

/// <summary>
/// 检查更新服务。通过 GitHub Releases API 获取最新版本信息，
/// 与本地程序集版本比对；可下载对应安装包到任意路径。
/// </summary>
public sealed class UpdateService : IDisposable
{
    private const string RepoOwner = "GoodZheng";
    private const string RepoName = "fastdog";
    private const string ReleasesLatestUrl =
        $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
    private const string FallbackReleasesUrl =
        $"https://github.com/{RepoOwner}/{RepoName}/releases/latest";
    private const string AssetNamePrefix = "FastDog-Setup-";
    private const string AssetNameSuffix = ".exe";
    private const int CacheHours = 24; // 缓存有效期（小时）

    private readonly HttpClient _http;

    public UpdateService()
    {
        _http = new HttpClient
        {
            DefaultRequestHeaders =
            {
                { "User-Agent", "FastDog" }
            }
        };
    }

    /// <summary>本地程序集版本号（3 段，与 csproj &lt;Version&gt; 同源）。</summary>
    public static Version LocalVersion
    {
        get
        {
            var v = typeof(UpdateService).Assembly.GetName().Version;
            return v ?? new Version(0, 0, 0);
        }
    }

    public static string LocalVersionString => LocalVersion.ToString(3);

    /// <summary>
    /// 缓存文件路径：%APPDATA%\FastDog\update-cache.json
    /// </summary>
    private static string CacheFilePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "FastDog", "update-cache.json");
        }
    }

    /// <summary>
    /// 查询 GitHub releases/latest，与本地版本比对。
    /// 先检查本地缓存（24 小时有效），避免频繁触发 API 限流。
    /// 网络失败抛 <see cref="HttpRequestException"/>；tag 格式不规范或找不到资产时
    /// 返回 <see cref="UpdateInfo"/>，其中 <see cref="UpdateInfo.HasUpdate"/> = false 或
    /// <see cref="UpdateInfo.DownloadUrl"/> = null（回退到 Release 页面）。
    /// </summary>
    public async Task<UpdateInfo> CheckAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        // 检查缓存（除非强制刷新）
        if (!forceRefresh && TryLoadCache(out var cachedInfo))
            return cachedInfo;

        // 调用 GitHub API
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(ReleasesLatestUrl, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"网络请求失败：{ex.Message}", ex);
        }

        // 处理 403 Rate Limit
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new RateLimitExceededException(
                "GitHub API 限流（未认证请求每小时仅 60 次）。请稍后再试，或等待 24 小时后自动恢复。");
        }

        // 处理 404（仓库不存在或没有 Release）
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("GitHub 上未找到 Release 信息");
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var release = JsonSerializer.Deserialize<GitHubRelease>(json, _jsonOptions);
        if (release is null)
            throw new InvalidOperationException("GitHub API 返回空响应");

        // 解析 tag：去掉 v 前缀，再 Version.TryParse
        var tagName = release.TagName?.Trim() ?? "";
        var cleanTag = tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? tagName[1..]
            : tagName;

        Version? latest = null;
        var hasUpdate = false;
        if (Version.TryParse(cleanTag, out var parsed))
        {
            latest = parsed;
            hasUpdate = parsed > LocalVersion;
        }

        // 定位安装包资产：FastDog-Setup-*.exe（与 installer.iss 的 OutputBaseFilename 约定一致）
        var downloadUrl = release.Assets?
            .FirstOrDefault(a =>
                a.Name is not null
                && a.Name.StartsWith(AssetNamePrefix, StringComparison.OrdinalIgnoreCase)
                && a.Name.EndsWith(AssetNameSuffix, StringComparison.OrdinalIgnoreCase))
            ?.BrowserDownloadUrl;

        var result = new UpdateInfo
        {
            RawTagName = tagName,
            LatestVersion = latest,
            HasUpdate = hasUpdate,
            DownloadUrl = downloadUrl,
            ReleasePageUrl = release.HtmlUrl ?? FallbackReleasesUrl,
            IsPrerelease = release.Prerelease,
        };

        // 保存到缓存
        SaveCache(result);

        return result;
    }

    /// <summary>
    /// 尝试从缓存加载结果。缓存有效期 24 小时。
    /// </summary>
    private static bool TryLoadCache(out UpdateInfo info)
    {
        info = null!;
        try
        {
            if (!File.Exists(CacheFilePath))
                return false;

            var json = File.ReadAllText(CacheFilePath);
            var cache = JsonSerializer.Deserialize<UpdateCache>(json);
            if (cache is null || cache.CheckedAt is null)
                return false;

            // 检查缓存是否过期（24 小时）
            var hoursSinceCheck = (DateTime.Now - cache.CheckedAt.Value).TotalHours;
            if (hoursSinceCheck > CacheHours)
                return false;

            info = cache.Info;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 保存结果到缓存文件。
    /// </summary>
    private static void SaveCache(UpdateInfo info)
    {
        try
        {
            var cache = new UpdateCache
            {
                CheckedAt = DateTime.Now,
                Info = info
            };
            var json = JsonSerializer.Serialize(cache, _jsonOptions);
            var dir = Path.GetDirectoryName(CacheFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(CacheFilePath, json);
        }
        catch
        {
            // 缓存写入失败不影响主流程
        }
    }

    /// <summary>
    /// 流式下载指定 URL 到目标路径，报告进度（0..1）。
    /// 若服务端未提供 Content-Length，progress 不会被报告（调用方按 0% 显示即可）。
    /// </summary>
    public async Task DownloadAsync(
        string url,
        string destPath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        using var response = await _http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var target = new FileStream(
            destPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)
                     .ConfigureAwait(false)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            totalRead += read;
            if (progress is not null && totalBytes > 0)
                progress.Report((double)totalRead / totalBytes);
        }

        if (progress is not null) progress.Report(1.0);
    }

    public void Dispose() => _http.Dispose();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }

    private sealed class UpdateCache
    {
        public DateTime? CheckedAt { get; set; }
        public UpdateInfo Info { get; set; } = null!;
    }
}

/// <summary>
/// GitHub API 限流异常。
/// </summary>
public sealed class RateLimitExceededException : Exception
{
    public RateLimitExceededException(string message) : base(message) { }
}

/// <summary>
/// 检查更新结果：最新版本、是否可下载、Releases 页面地址。
/// </summary>
public sealed record UpdateInfo
{
    public string RawTagName { get; init; } = "";
    public Version? LatestVersion { get; init; }
    public bool HasUpdate { get; init; }
    /// <summary>安装包直接下载 URL；找不到对应 .exe 资产时为 null。</summary>
    public string? DownloadUrl { get; init; }
    /// <summary>Releases 页面 URL（兜底入口）。</summary>
    public string ReleasePageUrl { get; init; } = "";
    public bool IsPrerelease { get; init; }
}
