using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface IPersonService
{
    Task<PersonDto?> GetByIdAsync(string userId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonDto>> GetByIdsAsync(string userId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonDto>> SearchAsync(string userId, string? searchTerm, int limit = 20, CancellationToken cancellationToken = default);

    Task<PersonDto> CreateAsync(string userId, string displayName, CancellationToken cancellationToken = default);

    Task<PersonDto> GetOrCreateAsync(string userId, string name, CancellationToken cancellationToken = default);
}
