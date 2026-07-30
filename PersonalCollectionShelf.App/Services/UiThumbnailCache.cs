using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace PersonalCollectionShelf.App.Services;

public sealed class UiThumbnailCache
{
    private const int MaxDimension = 300;
    private readonly string _cacheDirectory = Path.Combine(FileSystem.CacheDirectory, "ui-thumbnails");
    private readonly SemaphoreSlim _decoder = new(3, 3);
    private readonly ConcurrentDictionary<string, Task<string?>> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _cachePaths = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _displaySources = new(StringComparer.Ordinal);

    public UiThumbnailCache() => Directory.CreateDirectory(_cacheDirectory);

    public string GetDisplaySource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;
        if (_displaySources.TryGetValue(source, out var resolved)) return resolved;
        if (!TryResolveLocalPath(source, out var localPath))
        {
            _displaySources[source] = source;
            return source;
        }
        var cachePath = GetCachePath(source, localPath);
        resolved = File.Exists(cachePath) ? new Uri(cachePath).AbsoluteUri : string.Empty;
        _displaySources[source] = resolved;
        return resolved;
    }

    public Task PrimeDisplaySourcesAsync(IEnumerable<string?> sources, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            foreach (var source in sources.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = GetDisplaySource(source);
            }
        }, cancellationToken);

    public async Task<int> WarmBatchAsync(
        IEnumerable<string?> sources,
        int maximumCount,
        Action<int, int>? progress = null,
        int degreeOfParallelism = 3,
        CancellationToken cancellationToken = default)
    {
        var batch = sources
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .Take(maximumCount)
            .ToArray();
        if (batch.Length == 0) return 0;

        var completed = 0;
        progress?.Invoke(0, batch.Length);
        await Parallel.ForEachAsync(
            batch,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(degreeOfParallelism, 1, 3),
                CancellationToken = cancellationToken
            },
            async (source, token) =>
            {
                _ = await GetOrCreateAsync(source, token).ConfigureAwait(false);
                var current = Interlocked.Increment(ref completed);
                if (current == batch.Length || current % 10 == 0) progress?.Invoke(current, batch.Length);
            }).ConfigureAwait(false);
        return completed;
    }

    public Task<string?> GetOrCreateAsync(string? source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source)) return Task.FromResult<string?>(null);
        if (!TryResolveLocalPath(source, out var localPath)) return Task.FromResult<string?>(source);
        if (!File.Exists(localPath)) return Task.FromResult<string?>(null);

        var cachePath = GetCachePath(source, localPath);
        if (File.Exists(cachePath))
        {
            var cachedSource = new Uri(cachePath).AbsoluteUri;
            _displaySources[source] = cachedSource;
            return Task.FromResult<string?>(cachedSource);
        }

        return _pending.GetOrAdd(cachePath, _ => CreateAsync(source, localPath, cachePath, cancellationToken));
    }

    private async Task<string?> CreateAsync(string sourceKey, string sourcePath, string cachePath, CancellationToken cancellationToken)
    {
        await _decoder.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(cachePath))
            {
                var existingSource = new Uri(cachePath).AbsoluteUri;
                _displaySources[sourceKey] = existingSource;
                return existingSource;
            }
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = File.OpenRead(sourcePath);
            var bounds = SKBitmap.DecodeBounds(stream);
            if (bounds.Width <= 0 || bounds.Height <= 0) return null;

            var scale = Math.Min(1d, MaxDimension / (double)Math.Max(bounds.Width, bounds.Height));
            var width = Math.Max(1, (int)Math.Round(bounds.Width * scale));
            var height = Math.Max(1, (int)Math.Round(bounds.Height * scale));
            stream.Position = 0;
            var targetInfo = new SKImageInfo(width, height, bounds.ColorType, bounds.AlphaType, bounds.ColorSpace);
            using var decoded = SKBitmap.Decode(stream, targetInfo);
            if (decoded is null) return null;
            using var image = SKImage.FromBitmap(decoded);
            using var encoded = image.Encode(SKEncodedImageFormat.Webp, 76);
            if (encoded is null) return null;

            var temporaryPath = cachePath + ".tmp";
            await using (var output = File.Create(temporaryPath))
            {
                encoded.SaveTo(output);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporaryPath, cachePath, true);
            var result = new Uri(cachePath).AbsoluteUri;
            _displaySources[sourceKey] = result;
            return result;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            _decoder.Release();
            _pending.TryRemove(cachePath, out _);
        }
    }

    private string GetCachePath(string source, string localPath)
    {
        var file = new FileInfo(localPath);
        // Keep the original key format so existing thumbnails survive an app update.
        // Newly created entries use the lower-memory decoder above.
        var identity = $"{source}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
        return _cachePaths.GetOrAdd(identity, static (key, directory) =>
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
            return Path.Combine(directory, hash + ".webp");
        }, _cacheDirectory);
    }

    private static bool TryResolveLocalPath(string source, out string path)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme is "http" or "https")
            {
                path = string.Empty;
                return false;
            }
            if (uri.IsFile)
            {
                path = uri.LocalPath;
                return true;
            }
        }

        path = source;
        return Path.IsPathRooted(source);
    }
}
