namespace PersonalCollectionShelf.Application.DTOs;

public sealed record PersonDto(Guid Id, string Name)
{
    public string? SortName { get; init; }
    public string? PenName { get; init; }
    public int? BirthYear { get; init; }
    public int? DeathYear { get; init; }
    public string? Country { get; init; }
    public string? PhotoPath { get; init; }
    public string? Tagline { get; init; }

    public override string ToString() => Name;
}
