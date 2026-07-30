using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.Infrastructure.Services.Sync;

internal interface ICloudTableAdapter
{
    string EntityType { get; }

    Task<IReadOnlyList<LocalCloudEntity>> ReadAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<LocalCloudEntity>> ReadAsync(
        IReadOnlyCollection<string> entityKeys,
        CancellationToken cancellationToken);

    Task ApplyAsync(CloudDocument document, CancellationToken cancellationToken);
}

internal sealed record LocalCloudEntity(
    string EntityType,
    string EntityKey,
    string Payload,
    string ContentHash,
    DateTime EntityUpdatedAtUtc,
    bool IsDeleted);
