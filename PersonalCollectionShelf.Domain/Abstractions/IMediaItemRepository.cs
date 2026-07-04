using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IMediaItemRepository
{
    Task<IReadOnlyList<MediaItem>> GetAllAsync(string userId, CancellationToken cancellationToken = default);

    Task<MediaItem?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<MediaItem> AddAsync(MediaItem mediaItem, CancellationToken cancellationToken = default);

    Task<MediaItem> UpdateAsync(MediaItem mediaItem, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaItem>> SearchAsync(
        string userId,
        string? searchTerm,
        MediaType? mediaType,
        MediaStatus? status,
        CancellationToken cancellationToken = default);
}
