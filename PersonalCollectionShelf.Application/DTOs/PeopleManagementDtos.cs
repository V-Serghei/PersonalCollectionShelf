using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.DTOs;

public sealed record PersonDetailsDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public string? LastName { get; init; }
    public string? PenName { get; init; }
    public string? SortName { get; init; }
    public int? BirthYear { get; init; }
    public int? DeathYear { get; init; }
    public string? Country { get; init; }
    public string? PlaceOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? OfficialWebsite { get; init; }
    public string? PhotoPath { get; init; }
    public string? Tagline { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<string> Professions { get; init; } = [];
    public IReadOnlyList<PersonRelationDto> Relations { get; init; } = [];
    public IReadOnlyList<PersonWorkDto> Works { get; init; } = [];
    public IReadOnlyList<PersonPhotoDto> Photos { get; init; } = [];
}

public sealed record PersonWorkDto(
    Guid MediaItemId,
    string Title,
    MediaType MediaType,
    ContributionRole Role,
    string? Details,
    string? CreditedAs,
    decimal? Rating,
    string? CoverUrl,
    int? ReleaseYear);

public sealed record PersonPhotoDto(
    Guid Id,
    string FilePath,
    string? Caption,
    bool IsPrimary,
    int SortOrder);

public sealed record PersonCatalogDto(
    Guid Id,
    string Name,
    string? PhotoPath,
    string? Tagline,
    string? Country,
    IReadOnlyList<PersonWorkDto> TopWorks,
    int WorkCount,
    decimal? AverageRating);

public sealed record SavePersonRequest
{
    public Guid? Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public string? LastName { get; init; }
    public string? PenName { get; init; }
    public string? SortName { get; init; }
    public int? BirthYear { get; init; }
    public int? DeathYear { get; init; }
    public string? Country { get; init; }
    public string? PlaceOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? OfficialWebsite { get; init; }
    public string? PhotoPath { get; init; }
    public string? Tagline { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<string> Professions { get; init; } = [];
}

public sealed record PersonRelationDto(
    Guid Id,
    Guid RelatedPersonId,
    string RelatedPersonName,
    PersonRelationKind Kind,
    PersonRelationKind? InverseKind,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Notes);

public sealed record SavePersonRelationRequest
{
    public Guid? Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public Guid PersonId { get; init; }
    public Guid RelatedPersonId { get; init; }
    public PersonRelationKind Kind { get; init; } = PersonRelationKind.Other;
    public PersonRelationKind? InverseKind { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Notes { get; init; }
}

public sealed record AddPersonPhotoRequest
{
    public Guid? Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public Guid PersonId { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string? Caption { get; init; }
}
