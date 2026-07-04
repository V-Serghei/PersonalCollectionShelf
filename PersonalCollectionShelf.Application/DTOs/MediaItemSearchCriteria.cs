using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.DTOs;

public sealed record MediaItemSearchCriteria
{
    public string UserId { get; init; } = string.Empty;

    public string? SearchTerm { get; init; }

    public MediaType? MediaType { get; init; }

    public MediaStatus? Status { get; init; }
}
