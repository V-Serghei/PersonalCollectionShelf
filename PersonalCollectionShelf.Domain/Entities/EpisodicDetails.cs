namespace PersonalCollectionShelf.Domain.Entities;

public sealed class EpisodicDetails
{
    public Guid MediaItemId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? SeasonCount { get; set; }
    public int? EpisodeCount { get; set; }
    public int? EpisodeRuntimeMinutes { get; set; }
    public string? Network { get; set; }
    public string? AiringStatus { get; set; }
    public string? SourceMaterial { get; set; }
    public string? OriginalLanguage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
