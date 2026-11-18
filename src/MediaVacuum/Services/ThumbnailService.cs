using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using MediaVacuum.Core;

namespace MediaVacuum.Services;

/// <summary>Downloads/caches playlist thumbnails for the queue.</summary>
public class ThumbnailService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _semaphore = new(4);
    private readonly ConcurrentDictionary<string, BitmapImage> _memoryCache = new(StringComparer.OrdinalIgnoreCase);

    public ThumbnailService(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MediaVacuum", "0.2"));
    }

    public async Task<BitmapImage?> GetAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (_memoryCache.TryGetValue(url, out var cached))
        {
            return cached;
        }

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_memoryCache.TryGetValue(url, out cached))
            {
                return cached;
            }

            var bytes = await LoadBytesAsync(url, ct).ConfigureAwait(false);
            if (bytes is null)
            {
                return null;
            }

            var image = Decode(bytes);
            if (image is not null)
            {
                if (_memoryCache.Count > 600)
                {
                    _memoryCache.Clear();
                }

                _memoryCache[url] = image;
            }

            return image;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            AppLog.Exception($"Unable to load thumbnail {url}", ex);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<byte[]?> LoadBytesAsync(string url, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        var cachePath = Path.Combine(_cacheDirectory, hash + ".img");

        if (File.Exists(cachePath))
        {
            try
            {
                return await File.ReadAllBytesAsync(cachePath, ct).ConfigureAwait(false);
            }
            catch
            {
                // Re-download below.
            }
        }

        using var response = await _httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (bytes.Length == 0 || bytes.Length > 8 * 1024 * 1024)
        {
            return null;
        }

        try
        {
            var tempPath = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(tempPath, bytes, ct).ConfigureAwait(false);
            File.Move(tempPath, cachePath, true);
        }
        catch (Exception ex)
        {
            AppLog.Exception($"Unable to cache thumbnail {url}", ex);
        }

        return bytes;
    }

    private static BitmapImage? Decode(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 192;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _semaphore.Dispose();
    }
}
