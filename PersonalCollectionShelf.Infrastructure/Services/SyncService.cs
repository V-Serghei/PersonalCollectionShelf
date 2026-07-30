using System.Net;
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

    public async Task<SyncResultDto> RequestSyncAsync(
        IProgress<SyncProgressDto>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!await authService.IsSignedInAsync(cancellationToken))
        {
            throw new InvalidOperationException("Sign in before synchronization.");
        }

        var trackedProgress = new TrackingProgress(progress);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await firestoreSyncService.SyncAsync(trackedProgress, cancellationToken);
            }
            catch (Exception exception) when (
                attempt < 3 &&
                IsTransient(exception, cancellationToken))
            {
                trackedProgress.Report(new SyncProgressDto(
                    trackedProgress.LatestPercent,
                    "Sync.Progress.Retrying"));
                await Task.Delay(TimeSpan.FromMilliseconds(700 * attempt), cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or WebException or IOException ||
        exception is OperationCanceledException && !cancellationToken.IsCancellationRequested ||
        exception.InnerException is not null && IsTransient(exception.InnerException, cancellationToken);

    private sealed class TrackingProgress(IProgress<SyncProgressDto>? target) : IProgress<SyncProgressDto>
    {
        public int LatestPercent { get; private set; }

        public void Report(SyncProgressDto value)
        {
            LatestPercent = Math.Max(LatestPercent, value.Percent);
            target?.Report(value with { Percent = LatestPercent });
        }
    }
}
