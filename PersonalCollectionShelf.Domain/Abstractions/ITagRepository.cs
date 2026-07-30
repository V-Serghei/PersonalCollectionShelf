using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> GetAllAsync(string userId, TagKind? kind = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> GetForItemAsync(Guid mediaItemId, string userId, TagKind? kind = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Tag>>> GetForItemsAsync(
        IReadOnlyCollection<Guid> mediaItemIds,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tag>> SearchAsync(string userId, string? searchTerm, TagKind kind, int limit = 20, CancellationToken cancellationToken = default);

    Task<Tag> GetOrCreateAsync(string userId, string name, TagKind kind, CancellationToken cancellationToken = default);

    Task SetForItemAsync(Guid mediaItemId, string userId, IReadOnlyCollection<string> tagNames, TagKind kind = TagKind.Tag, CancellationToken cancellationToken = default);
}
