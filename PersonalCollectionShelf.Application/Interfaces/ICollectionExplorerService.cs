using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface ICollectionExplorerService
{
    Task<IReadOnlyList<CollectionExplorerDto>> GetAllAsync(string userId, CancellationToken cancellationToken = default);

    Task<CollectionExplorerDto?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
