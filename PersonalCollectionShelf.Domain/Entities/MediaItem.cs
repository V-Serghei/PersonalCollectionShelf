using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Domain.Entities;

public sealed class MediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? OriginalTitle { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public Guid? CategoryId { get; set; }

    public string? Tags { get; set; }

    public Guid? CreatorId { get; set; }

    public Guid? StudioId { get; set; }

    public string? SerialNumber { get; set; }

    public int? TmdbId { get; set; }

    public string? ImdbId { get; set; }

    public int? KinopoiskId { get; set; }

    public decimal? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public decimal? ImdbRating { get; set; }

    public int? ImdbVoteCount { get; set; }

    public decimal? KinopoiskRating { get; set; }

    public int? KinopoiskVoteCount { get; set; }

    public DateTime? ExternalRatingsUpdatedAt { get; set; }

    public MediaType MediaType { get; set; } = MediaType.Other;

    public MediaStatus Status { get; set; } = MediaStatus.Planned;

    public decimal? Rating { get; set; }

    public int ProgressCurrent { get; set; }

    public int? ProgressTotal { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? FinishDate { get; set; }

    public int? ReleaseYear { get; set; }

    public string? CoverUrl { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public void Touch(DateTime? utcNow = null)
    {
        UpdatedAt = utcNow ?? DateTime.UtcNow;
    }

    public void MarkDeleted(DateTime? utcNow = null)
    {
        var timestamp = utcNow ?? DateTime.UtcNow;
        DeletedAt = timestamp;
        UpdatedAt = timestamp;
    }
}
