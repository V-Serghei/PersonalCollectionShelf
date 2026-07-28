using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IMovieDetailsRepository
{
    Task<MovieDetails?> GetAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default);

    Task UpsertAsync(MovieDetails details, CancellationToken cancellationToken = default);
}
