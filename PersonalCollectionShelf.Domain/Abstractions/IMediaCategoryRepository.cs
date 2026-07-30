using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IMediaCategoryRepository
{
    Task<IReadOnlyList<MediaCategory>> GetAllAsync(string userId, CancellationToken cancellationToken = default);

    Task<MediaCategory?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<MediaCategory> GetSystemAsync(MediaType mediaType, CancellationToken cancellationToken = default);

    Task<MediaCategory> GetOrCreateCustomAsync(string userId, string name, MediaType? baseMediaType, CancellationToken cancellationToken = default);

    Task<MediaCategory> UpdateAsync(MediaCategory category, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
