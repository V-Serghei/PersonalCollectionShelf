using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IStudioRepository
{
    Task<Studio?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<Studio?> GetByNameAsync(string userId, string name, CancellationToken cancellationToken = default);

    Task<Studio> AddAsync(Studio studio, CancellationToken cancellationToken = default);
}
