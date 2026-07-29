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
            LastSyncedAt = await firestoreSyncService.GetLastSyncedAtAsync(cancellationToken),
            MessageKey = !firestoreSyncService.IsConfigured
                ? "Sync.Status.NotConfigured"
                : isSignedIn
                    ? "Sync.Status.Ready"
                    : "Sync.Status.SignInRequired"
        };
    }

    public async Task RequestSyncAsync(CancellationToken cancellationToken = default)
    {
        if (!await authService.IsSignedInAsync(cancellationToken))
        {
            throw new InvalidOperationException("Sign in before synchronization.");
        }

        await firestoreSyncService.SyncAsync(cancellationToken);
    }
}
