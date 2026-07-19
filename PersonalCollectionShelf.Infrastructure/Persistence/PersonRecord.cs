using SQLite;

namespace PersonalCollectionShelf.Infrastructure.Persistence;

[Table("People")]
public sealed class PersonRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? MiddleName { get; set; }

    public string? LastName { get; set; }

    public string? PenName { get; set; }

    public string? SortName { get; set; }

    public int? BirthYear { get; set; }

    public int? DeathYear { get; set; }

    public DateTime? BirthDate { get; set; }

    public DateTime? DeathDate { get; set; }

    public string? PlaceOfBirth { get; set; }

    public string? Country { get; set; }

    public string? Gender { get; set; }

    public string? OfficialWebsite { get; set; }

    public string? PhotoPath { get; set; }

    public string? Tagline { get; set; }

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [Indexed]
    public DateTime? DeletedAt { get; set; }
}
