using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.DTOs;

public sealed record StudioCreditInput
{
    public Guid? StudioId { get; init; }

    public string Name { get; init; } = string.Empty;

    public StudioRole Role { get; init; } = StudioRole.ProductionCompany;

    public int SortOrder { get; init; }
}

public sealed record MediaStudioCreditDto(
    Guid Id,
    Guid StudioId,
    string StudioName,
    StudioRole Role,
    int SortOrder);

public sealed record MovieDetailsInput
{
    public int? RuntimeMinutes { get; init; }

    public string? OriginalLanguage { get; init; }

    public string? Language { get; init; }

    public string? CountryOfOrigin { get; init; }

    public string? AgeRating { get; init; }
}

public sealed record MovieDetailsDto
{
    public int? RuntimeMinutes { get; init; }

    public string? OriginalLanguage { get; init; }

    public string? Language { get; init; }

    public string? CountryOfOrigin { get; init; }

    public string? AgeRating { get; init; }
}
