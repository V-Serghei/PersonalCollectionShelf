using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IPersonPhotoRepository
{
    Task<IReadOnlyList<PersonPhoto>> GetForPersonAsync(Guid personId, string userId, CancellationToken cancellationToken = default);
    Task<PersonPhoto> AddAsync(PersonPhoto photo, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid photoId, string userId, CancellationToken cancellationToken = default);
    Task SetPrimaryAsync(Guid personId, Guid photoId, string userId, CancellationToken cancellationToken = default);
}
