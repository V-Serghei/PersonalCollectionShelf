using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Domain.Entities;

public sealed class MediaContribution
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public Guid MediaItemId { get; set; }

    public Guid PersonId { get; set; }

    public Guid CreditRoleId { get; set; }

    public ContributionRole Role { get; set; } = ContributionRole.Other;

    public int SortOrder { get; set; }

    public string? Details { get; set; }

    public string? CreditedAs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
