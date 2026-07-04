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
    public int MediaType { get; set; }

    [Indexed]
    public int Status { get; set; }

    public int? Rating { get; set; }

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
