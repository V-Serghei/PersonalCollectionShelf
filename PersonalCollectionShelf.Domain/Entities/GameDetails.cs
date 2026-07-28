namespace PersonalCollectionShelf.Domain.Entities;

public sealed class GameDetails
{
    public Guid MediaItemId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public decimal? MainStoryHours { get; set; }
    public decimal? CompletionistHours { get; set; }
    public string? GameMode { get; set; }
    public string? Engine { get; set; }
    public string? Region { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
