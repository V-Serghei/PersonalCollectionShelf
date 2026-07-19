using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.Services;

public sealed class TagService(ITagRepository tagRepository) : ITagService
{
    public async Task<IReadOnlyList<TagDto>> SearchAsync(string userId, string? searchTerm, TagKind kind, int limit = 20, CancellationToken cancellationToken = default)
    {
        var tags = await tagRepository.SearchAsync(userId, searchTerm, kind, limit, cancellationToken);
        return tags.Select(ToDto).ToList();
    }

    public async Task<TagDto> GetOrCreateAsync(string userId, string name, TagKind kind, CancellationToken cancellationToken = default)
    {
        var tag = await tagRepository.GetOrCreateAsync(userId, name, kind, cancellationToken);
        return ToDto(tag);
    }

    private static TagDto ToDto(Tag tag) => new(tag.Id, tag.Name, tag.Kind);
}
