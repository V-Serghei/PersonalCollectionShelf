namespace PersonalCollectionShelf.Application.DTOs;

public sealed record SyncStatusDto
{
    public bool IsEnabled { get; init; }

    public bool IsSignedIn { get; init; }

    public DateTime? LastSyncedAt { get; init; }

    public string MessageKey { get; init; } = "Sync.Status.NotConfigured";
}

public sealed record SyncProgressDto(
    int Percent,
    string MessageKey,
    int Completed = 0,
    int Total = 0);

public sealed record SyncResultDto(int FailedAssetCount = 0)
{
    public bool HasWarnings => FailedAssetCount > 0;
}
