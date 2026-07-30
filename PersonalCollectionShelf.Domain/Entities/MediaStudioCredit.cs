using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Domain.Entities;

public sealed class MediaStudioCredit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public Guid MediaItemId { get; set; }

    public Guid StudioId { get; set; }

    public StudioRole Role { get; set; } = StudioRole.ProductionCompany;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
