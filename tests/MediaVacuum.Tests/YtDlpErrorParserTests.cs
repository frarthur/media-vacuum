using MediaVacuum.Core.Services;

namespace MediaVacuum.Tests;

public class YtDlpErrorParserTests
{
    [Fact]
    public void Parse_KeepsOnlyErrorLines()
    {
        const string stderr = """
        WARNING: Your yt-dlp version (2026.06.09) is older than 90 days!
        WARNING: [youtube] No supported JavaScript runtime could be found.
        WARNING: webm doesn't support embedding a thumbnail, mkv will be used
        ERROR: unable to download video data: HTTP Error 403: Forbidden
        """;

        var result = YtDlpErrorParser.Parse(stderr);

        Assert.Equal("unable to download video data: HTTP Error 403: Forbidden", result);
        Assert.DoesNotContain("WARNING", result);
    }

    [Fact]
    public void Parse_DeduplicatesErrors()
    {
        const string stderr = """
        ERROR: same failure
        ERROR: same failure
        """;

        Assert.Equal("same failure", YtDlpErrorParser.Parse(stderr));
    }

    [Fact]
    public void Parse_DropsWarningsWhenNoErrorLineExists()
    {
        const string stderr = """
        WARNING: first warning
        [youtube] Extracting URL
        [youtube] Some useful detail
        """;

        var result = YtDlpErrorParser.Parse(stderr);

        Assert.DoesNotContain("WARNING", result);
        Assert.Contains("Some useful detail", result);
    }

    [Fact]
    public void Parse_ReturnsFallbackMessageWhenEmpty()
    {
        Assert.Equal("yt-dlp failed.", YtDlpErrorParser.Parse(""));
    }
}
