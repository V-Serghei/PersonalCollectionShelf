using SQLite;

namespace PersonalCollectionShelf.Infrastructure.Persistence;

[Table("MediaCategories")]
public sealed class MediaCategoryRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string? UserId { get; set; }

    [Indexed]
    public string Key { get; set; } = string.Empty;

    [Indexed]
    public string NormalizedName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int? BaseMediaType { get; set; }
    public bool IsSystem { get; set; }
    public string? FieldSchemaJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

[Table("BookDetails")]
public sealed class BookDetailsRecord
{
    [PrimaryKey]
    public string MediaItemId { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string? PublisherId { get; set; }

    public string? Subtitle { get; set; }
    public string? Edition { get; set; }
    public int? EditionNumber { get; set; }
    public int? EditionYear { get; set; }
    public int? OriginalPublicationYear { get; set; }
    public int? TranslationYear { get; set; }
    public string? OriginalLanguage { get; set; }
    public string? Language { get; set; }
    public int? PageCount { get; set; }
    public string? Isbn10 { get; set; }
    public string? Isbn13 { get; set; }
    public int? Format { get; set; }
    public string? Binding { get; set; }
    public string? CountryOfOrigin { get; set; }
    public string? AgeRating { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("MovieDetails")]
public sealed class MovieDetailsRecord
{
    [PrimaryKey]
    public string MediaItemId { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }
    public string? OriginalLanguage { get; set; }
    public string? Language { get; set; }
    public string? CountryOfOrigin { get; set; }
    public string? AgeRating { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("EpisodicDetails")]
public sealed class EpisodicDetailsRecord
{
    [PrimaryKey] public string MediaItemId { get; set; } = string.Empty;
    [Indexed] public string UserId { get; set; } = string.Empty;
    public int? SeasonCount { get; set; }
    public int? EpisodeCount { get; set; }
    public int? EpisodeRuntimeMinutes { get; set; }
    public string? Network { get; set; }
    public string? AiringStatus { get; set; }
    public string? SourceMaterial { get; set; }
    public string? OriginalLanguage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("GraphicPublicationDetails")]
public sealed class GraphicPublicationDetailsRecord
{
    [PrimaryKey] public string MediaItemId { get; set; } = string.Empty;
    [Indexed] public string UserId { get; set; } = string.Empty;
    public int? VolumeCount { get; set; }
    public int? ChapterOrIssueCount { get; set; }
    public string? ReadingDirection { get; set; }
    public bool? IsColor { get; set; }
    public string? PublicationStatus { get; set; }
    public string? Imprint { get; set; }
    public string? OriginalLanguage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("GameDetails")]
public sealed class GameDetailsRecord
{
    [PrimaryKey] public string MediaItemId { get; set; } = string.Empty;
    [Indexed] public string UserId { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public decimal? MainStoryHours { get; set; }
    public decimal? CompletionistHours { get; set; }
    public string? GameMode { get; set; }
    public string? Engine { get; set; }
    public string? Region { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("Tags")]
public sealed class TagRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string NormalizedName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int Kind { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

[Table("MediaItemTags")]
public sealed class MediaItemTagRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string MediaItemId { get; set; } = string.Empty;

    [Indexed]
    public string TagId { get; set; } = string.Empty;
}

[Table("MediaContributions")]
public sealed class MediaContributionRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string MediaItemId { get; set; } = string.Empty;

    [Indexed]
    public string PersonId { get; set; } = string.Empty;

    [Indexed]
    public string CreditRoleId { get; set; } = string.Empty;

    [Indexed]
    public int Role { get; set; }

    public int SortOrder { get; set; }
    public string? Details { get; set; }
    public string? CreditedAs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("MediaStudioCredits")]
public sealed class MediaStudioCreditRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string MediaItemId { get; set; } = string.Empty;

    [Indexed]
    public string StudioId { get; set; } = string.Empty;

    [Indexed]
    public int Role { get; set; }

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("CreditRoles")]
public sealed class CreditRoleRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
}

[Table("MediaCollections")]
public sealed class MediaCollectionRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string NormalizedName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int Kind { get; set; }
    public string? Description { get; set; }
    [Indexed]
    public string? ParentCollectionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

[Table("MediaCollectionEntries")]
public sealed class MediaCollectionEntryRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string CollectionId { get; set; } = string.Empty;

    [Indexed]
    public string MediaItemId { get; set; } = string.Empty;

    public double? Position { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("MediaRelations")]
public sealed class MediaRelationRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string FromItemId { get; set; } = string.Empty;

    [Indexed]
    public string ToItemId { get; set; } = string.Empty;

    public int Kind { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("PersonRelations")]
public sealed class PersonRelationRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string PersonId { get; set; } = string.Empty;

    [Indexed]
    public string RelatedPersonId { get; set; } = string.Empty;

    public int Kind { get; set; }
    public int? InverseKind { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("Professions")]
public sealed class ProfessionRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
}

[Table("PersonProfessions")]
public sealed class PersonProfessionRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string PersonId { get; set; } = string.Empty;

    [Indexed]
    public string ProfessionId { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
