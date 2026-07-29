namespace PersonalCollectionShelf.Application.Interfaces;

public interface ICloudAssetStore
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task UploadAsync(
        string objectName,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<byte[]> DownloadAsync(string objectName, CancellationToken cancellationToken = default);
}
