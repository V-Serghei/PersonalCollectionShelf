using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IMediaRelationRepository
{
    Task<IReadOnlyList<MediaRelation>> GetForItemAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default);

    Task<MediaRelation> AddAsync(MediaRelation relation, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task ReplaceFromItemAsync(Guid mediaItemId, string userId, IReadOnlyCollection<MediaRelation> relations, CancellationToken cancellationToken = default);
}
