using MediaVacuum.Core.Models;
using MediaVacuum.Core.Utilities;

namespace MediaVacuum.Tests;

public class FilenameServiceTests
{
    [Fact]
    public void Sanitize_RemovesInvalidWindowsCharacters()
    {
        var result = FilenameService.Sanitize("My: video / test \\ <> | ? *");

        Assert.DoesNotContain(':', result);
        Assert.DoesNotContain('/', result);
        Assert.DoesNotContain('\\', result);
        Assert.DoesNotContain('<', result);
        Assert.DoesNotContain('>', result);
        Assert.DoesNotContain('|', result);
        Assert.DoesNotContain('?', result);
        Assert.DoesNotContain('*', result);
    }

    [Fact]
    public void Sanitize_CollapsesWhitespaceAndTrimsDots()
    {
        Assert.Equal("Hello world", FilenameService.Sanitize("  Hello    world ... "));
    }

    [Fact]
    public void Sanitize_PrefixesReservedNames()
    {
        Assert.Equal("_CON", FilenameService.Sanitize("CON"));
        Assert.Equal("_lpt1", FilenameService.Sanitize("lpt1"));
    }

    [Fact]
    public void Sanitize_EnforcesMaximumLength()
    {
        var result = FilenameService.Sanitize(new string('a', 300), 50);
        Assert.Equal(50, result.Length);
    }

    [Fact]
    public void Sanitize_KeepsAccentsAndApostrophes()
    {
        Assert.Equal("L'été à Paris", FilenameService.Sanitize("L'été à Paris"));
    }

    [Fact]
    public void BuildOutputTemplate_IncludesPlaylistIndexAndFolder()
    {
        var item = new DownloadItem
        {
            Title = "A/B: test?",
            PlaylistIndex = 3,
            PlaylistTitle = "My playlist"
        };
        var options = new DownloadOptions { CreatePlaylistFolder = true };

        var template = FilenameService.BuildOutputTemplate(item, options);

        Assert.Equal(Path.Combine("My playlist", "03 - A B test") + ".%(ext)s", template);
    }

    [Fact]
    public void BuildOutputTemplate_EscapesPercentSigns()
    {
        var item = new DownloadItem { Title = "100% real" };
        var options = new DownloadOptions { CreatePlaylistFolder = false };

        var template = FilenameService.BuildOutputTemplate(item, options);

        Assert.Equal("100%% real.%(ext)s", template);
    }

    [Fact]
    public void BuildBaseName_AddsVideoIdWhenRequested()
    {
        var item = new DownloadItem { Title = "Title", Id = "abc123" };
        var options = new DownloadOptions { IncludeVideoId = true };

        Assert.Equal("Title [abc123]", FilenameService.BuildBaseName(item, options));
    }
}
