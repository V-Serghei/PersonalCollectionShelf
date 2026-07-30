using PersonalCollectionShelf.Application.Interfaces;

namespace PersonalCollectionShelf.Infrastructure.Services.Sync;

internal sealed class DisabledCloudAssetStore : ICloudAssetStore
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> ExistsAsync(string objectName, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task UploadAsync(
        string objectName,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<byte[]> DownloadAsync(string objectName, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Cloud image storage is not available.");
}
