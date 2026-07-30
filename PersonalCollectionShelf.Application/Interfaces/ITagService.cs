using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface ITagService
{
    Task<IReadOnlyList<TagDto>> SearchAsync(string userId, string? searchTerm, TagKind kind, int limit = 20, CancellationToken cancellationToken = default);

    Task<TagDto> GetOrCreateAsync(string userId, string name, TagKind kind, CancellationToken cancellationToken = default);
}
