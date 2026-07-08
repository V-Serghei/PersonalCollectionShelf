using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Application.Services;

public sealed class StudioService(IStudioRepository studioRepository) : IStudioService
{
    public async Task<StudioDto?> GetByIdAsync(string userId, Guid id, CancellationToken cancellationToken = default)
    {
        var studio = await studioRepository.GetByIdAsync(id, userId, cancellationToken);
        return studio is null ? null : ToDto(studio);
    }

    public async Task<StudioDto> GetOrCreateAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        var existing = await studioRepository.GetByNameAsync(userId, trimmedName, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var studio = new Studio
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmedName,
            CreatedAt = DateTime.UtcNow
        };

        var created = await studioRepository.AddAsync(studio, cancellationToken);
        return ToDto(created);
    }

    private static StudioDto ToDto(Studio studio)
    {
        return new StudioDto(studio.Id, studio.Name);
    }
}
