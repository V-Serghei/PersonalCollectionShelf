using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface ISyncService
{
    Task<SyncStatusDto> GetSyncStatusAsync(CancellationToken cancellationToken = default);

    Task<SyncResultDto> RequestSyncAsync(
        IProgress<SyncProgressDto>? progress = null,
        CancellationToken cancellationToken = default);
}
