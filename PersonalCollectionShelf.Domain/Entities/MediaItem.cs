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

    public string? Tags { get; set; }

    public Guid? CreatorId { get; set; }

    public Guid? StudioId { get; set; }

    public string? SerialNumber { get; set; }

    public MediaType MediaType { get; set; } = MediaType.Other;

    public MediaStatus Status { get; set; } = MediaStatus.Planned;

    public int? Rating { get; set; }

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
