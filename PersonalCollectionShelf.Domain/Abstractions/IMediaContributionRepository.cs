using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IMediaContributionRepository
{
    Task<IReadOnlyList<MediaContribution>> GetAllAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaContribution>> GetForItemAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaContribution>> GetForPersonAsync(Guid personId, string userId, CancellationToken cancellationToken = default);

    Task ReplaceForItemAsync(Guid mediaItemId, string userId, IReadOnlyCollection<MediaContribution> contributions, CancellationToken cancellationToken = default);
}
