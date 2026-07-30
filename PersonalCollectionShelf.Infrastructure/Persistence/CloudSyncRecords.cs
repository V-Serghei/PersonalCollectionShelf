using SQLite;

namespace PersonalCollectionShelf.Infrastructure.Persistence;

[Table("CloudEntityStates")]
public sealed class CloudEntityStateRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string EntityType { get; set; } = string.Empty;

    [Indexed]
    public string EntityKey { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;
    public string? CloudHash { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}

[Table("CloudSyncMetadata")]
public sealed class CloudSyncMetadataRecord
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }
}

[Table("CloudDirtyEntities")]
public sealed class CloudDirtyEntityRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string EntityType { get; set; } = string.Empty;

    [Indexed]
    public string EntityKey { get; set; } = string.Empty;

    public long Revision { get; set; }
}

[Table("CloudAssetStates")]
public sealed class CloudAssetStateRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string OwnerType { get; set; } = string.Empty;

    [Indexed]
    public string OwnerId { get; set; } = string.Empty;

    public string Slot { get; set; } = string.Empty;
    public string? OriginalPath { get; set; }
    public string? CachePath { get; set; }
    public string? SourceHash { get; set; }
    public string? SourceFingerprint { get; set; }
    public string? CloudObjectName { get; set; }
    public string? CloudHash { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
