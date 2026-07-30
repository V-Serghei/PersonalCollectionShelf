using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IPersonRepository
{
    Task<Person?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Person>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, string userId, CancellationToken cancellationToken = default);

    Task<Person?> GetByNameAsync(string userId, string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Person>> SearchAsync(string userId, string? searchTerm, int limit = 20, CancellationToken cancellationToken = default);

    Task<Person> AddAsync(Person person, CancellationToken cancellationToken = default);

    Task<Person> UpdateAsync(Person person, CancellationToken cancellationToken = default);
}
