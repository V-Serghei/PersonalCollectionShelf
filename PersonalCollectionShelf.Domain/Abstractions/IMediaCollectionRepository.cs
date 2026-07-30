using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IMediaCollectionRepository
{
    Task<IReadOnlyList<MediaCollection>> GetAllAsync(string userId, CancellationToken cancellationToken = default);

    Task<MediaCollection?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<MediaCollection?> GetByNameAsync(string userId, string name, CancellationToken cancellationToken = default);

    Task<MediaCollection> AddAsync(MediaCollection collection, CancellationToken cancellationToken = default);

    Task UpdateAsync(MediaCollection collection, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaCollectionEntry>> GetEntriesAsync(Guid collectionId, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaCollectionEntry>> GetAllEntriesAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaCollectionEntry>> GetEntriesForItemAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default);

    Task UpsertEntryAsync(MediaCollectionEntry entry, CancellationToken cancellationToken = default);

    Task RemoveEntryAsync(Guid entryId, string userId, CancellationToken cancellationToken = default);

    Task ReplaceEntryForItemAsync(Guid mediaItemId, string userId, MediaCollectionEntry? entry, CancellationToken cancellationToken = default);
}
