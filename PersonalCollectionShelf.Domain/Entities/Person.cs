namespace PersonalCollectionShelf.Domain.Entities;

public sealed class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public void Touch(DateTime? utcNow = null)
    {
        UpdatedAt = utcNow ?? DateTime.UtcNow;
    }

    public void MarkDeleted(DateTime? utcNow = null)
    {
        var timestamp = utcNow ?? DateTime.UtcNow;
        DeletedAt = timestamp;
        UpdatedAt = timestamp;
    }
}
