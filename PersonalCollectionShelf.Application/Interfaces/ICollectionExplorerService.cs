using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface ICollectionExplorerService
{
    Task<IReadOnlyList<CollectionExplorerDto>> GetAllAsync(string userId, CancellationToken cancellationToken = default);
}
