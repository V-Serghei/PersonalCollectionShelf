using System.Security.Cryptography;
using SkiaSharp;

namespace PersonalCollectionShelf.Infrastructure.Services.Sync;

public sealed class CloudImageCompressor
{
    private const int MaxDimension = 768;
    private const int WebpQuality = 78;

    public async Task<CompressedCloudImage> CompressAsync(string path, CancellationToken cancellationToken)
    {
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
        return new CompressedCloudImage(sourceHash, cloudHash, bytes);
    }
}

public sealed record CompressedCloudImage(string SourceHash, string CloudHash, byte[] Bytes);
