using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface IPersonService
{
    Task<PersonDto?> GetByIdAsync(string userId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonDto>> GetByIdsAsync(string userId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<PersonDto> GetOrCreateAsync(string userId, string name, CancellationToken cancellationToken = default);
}
