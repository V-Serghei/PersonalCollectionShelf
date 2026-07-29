using SQLite;

namespace PersonalCollectionShelf.Infrastructure.Persistence;

[Table("MediaItems")]
public sealed class MediaItemRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    [Indexed]
    public string Title { get; set; } = string.Empty;

    public string? OriginalTitle { get; set; }

    public string? Description { get; set; }

    [Indexed]
    public string? Category { get; set; }

    [Indexed]
    public string? CategoryId { get; set; }

    public string? Tags { get; set; }

    [Indexed]
    public string? CreatorId { get; set; }

    [Indexed]
    public string? StudioId { get; set; }

    public string? SerialNumber { get; set; }

    [Indexed]
    public int? TmdbId { get; set; }

    [Indexed]
    public string? ImdbId { get; set; }

    [Indexed]
    public int? KinopoiskId { get; set; }

    public decimal? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public decimal? ImdbRating { get; set; }

    public int? ImdbVoteCount { get; set; }

    public decimal? KinopoiskRating { get; set; }

    public int? KinopoiskVoteCount { get; set; }

    public DateTime? ExternalRatingsUpdatedAt { get; set; }
    public string? CatalogProvider { get; set; }
    public string? CatalogItemId { get; set; }
    public string? CatalogSourceUrl { get; set; }
    public string? CatalogRatingPrimarySource { get; set; }
    public decimal? CatalogRatingPrimary { get; set; }
    public int? CatalogRatingPrimaryCount { get; set; }
    public string? CatalogRatingSecondarySource { get; set; }
    public decimal? CatalogRatingSecondary { get; set; }
    public int? CatalogRatingSecondaryCount { get; set; }

    [Indexed]
    public int MediaType { get; set; }

    [Indexed]
    public int Status { get; set; }

    public decimal? Rating { get; set; }

    public int ProgressCurrent { get; set; }

    public int? ProgressTotal { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? FinishDate { get; set; }

    public int? ReleaseYear { get; set; }

    public string? CoverUrl { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [Indexed]
    public DateTime? DeletedAt { get; set; }

    public bool IsFavorite { get; set; }
}
