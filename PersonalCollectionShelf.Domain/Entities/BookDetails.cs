using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Domain.Entities;

public sealed class BookDetails
{
    public Guid MediaItemId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public Guid? PublisherId { get; set; }

    public string? Subtitle { get; set; }

    public string? Edition { get; set; }

    public int? EditionNumber { get; set; }

    public int? EditionYear { get; set; }

    public int? OriginalPublicationYear { get; set; }

    public int? TranslationYear { get; set; }

    public string? OriginalLanguage { get; set; }

    public string? Language { get; set; }

    public int? PageCount { get; set; }

    public string? Isbn10 { get; set; }

    public string? Isbn13 { get; set; }

    public BookFormat? Format { get; set; }

    public string? Binding { get; set; }

    public string? CountryOfOrigin { get; set; }

    public string? AgeRating { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
