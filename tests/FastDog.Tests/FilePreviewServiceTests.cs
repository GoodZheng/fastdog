using FastDog.Services;
using FastDog.Models;

namespace FastDog.Tests;

public class FilePreviewServiceTests
{
    private readonly FilePreviewService _service = new();

    [Fact]
    public void IsBinaryFile_KnownBinaryExtensions_ReturnsTrue()
    {
        Assert.True(_service.IsBinaryFile("app.exe"));
        Assert.True(_service.IsBinaryFile("lib.dll"));
        Assert.True(_service.IsBinaryFile("image.png"));
        Assert.True(_service.IsBinaryFile("photo.jpg"));
        Assert.True(_service.IsBinaryFile("archive.zip"));
        Assert.True(_service.IsBinaryFile("data.bin"));
        Assert.True(_service.IsBinaryFile("lib.so"));
        Assert.True(_service.IsBinaryFile("app.pdb"));
    }

    [Fact]
    public void IsBinaryFile_TextExtensions_ReturnsFalse()
    {
        Assert.False(_service.IsBinaryFile("Program.cs"));
        Assert.False(_service.IsBinaryFile("config.json"));
        Assert.False(_service.IsBinaryFile("page.xaml"));
        Assert.False(_service.IsBinaryFile("style.css"));
        Assert.False(_service.IsBinaryFile("readme.md"));
        Assert.False(_service.IsBinaryFile("script.py"));
        Assert.False(_service.IsBinaryFile("app.js"));
        Assert.False(_service.IsBinaryFile("FileWithNoExtension"));
    }

    [Fact]
    public void ReadFileContent_NonExistentFile_ReturnsError()
    {
        var result = _service.LoadFileContent(@"Z:\nonexistent\path\file.txt", out _);
        Assert.Null(result);
    }

    [Fact]
    public void ComputeGlobalOffsets_SingleLine()
    {
        var lineLengths = new[] { 11, 21, 15 };
        var match = new MatchLine { LineNumber = 2, MatchStart = 5, MatchEnd = 10 };
        var (start, end) = _service.ComputeGlobalOffset(lineLengths, match);
        Assert.Equal(11 + 5, start);
        Assert.Equal(11 + 10, end);
    }

    [Fact]
    public void ComputeGlobalOffsets_FirstLine()
    {
        var lineLengths = new[] { 11, 21, 15 };
        var match = new MatchLine { LineNumber = 1, MatchStart = 0, MatchEnd = 5 };
        var (start, end) = _service.ComputeGlobalOffset(lineLengths, match);
        Assert.Equal(0, start);
        Assert.Equal(5, end);
    }

    [Fact]
    public void ComputeGlobalOffsets_LastLine()
    {
        var lineLengths = new[] { 11, 21, 15 };
        var match = new MatchLine { LineNumber = 3, MatchStart = 3, MatchEnd = 8 };
        var (start, end) = _service.ComputeGlobalOffset(lineLengths, match);
        Assert.Equal(32 + 3, start);
        Assert.Equal(32 + 8, end);
    }

    [Fact]
    public void ComputeLineLengths_MultilineContent()
    {
        var content = "line1\nline2 is longer\nline3\n";
        var lengths = _service.ComputeLineLengths(content);
        Assert.Equal(3, lengths.Length);
        Assert.Equal(6, lengths[0]);
        Assert.Equal(16, lengths[1]);
        Assert.Equal(5, lengths[2]);
    }

    [Fact]
    public void ReadFileContent_ValidTextFile_ReturnsContent()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "hello world\nline 2\n");
            var result = _service.LoadFileContent(tempFile, out var truncated);
            Assert.Equal("hello world\nline 2\n", result);
            Assert.False(truncated);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadFileContent_LargeFile_Truncates()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var lines = Enumerable.Range(1, 100_000)
                .Select(i => $"line {i} content here padding padding padding extra data to make file large");
            File.WriteAllLines(tempFile, lines);
            var result = _service.LoadFileContent(tempFile, out var truncated);
            Assert.NotNull(result);
            Assert.True(truncated);
            var resultLineCount = result.Split('\n').Length - 1;
            Assert.True(resultLineCount <= 5001, $"Expected <= 5001 lines, got {resultLineCount}");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
