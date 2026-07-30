namespace PersonalCollectionShelf.Domain.Entities;

public sealed class GraphicPublicationDetails
{
    public Guid MediaItemId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? VolumeCount { get; set; }
    public int? ChapterOrIssueCount { get; set; }
    public string? ReadingDirection { get; set; }
    public bool? IsColor { get; set; }
    public string? PublicationStatus { get; set; }
    public string? Imprint { get; set; }
    public string? OriginalLanguage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
