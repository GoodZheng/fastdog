using FastDog.Services;

namespace FastDog.Tests;

public class JsonParserTests
{
    [Fact]
    public void ParseBeginMessage()
    {
        var json = @"{""type"":""begin"",""data"":{""path"":{""text"":""E:\\code\\App.cs""}}}";
        var result = RipgrepBridge.ParseRgLine(json);
        Assert.NotNull(result);
        Assert.Equal(RgEventType.FileBegin, result.Type);
        Assert.Equal(@"E:\code\App.cs", result.FilePath);
    }

    [Fact]
    public void ParseMatchMessage()
    {
        var json = @"{""type"":""match"",""data"":{""path"":{""text"":""E:\\code\\App.cs""},""lines"":{""text"":""public class App\r\n""},""line_number"":5,""absolute_offset"":120,""submatches"":[{""match"":{""text"":""class""},""start"":7,""end"":12}]}}";
        var result = RipgrepBridge.ParseRgLine(json);
        Assert.NotNull(result);
        Assert.Equal(RgEventType.Match, result.Type);
        Assert.Equal(@"E:\code\App.cs", result.FilePath);
        Assert.Equal(5, result.LineNumber);
        Assert.Equal("public class App\r\n", result.LineText);
        Assert.Equal(7, result.MatchStart);
        Assert.Equal(12, result.MatchEnd);
    }

    [Fact]
    public void ParseEndMessage()
    {
        var json = @"{""type"":""end"",""data"":{""path"":{""text"":""E:\\code\\App.cs""},""binary_offset"":null,""stats"":{""elapsed"":{""secs"":0,""nanos"":23400,""human"":""0.000023s""},""searches"":1,""searches_with_match"":1,""bytes_searched"":33782,""bytes_printed"":394,""matched_lines"":2,""matches"":3}}}";
        var result = RipgrepBridge.ParseRgLine(json);
        Assert.NotNull(result);
        Assert.Equal(RgEventType.FileEnd, result.Type);
        Assert.Equal(@"E:\code\App.cs", result.FilePath);
    }

    [Fact]
    public void ParseSummaryMessage()
    {
        var json = @"{""data"":{""elapsed_total"":{""human"":""0.003853s"",""nanos"":3853100,""secs"":0},""stats"":{""bytes_printed"":394,""bytes_searched"":33782,""elapsed"":{""human"":""0.000023s"",""nanos"":23400,""secs"":0},""matched_lines"":2,""matches"":3,""searches"":1,""searches_with_match"":1}},""type"":""summary""}";
        var result = RipgrepBridge.ParseRgLine(json);
        Assert.NotNull(result);
        Assert.Equal(RgEventType.Summary, result.Type);
        Assert.Equal(3L, result.TotalMatches);
        Assert.Equal(2L, result.MatchedLines);
    }

    [Fact]
    public void ParseNull_ReturnsNull()
    {
        var result = RipgrepBridge.ParseRgLine("");
        Assert.Null(result);
    }
}
