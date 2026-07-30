namespace PersonalCollectionShelf.Application.Interfaces;

public interface IGoogleDriveBackupService
{
    Task<string> UploadBackupAsync(
        string fileName,
        ReadOnlyMemory<byte> content,
        IProgress<DriveTransferProgressDto>? progress = null,
        CancellationToken cancellationToken = default);

    Task<DriveBackupFileDto?> DownloadLatestBackupAsync(
        IProgress<DriveTransferProgressDto>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record DriveBackupFileDto(string Name, byte[] Content, DateTime CreatedAtUtc);

public sealed record DriveTransferProgressDto(long TransferredBytes, long? TotalBytes);
