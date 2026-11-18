using System.Globalization;
using MediaVacuum.Core.Services;

namespace MediaVacuum.Tests;

public class ProgressParserTests
{
    [Fact]
    public void Parse_DownloadLine_UsesInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            var parsed = ProgressParser.Parse("MVPROG|1048576|10485760|11000000|10.5|524288|30.2");

            Assert.NotNull(parsed);
            Assert.Equal(ProgressLineKind.Download, parsed!.Kind);
            Assert.Equal(1048576, parsed.Progress!.DownloadedBytes);
            Assert.Equal(10485760, parsed.Progress.TotalBytes);
            Assert.Equal(11000000, parsed.Progress.TotalBytesEstimate);
            Assert.Equal(10.5, parsed.Progress.Percentage);
            Assert.Equal(524288, parsed.Progress.SpeedBytesPerSecond);
            Assert.Equal(30.2, parsed.Progress.EtaSeconds);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Parse_HandlesNotAvailableValues()
    {
        var parsed = ProgressParser.Parse("MVPROG|NA|NA|NA|NA|NA|NA");

        Assert.NotNull(parsed);
        Assert.Null(parsed!.Progress!.DownloadedBytes);
        Assert.Null(parsed.Progress.Percentage);
    }

    [Fact]
    public void Parse_FileCompletedLine()
    {
        var parsed = ProgressParser.Parse("MVFILE|C:\\Downloads\\video.mp4|123456");

        Assert.NotNull(parsed);
        Assert.Equal(ProgressLineKind.FileCompleted, parsed!.Kind);
        Assert.Equal(@"C:\Downloads\video.mp4", parsed.FilePath);
        Assert.Equal(123456, parsed.FileSize);
    }

    [Fact]
    public void Parse_TreatsNotAvailablePathsAsNull()
    {
        var name = ProgressParser.Parse("MVNAME|NA");
        var file = ProgressParser.Parse("MVFILE|NA|NA");

        Assert.Null(name!.FilePath);
        Assert.Null(file!.FilePath);
        Assert.Null(file.FileSize);
    }

    [Fact]
    public void Parse_DetectsAlreadyDownloaded()
    {
        var parsed = ProgressParser.Parse("[download] C:\\Downloads\\video.mp4 has already been downloaded");
        Assert.Equal(ProgressLineKind.AlreadyDownloaded, parsed!.Kind);
    }

    [Fact]
    public void Parse_IgnoresUnrelatedLines()
    {
        Assert.Null(ProgressParser.Parse("[youtube] Extracting URL"));
    }
}
