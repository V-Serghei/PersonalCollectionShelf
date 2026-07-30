using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PersonalCollectionShelf.Infrastructure.Persistence;
using SkiaSharp;

namespace PersonalCollectionShelf.Infrastructure.Services.Sync;

public sealed class CloudImageCompressor(LocalDatabaseService database)
{
    private const int MaxDimension = 768;
    private const int WebpQuality = 78;
    private readonly string _cacheDirectory = Path.Combine(
        Path.GetDirectoryName(database.DatabasePath) ?? AppContext.BaseDirectory,
        "compressed-image-cache");

    public async Task<string?> TryGetCachedObjectNameAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var (imageCachePath, metadataCachePath) = GetCachePaths(path);
        if (!File.Exists(imageCachePath) || !File.Exists(metadataCachePath)) return null;
        try
        {
            var metadata = JsonSerializer.Deserialize<CompressionCacheMetadata>(
                await File.ReadAllTextAsync(metadataCachePath, cancellationToken));
            return metadata is null ? null : $"{metadata.SourceHash}.webp";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<CompressedCloudImage> CompressAsync(string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_cacheDirectory);
        var (imageCachePath, metadataCachePath) = GetCachePaths(path);
        if (File.Exists(imageCachePath) && File.Exists(metadataCachePath))
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<CompressionCacheMetadata>(
                    await File.ReadAllTextAsync(metadataCachePath, cancellationToken));
                if (metadata is not null)
                {
                    return new CompressedCloudImage(
                        metadata.SourceHash,
                        metadata.CloudHash,
                        await File.ReadAllBytesAsync(imageCachePath, cancellationToken));
                }
            }
            catch (JsonException)
            {
                // Rebuild a corrupt cache entry below.
            }
        }

        await using var input = File.OpenRead(path);
        var sourceHash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
        input.Position = 0;

        using var source = SKBitmap.Decode(input)
            ?? throw new InvalidDataException($"Unsupported image file: {path}");
        var scale = Math.Min(1d, MaxDimension / (double)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var resized = scale < 1d
            ? source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            : null;
        using var image = SKImage.FromBitmap(resized ?? source);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebpQuality)
            ?? throw new InvalidDataException($"Unable to encode image: {path}");
        var bytes = encoded.ToArray();
        var cloudHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        await File.WriteAllBytesAsync(imageCachePath, bytes, cancellationToken);
        await File.WriteAllTextAsync(
            metadataCachePath,
            JsonSerializer.Serialize(new CompressionCacheMetadata(sourceHash, cloudHash)),
            cancellationToken);
        return new CompressedCloudImage(sourceHash, cloudHash, bytes);
    }

    private (string ImagePath, string MetadataPath) GetCachePaths(string path)
    {
        var sourceInfo = new FileInfo(path);
        var fingerprint = $"{Path.GetFullPath(path)}|{sourceInfo.Length}|{sourceInfo.LastWriteTimeUtc.Ticks}";
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))).ToLowerInvariant();
        return (
            Path.Combine(_cacheDirectory, cacheKey + ".webp"),
            Path.Combine(_cacheDirectory, cacheKey + ".json"));
    }

    private sealed record CompressionCacheMetadata(string SourceHash, string CloudHash);
}

public sealed record CompressedCloudImage(string SourceHash, string CloudHash, byte[] Bytes);
