using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface IStudioService
{
    Task<StudioDto?> GetByIdAsync(string userId, Guid id, CancellationToken cancellationToken = default);

    Task<StudioDto> GetOrCreateAsync(string userId, string name, CancellationToken cancellationToken = default);
}
