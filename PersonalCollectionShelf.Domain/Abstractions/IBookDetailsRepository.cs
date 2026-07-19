using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IBookDetailsRepository
{
    Task<BookDetails?> GetAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default);

    Task UpsertAsync(BookDetails details, CancellationToken cancellationToken = default);
}
