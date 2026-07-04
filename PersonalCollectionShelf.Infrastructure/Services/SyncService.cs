using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;

namespace PersonalCollectionShelf.Infrastructure.Services;

public sealed class SyncService(FirestoreSyncService firestoreSyncService, IAuthService authService) : ISyncService
{
    public async Task<SyncStatusDto> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        var isSignedIn = await authService.IsSignedInAsync(cancellationToken);

        return new SyncStatusDto
        {
            IsEnabled = firestoreSyncService.IsConfigured,
            IsSignedIn = isSignedIn,
            LastSyncedAt = null,
            MessageKey = "Sync.Status.NotConfigured"
        };
    }

    public Task RequestSyncAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
