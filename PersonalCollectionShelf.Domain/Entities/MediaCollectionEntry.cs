namespace PersonalCollectionShelf.Domain.Entities;

public sealed class MediaCollectionEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public Guid CollectionId { get; set; }

    public Guid MediaItemId { get; set; }

    public double? Position { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
