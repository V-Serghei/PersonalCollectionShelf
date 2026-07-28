using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IMediaStudioCreditRepository
{
    Task<IReadOnlyList<MediaStudioCredit>> GetForItemAsync(
        Guid mediaItemId,
        string userId,
        CancellationToken cancellationToken = default);

    Task ReplaceForItemAsync(
        Guid mediaItemId,
        string userId,
        IReadOnlyCollection<MediaStudioCredit> credits,
        CancellationToken cancellationToken = default);
}
