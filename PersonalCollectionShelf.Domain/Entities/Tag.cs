namespace PersonalCollectionShelf.Domain.Entities;

using PersonalCollectionShelf.Domain.Enums;

public sealed class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TagKind Kind { get; set; } = TagKind.Tag;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }
}
