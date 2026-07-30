using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Domain.Entities;

public sealed class MediaRelation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public Guid FromItemId { get; set; }

    public Guid ToItemId { get; set; }

    public MediaRelationKind Kind { get; set; } = MediaRelationKind.Other;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
