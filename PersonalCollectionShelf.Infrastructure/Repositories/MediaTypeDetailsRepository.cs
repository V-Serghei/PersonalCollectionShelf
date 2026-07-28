using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Infrastructure.Persistence;

namespace PersonalCollectionShelf.Infrastructure.Repositories;

public sealed class MediaTypeDetailsRepository(LocalDatabaseService databaseService) : IMediaTypeDetailsRepository
{
    public async Task<EpisodicDetails?> GetEpisodicAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var value = await databaseService.Connection.FindAsync<EpisodicDetailsRecord>(mediaItemId.ToString());
        return value is null || value.UserId != userId ? null : new EpisodicDetails
        {
            MediaItemId = mediaItemId, UserId = userId, SeasonCount = value.SeasonCount,
            EpisodeCount = value.EpisodeCount, EpisodeRuntimeMinutes = value.EpisodeRuntimeMinutes,
            Network = value.Network, AiringStatus = value.AiringStatus, SourceMaterial = value.SourceMaterial,
            OriginalLanguage = value.OriginalLanguage, CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt
        };
    }

    public async Task UpsertEpisodicAsync(EpisodicDetails value, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.InsertOrReplaceAsync(new EpisodicDetailsRecord
        {
            MediaItemId = value.MediaItemId.ToString(), UserId = value.UserId, SeasonCount = value.SeasonCount,
            EpisodeCount = value.EpisodeCount, EpisodeRuntimeMinutes = value.EpisodeRuntimeMinutes,
            Network = value.Network, AiringStatus = value.AiringStatus, SourceMaterial = value.SourceMaterial,
            OriginalLanguage = value.OriginalLanguage, CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt
        });
    }

    public async Task<GraphicPublicationDetails?> GetGraphicPublicationAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var value = await databaseService.Connection.FindAsync<GraphicPublicationDetailsRecord>(mediaItemId.ToString());
        return value is null || value.UserId != userId ? null : new GraphicPublicationDetails
        {
            MediaItemId = mediaItemId, UserId = userId, VolumeCount = value.VolumeCount,
            ChapterOrIssueCount = value.ChapterOrIssueCount, ReadingDirection = value.ReadingDirection,
            IsColor = value.IsColor, PublicationStatus = value.PublicationStatus, Imprint = value.Imprint,
            OriginalLanguage = value.OriginalLanguage, CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt
        };
    }

    public async Task UpsertGraphicPublicationAsync(GraphicPublicationDetails value, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.InsertOrReplaceAsync(new GraphicPublicationDetailsRecord
        {
            MediaItemId = value.MediaItemId.ToString(), UserId = value.UserId, VolumeCount = value.VolumeCount,
            ChapterOrIssueCount = value.ChapterOrIssueCount, ReadingDirection = value.ReadingDirection,
            IsColor = value.IsColor, PublicationStatus = value.PublicationStatus, Imprint = value.Imprint,
            OriginalLanguage = value.OriginalLanguage, CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt
        });
    }

    public async Task<GameDetails?> GetGameAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var value = await databaseService.Connection.FindAsync<GameDetailsRecord>(mediaItemId.ToString());
        return value is null || value.UserId != userId ? null : new GameDetails
        {
            MediaItemId = mediaItemId, UserId = userId, Platform = value.Platform,
            MainStoryHours = value.MainStoryHours, CompletionistHours = value.CompletionistHours,
            GameMode = value.GameMode, Engine = value.Engine, Region = value.Region,
            CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt
        };
    }

    public async Task UpsertGameAsync(GameDetails value, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.InsertOrReplaceAsync(new GameDetailsRecord
        {
            MediaItemId = value.MediaItemId.ToString(), UserId = value.UserId, Platform = value.Platform,
            MainStoryHours = value.MainStoryHours, CompletionistHours = value.CompletionistHours,
            GameMode = value.GameMode, Engine = value.Engine, Region = value.Region,
            CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt
        });
    }
}
