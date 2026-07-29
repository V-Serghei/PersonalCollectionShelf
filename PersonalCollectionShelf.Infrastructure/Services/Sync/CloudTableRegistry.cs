using PersonalCollectionShelf.Infrastructure.Persistence;

namespace PersonalCollectionShelf.Infrastructure.Services.Sync;

internal static class CloudTableRegistry
{
    private const string LocalUserId = "local-user";

    public static IReadOnlyList<ICloudTableAdapter> Create(LocalDatabaseService database) =>
    [
        new CloudTableAdapter<MediaItemRecord>(database, "mediaItem", row => row.Id,
            row => row.UserId == LocalUserId,
            row => row.UpdatedAt,
            row => row.DeletedAt is not null,
            row => row.CoverUrl = null,
            (row, existing) =>
            {
                row.UserId = LocalUserId;
                row.CoverUrl = existing?.CoverUrl;
            }),
        new CloudTableAdapter<PersonRecord>(database, "person", row => row.Id,
            row => row.UserId == LocalUserId,
            row => row.UpdatedAt,
            row => row.DeletedAt is not null,
            row => row.PhotoPath = null,
            (row, existing) =>
            {
                row.UserId = LocalUserId;
                row.PhotoPath = existing?.PhotoPath;
            }),
        new CloudTableAdapter<PersonPhotoRecord>(database, "personPhoto", row => row.Id,
            row => row.UserId == LocalUserId,
            row => row.CreatedAt,
            sanitizeForCloud: row => row.FilePath = string.Empty,
            prepareForLocal: (row, existing) =>
            {
                row.UserId = LocalUserId;
                row.FilePath = existing?.FilePath ?? string.Empty;
            }),
        new CloudTableAdapter<StudioRecord>(database, "studio", row => row.Id,
            row => row.UserId == LocalUserId,
            row => row.CreatedAt),
        new CloudTableAdapter<MediaCategoryRecord>(database, "mediaCategory", row => row.Id,
            row => !row.IsSystem && row.UserId == LocalUserId,
            row => row.UpdatedAt,
            row => row.DeletedAt is not null),
        new CloudTableAdapter<BookDetailsRecord>(database, "bookDetails", row => row.MediaItemId,
            row => row.UserId == LocalUserId, row => row.UpdatedAt),
        new CloudTableAdapter<MovieDetailsRecord>(database, "movieDetails", row => row.MediaItemId,
            row => row.UserId == LocalUserId, row => row.UpdatedAt),
        new CloudTableAdapter<EpisodicDetailsRecord>(database, "episodicDetails", row => row.MediaItemId,
            row => row.UserId == LocalUserId, row => row.UpdatedAt),
        new CloudTableAdapter<GraphicPublicationDetailsRecord>(database, "graphicDetails", row => row.MediaItemId,
            row => row.UserId == LocalUserId, row => row.UpdatedAt),
        new CloudTableAdapter<GameDetailsRecord>(database, "gameDetails", row => row.MediaItemId,
            row => row.UserId == LocalUserId, row => row.UpdatedAt),
        new CloudTableAdapter<TagRecord>(database, "tag", row => row.Id,
            row => row.UserId == LocalUserId, row => row.UpdatedAt, row => row.DeletedAt is not null),
        new CloudTableAdapter<MediaItemTagRecord>(database, "mediaItemTag", row => $"{row.MediaItemId}:{row.TagId}",
            row => row.UserId == LocalUserId,
            prepareForLocal: (row, existing) =>
            {
                row.UserId = LocalUserId;
                row.Id = existing?.Id ?? 0;
            }),
        new CloudTableAdapter<MediaContributionRecord>(database, "mediaContribution", row => row.Id,
            row => row.UserId == LocalUserId, row => row.UpdatedAt),
        new CloudTableAdapter<MediaStudioCreditRecord>(database, "mediaStudioCredit", row => row.Id,
            row => row.UserId == LocalUserId, row => row.UpdatedAt),
        new CloudTableAdapter<MediaCollectionRecord>(database, "mediaCollection", row => row.Id,
            row => row.UserId == LocalUserId, row => row.UpdatedAt, row => row.DeletedAt is not null),
        new CloudTableAdapter<MediaCollectionEntryRecord>(database, "mediaCollectionEntry", row => row.Id,
            row => row.UserId == LocalUserId, row => row.UpdatedAt),
        new CloudTableAdapter<MediaRelationRecord>(database, "mediaRelation", row => row.Id,
            row => row.UserId == LocalUserId, row => row.UpdatedAt),
        new CloudTableAdapter<PersonRelationRecord>(database, "personRelation", row => row.Id,
            row => row.UserId == LocalUserId, row => row.UpdatedAt),
        new CloudTableAdapter<PersonProfessionRecord>(database, "personProfession", row => row.Id,
            row => row.UserId == LocalUserId)
    ];
}
