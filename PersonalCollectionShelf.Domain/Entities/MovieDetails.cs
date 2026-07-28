namespace PersonalCollectionShelf.Domain.Entities;

public sealed class MovieDetails
{
    public Guid MediaItemId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public string? OriginalLanguage { get; set; }

    public string? Language { get; set; }

    public string? CountryOfOrigin { get; set; }

    public string? AgeRating { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
