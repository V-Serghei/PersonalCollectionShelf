using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IProfessionRepository
{
    Task<IReadOnlyList<Profession>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Profession> GetOrCreateAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Profession>> GetForPersonAsync(Guid personId, string userId, CancellationToken cancellationToken = default);

    Task SetForPersonAsync(Guid personId, string userId, IReadOnlyCollection<string> professionNames, CancellationToken cancellationToken = default);
}
