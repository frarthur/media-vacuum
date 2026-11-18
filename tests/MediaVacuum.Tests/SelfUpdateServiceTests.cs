using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using MediaVacuum.Core.Models;
using MediaVacuum.Core.Services;

namespace MediaVacuum.Tests;

public class SelfUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_PrefersZipAsset()
    {
        const string json = """
        {
          "tag_name": "v0.3.0",
          "html_url": "https://github.com/frarthur/MediaVacuum/releases/tag/v0.3.0",
          "assets": [
            { "name": "MediaVacuum-0.3.0-win-x64.zip", "browser_download_url": "https://example.com/update.zip", "digest": "sha256:ABCD" },
            { "name": "MediaVacuum.exe", "browser_download_url": "https://example.com/MediaVacuum.exe" }
          ]
        }
        """;

        var service = CreateService(json);

        var update = await service.CheckForUpdateAsync();

        Assert.NotNull(update);
        Assert.Equal("0.3.0", update!.Version);
        Assert.True(update.IsZip);
        Assert.Equal("https://example.com/update.zip", update.DownloadUrl);
        Assert.Equal("sha256:ABCD", update.Sha256);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNullWhenUpToDate()
    {
        const string json = """
        { "tag_name": "v0.2.0", "assets": [] }
        """;

        var service = CreateService(json);

        Assert.Null(await service.CheckForUpdateAsync());
    }

    [Fact]
    public async Task CheckForUpdateAsync_FallsBackToExeAsset()
    {
        const string json = """
        {
          "tag_name": "v0.3.0",
          "assets": [
            { "name": "MediaVacuum.exe", "browser_download_url": "https://example.com/MediaVacuum.exe" }
          ]
        }
        """;

        var service = CreateService(json);
        var update = await service.CheckForUpdateAsync();

        Assert.NotNull(update);
        Assert.False(update!.IsZip);
        Assert.Equal("https://example.com/MediaVacuum.exe", update.DownloadUrl);
    }

    [Fact]
    public async Task DownloadAsync_ExtractsExecutableFromZip()
    {
        var root = Path.Combine(Path.GetTempPath(), "mv-selfupdate-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var updates = Path.Combine(root, "updates");
        Directory.CreateDirectory(source);

        try
        {
            var exePath = Path.Combine(source, "MediaVacuum.exe");
            await File.WriteAllBytesAsync(exePath, new byte[10_100_000]);
            var zipPath = Path.Combine(root, "release.zip");
            ZipFile.CreateFromDirectory(source, zipPath);
            var zipBytes = await File.ReadAllBytesAsync(zipPath);

            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(zipBytes)
            });

            var service = new SelfUpdateService(new Version(0, 2, 0), new HttpClient(handler), updates);
            var update = new AppUpdateInfo(
                "0.3.0",
                "v0.3.0",
                "https://example.com/update.zip",
                "MediaVacuum-0.3.0-win-x64.zip",
                IsZip: true,
                Sha256: null,
                ReleaseNotesUrl: null);

            var downloaded = await service.DownloadAsync(update);

            Assert.True(File.Exists(downloaded));
            Assert.True(new FileInfo(downloaded).Length > 10_000_000);
            Assert.EndsWith(".exe", downloaded, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Best effort.
            }
        }
    }

    private static SelfUpdateService CreateService(string json)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        return new SelfUpdateService(new Version(0, 2, 0), new HttpClient(handler));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
        {
            _factory = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_factory(request));
    }
}
