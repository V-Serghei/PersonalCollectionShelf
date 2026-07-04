namespace PersonalCollectionShelf.Infrastructure.Services;

public sealed class FirestoreSyncService
{
    public bool IsConfigured => false;

    public Task PullAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task PushAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
