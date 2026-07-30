using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IPersonRelationRepository
{
    Task<IReadOnlyList<PersonRelation>> GetForPersonAsync(Guid personId, string userId, CancellationToken cancellationToken = default);

    Task<PersonRelation> UpsertAsync(PersonRelation relation, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
