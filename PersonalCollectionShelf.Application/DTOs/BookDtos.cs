using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.DTOs;

public sealed record PersonCreditInput
{
    public Guid? PersonId { get; init; }

    public Guid? CreditRoleId { get; init; }

    public string Name { get; init; } = string.Empty;

    public ContributionRole Role { get; init; } = ContributionRole.Other;

    public int SortOrder { get; init; }

    public string? Details { get; init; }

    public string? CreditedAs { get; init; }
}

public sealed record MediaContributionDto(
    Guid Id,
    Guid PersonId,
    string PersonName,
    Guid CreditRoleId,
    ContributionRole Role,
    int SortOrder,
    string? Details,
    string? CreditedAs);

public sealed record BookDetailsInput
{
    public string? Subtitle { get; init; }
    public string? Publisher { get; init; }
    public string? Edition { get; init; }
    public int? EditionNumber { get; init; }
    public int? EditionYear { get; init; }
    public int? OriginalPublicationYear { get; init; }
    public int? TranslationYear { get; init; }
    public string? OriginalLanguage { get; init; }
    public string? Language { get; init; }
    public int? PageCount { get; init; }
    public string? Isbn10 { get; init; }
    public string? Isbn13 { get; init; }
    public BookFormat? Format { get; init; }
    public string? Binding { get; init; }
    public string? CountryOfOrigin { get; init; }
    public string? AgeRating { get; init; }
}

public sealed record BookDetailsDto
{
    public string? Subtitle { get; init; }
    public Guid? PublisherId { get; init; }
    public string? Publisher { get; init; }
    public string? Edition { get; init; }
    public int? EditionNumber { get; init; }
    public int? EditionYear { get; init; }
    public int? OriginalPublicationYear { get; init; }
    public int? TranslationYear { get; init; }
    public string? OriginalLanguage { get; init; }
    public string? Language { get; init; }
    public int? PageCount { get; init; }
    public string? Isbn10 { get; init; }
    public string? Isbn13 { get; init; }
    public BookFormat? Format { get; init; }
    public string? Binding { get; init; }
    public string? CountryOfOrigin { get; init; }
    public string? AgeRating { get; init; }
}

public sealed record CollectionMembershipInput
{
    public Guid? CollectionId { get; init; }
    public string? Name { get; init; }
    public MediaCollectionKind Kind { get; init; } = MediaCollectionKind.Series;
    public double? Position { get; init; }
}

public sealed record CollectionMembershipDto(
    Guid EntryId,
    Guid CollectionId,
    string Name,
    MediaCollectionKind Kind,
    double? Position);

public sealed record MediaRelationInput
{
    public Guid RelatedItemId { get; init; }
    public MediaRelationKind Kind { get; init; } = MediaRelationKind.Other;
    public string? Notes { get; init; }
}

public sealed record MediaRelationDto(
    Guid Id,
    Guid RelatedItemId,
    string RelatedItemTitle,
    MediaRelationKind Kind,
    string? Notes,
    bool IsOutgoing);
