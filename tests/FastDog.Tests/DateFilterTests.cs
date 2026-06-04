using FastDog.Services;
using FastDog.Models;

namespace FastDog.Tests;

public class DateFilterTests
{
    [Fact]
    public void FilterByDate_WithinRange()
    {
        var results = new List<SearchResult>
        {
            new() { FilePath = "a.cs", LastModified = new DateTime(2026, 5, 15) }
        };
        var filtered = SearchService.FilterByDate(
            results, new DateTime(2026, 5, 1), new DateTime(2026, 6, 1));
        Assert.Single(filtered);
    }

    [Fact]
    public void FilterByDate_BeforeRange()
    {
        var results = new List<SearchResult>
        {
            new() { FilePath = "a.cs", LastModified = new DateTime(2026, 4, 15) }
        };
        var filtered = SearchService.FilterByDate(
            results, new DateTime(2026, 5, 1), new DateTime(2026, 6, 1));
        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterByDate_AfterRange()
    {
        var results = new List<SearchResult>
        {
            new() { FilePath = "a.cs", LastModified = new DateTime(2026, 7, 1) }
        };
        var filtered = SearchService.FilterByDate(
            results, new DateTime(2026, 5, 1), new DateTime(2026, 6, 1));
        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterByDate_OnlyFrom()
    {
        var results = new List<SearchResult>
        {
            new() { FilePath = "a.cs", LastModified = new DateTime(2026, 5, 15) },
            new() { FilePath = "b.cs", LastModified = new DateTime(2026, 4, 15) }
        };
        var filtered = SearchService.FilterByDate(
            results, new DateTime(2026, 5, 1), null);
        Assert.Single(filtered);
        Assert.Equal("a.cs", filtered[0].FilePath);
    }

    [Fact]
    public void FilterByDate_OnlyTo()
    {
        var results = new List<SearchResult>
        {
            new() { FilePath = "a.cs", LastModified = new DateTime(2026, 5, 15) },
            new() { FilePath = "b.cs", LastModified = new DateTime(2026, 7, 1) }
        };
        var filtered = SearchService.FilterByDate(
            results, null, new DateTime(2026, 6, 1));
        Assert.Single(filtered);
        Assert.Equal("a.cs", filtered[0].FilePath);
    }
}
