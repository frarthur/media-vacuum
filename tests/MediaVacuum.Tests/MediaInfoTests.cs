using System.Text.Json;
using MediaVacuum.Core.Models;

namespace MediaVacuum.Tests;

public class MediaInfoTests
{
    [Fact]
    public void Deserialize_PlaylistWithEntries()
    {
        const string json = """
        {
          "_type": "playlist",
          "id": "PL123",
          "title": "My channel",
          "webpage_url": "https://example.com/channel",
          "thumbnails": [
            { "url": "https://example.com/small.jpg", "width": 120 },
            { "url": "https://example.com/large.jpg", "width": 640 }
          ],
          "entries": [
            {
              "id": "abc",
              "title": "First video",
              "url": "https://example.com/watch?v=abc",
              "duration": 125.5,
              "thumbnails": [ { "url": "https://example.com/abc.jpg", "width": 320 } ]
            }
          ]
        }
        """;

        var info = JsonSerializer.Deserialize<MediaInfo>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(info);
        Assert.True(info!.IsPlaylist);
        Assert.Single(info.Entries!);
        Assert.Equal("First video", info.Entries![0].Title);
        Assert.Equal("https://example.com/large.jpg", info.GetBestThumbnailUrl());
        Assert.Equal("https://example.com/abc.jpg", info.Entries[0].GetBestThumbnailUrl());
    }

    [Fact]
    public void IsPlaylist_IsFalseForSingleVideo()
    {
        const string json = """{ "_type": "video", "id": "x", "title": "Video" }""";
        var info = JsonSerializer.Deserialize<MediaInfo>(json);

        Assert.NotNull(info);
        Assert.False(info!.IsPlaylist);
    }
}
