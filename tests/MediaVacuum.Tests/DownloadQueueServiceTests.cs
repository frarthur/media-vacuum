using MediaVacuum.Core.Interfaces;
using MediaVacuum.Core.Models;
using MediaVacuum.Core.Services;

namespace MediaVacuum.Tests;

public class DownloadQueueServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_PlaylistMapsEveryEntry()
    {
        var fake = new FakeYtDlp
        {
            Info = new MediaInfo
            {
                Type = "playlist",
                Title = "My playlist",
                WebpageUrl = "https://example.com/playlist",
                Entries =
                [
                    new MediaInfo
                    {
                        Id = "one",
                        Title = "First video",
                        Url = "https://example.com/watch?v=one",
                        Duration = 60,
                        Thumbnails = [new ThumbnailInfo { Url = "https://example.com/one.jpg", Width = 320 }]
                    },
                    new MediaInfo
                    {
                        Id = "two",
                        Title = "Second video",
                        WebpageUrl = "https://example.com/watch?v=two",
                        Duration = 30
                    }
                ]
            }
        };

        var queue = new DownloadQueueService(fake);
        var result = await queue.AnalyzeAsync(
            "https://example.com/playlist",
            new DownloadOptions(),
            includePlaylist: true);

        Assert.True(result.IsPlaylist);
        Assert.Equal("My playlist", result.Title);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Items[0].PlaylistIndex);
        Assert.Equal("My playlist", result.Items[0].PlaylistTitle);
        Assert.Equal("https://example.com/watch?v=one", result.Items[0].Url);
        Assert.Equal("https://example.com/one.jpg", result.Items[0].ThumbnailUrl);
        Assert.True(result.Items[0].TotalBytesEstimate > 0);
        Assert.True(result.Items[0].IsSizeEstimated);
    }

    [Fact]
    public async Task AnalyzeAsync_ResolvesYouTubeFlatEntryIds()
    {
        var fake = new FakeYtDlp
        {
            Info = new MediaInfo
            {
                Type = "playlist",
                Title = "Channel",
                WebpageUrl = "https://www.youtube.com/@example",
                Entries =
                [
                    new MediaInfo { Id = "abc123", Title = "Video", Url = "abc123" }
                ]
            }
        };

        var queue = new DownloadQueueService(fake);
        var result = await queue.AnalyzeAsync(
            "https://www.youtube.com/@example",
            new DownloadOptions(),
            includePlaylist: true);

        Assert.Equal("https://www.youtube.com/watch?v=abc123", result.Items[0].Url);
    }

    [Fact]
    public async Task AnalyzeAsync_SingleVideoMapsMetadata()
    {
        var fake = new FakeYtDlp
        {
            Info = new MediaInfo
            {
                Type = "video",
                Id = "abc",
                Title = "A video",
                WebpageUrl = "https://example.com/watch?v=abc",
                Duration = 42,
                FileSizeApprox = 123456
            }
        };

        var queue = new DownloadQueueService(fake);
        var result = await queue.AnalyzeAsync(
            "https://example.com/watch?v=abc",
            new DownloadOptions(),
            includePlaylist: false);

        Assert.False(result.IsPlaylist);
        var item = Assert.Single(result.Items);
        Assert.Equal("A video", item.Title);
        Assert.Equal(123456, item.TotalBytes);
        Assert.False(item.IsSizeEstimated);
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyPlaylistThrows()
    {
        var fake = new FakeYtDlp
        {
            Info = new MediaInfo { Type = "playlist", Title = "Empty", Entries = [] }
        };

        var queue = new DownloadQueueService(fake);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            queue.AnalyzeAsync("https://example.com/playlist", new DownloadOptions(), includePlaylist: true));
    }

    [Fact]
    public async Task StartAsync_MarksItemCompleted()
    {
        var fake = new FakeYtDlp
        {
            Result = DownloadResult.FromSuccess(@"C:\out\video.mp4", "Video", 4096)
        };

        var queue = new DownloadQueueService(fake);
        var item = new DownloadItem
        {
            Id = "abc",
            Title = "Video",
            Url = "https://example.com/watch?v=abc"
        };

        await queue.StartAsync([item], new DownloadOptions(), concurrency: 1);

        Assert.Equal(DownloadItemStatus.Completed, item.Status);
        Assert.Equal(100, item.Progress);
        Assert.Equal(@"C:\out\video.mp4", item.FilePath);
        Assert.Equal(4096, item.FileSize);
    }

    private sealed class FakeYtDlp : IYtDlpService
    {
        public MediaInfo? Info { get; set; }

        public DownloadResult Result { get; set; } = DownloadResult.FromSuccess("file.mp4", "Video", 1024);

        public string YtDlpPath => "fake-yt-dlp.exe";

        public bool Exists => true;

        public Task<MediaInfo?> GetMediaInfoAsync(
            string url,
            bool flatPlaylist,
            bool noPlaylist,
            CancellationToken ct = default) => Task.FromResult(Info);

        public Task<DownloadResult> DownloadAsync(
            DownloadItem item,
            DownloadOptions options,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken ct = default)
        {
            progress?.Report(new DownloadProgress
            {
                Status = "downloading",
                Percentage = 50,
                DownloadedBytes = 50,
                TotalBytes = 100,
                SpeedBytesPerSecond = 10,
                EtaSeconds = 1
            });

            progress?.Report(new DownloadProgress { Status = "postprocessing" });
            return Task.FromResult(Result);
        }

        public Task<string> GetVersionAsync(CancellationToken ct = default) =>
            Task.FromResult("2026.01.01");

        public void KillActiveProcesses()
        {
        }
    }
}
