namespace PersonalCollectionShelf.Application.Interfaces;

public interface IGoogleDriveBackupService
{
    Task<string> UploadBackupAsync(
        string fileName,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    Task<DriveBackupFileDto?> DownloadLatestBackupAsync(CancellationToken cancellationToken = default);
}

public sealed record DriveBackupFileDto(string Name, byte[] Content, DateTime CreatedAtUtc);
