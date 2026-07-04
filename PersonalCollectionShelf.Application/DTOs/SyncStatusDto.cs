namespace PersonalCollectionShelf.Application.DTOs;

public sealed record SyncStatusDto
{
    public bool IsEnabled { get; init; }

    public bool IsSignedIn { get; init; }

    public DateTime? LastSyncedAt { get; init; }

    public string MessageKey { get; init; } = "Sync.Status.NotConfigured";
}
