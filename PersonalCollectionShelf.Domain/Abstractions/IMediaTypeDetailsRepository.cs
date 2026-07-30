using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Domain.Abstractions;

public interface IMediaTypeDetailsRepository
{
    Task<EpisodicDetails?> GetEpisodicAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default);
    Task UpsertEpisodicAsync(EpisodicDetails details, CancellationToken cancellationToken = default);
    Task<GraphicPublicationDetails?> GetGraphicPublicationAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default);
    Task UpsertGraphicPublicationAsync(GraphicPublicationDetails details, CancellationToken cancellationToken = default);
    Task<GameDetails?> GetGameAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default);
    Task UpsertGameAsync(GameDetails details, CancellationToken cancellationToken = default);
}
