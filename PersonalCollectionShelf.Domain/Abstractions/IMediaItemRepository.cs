using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IMediaItemRepository
{
    Task<IReadOnlyList<MediaItem>> GetAllAsync(string userId, CancellationToken cancellationToken = default);

    Task<int> CountAsync(string userId, CancellationToken cancellationToken = default);

    Task<MediaItem?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<MediaItem> AddAsync(MediaItem mediaItem, CancellationToken cancellationToken = default);

    Task<MediaItem> UpdateAsync(MediaItem mediaItem, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaItem>> SearchAsync(
        string userId,
        string? searchTerm,
        MediaType? mediaType,
        MediaStatus? status,
        string? category,
        string? tag,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetCastPersonIdsAsync(Guid mediaItemId, CancellationToken cancellationToken = default);

    Task ReplaceCastAsync(Guid mediaItemId, IReadOnlyList<Guid> personIds, CancellationToken cancellationToken = default);
}
