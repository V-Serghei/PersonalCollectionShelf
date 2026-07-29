using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.Services;

public enum ExternalCatalogProvider
{
    GoogleBooks,
    OpenLibrary,
    ComicVine,
    Rawg
}

public sealed record ExternalCatalogCandidate(
    ExternalCatalogProvider Provider,
    string ExternalId,
    MediaType MediaType,
    string Title,
    string Subtitle,
    int? Year,
    string? ImageUrl,
    string CreatorSummary,
    string EditionSummary,
    string? Isbn,
    string? SourceUrl,
    decimal? Rating,
    int? RatingCount)
{
    public string ProviderName => Provider switch
    {
        ExternalCatalogProvider.GoogleBooks => "Google Books",
        ExternalCatalogProvider.OpenLibrary => "Open Library",
        ExternalCatalogProvider.ComicVine => "Comic Vine",
        _ => "RAWG"
    };

    public string YearText => Year?.ToString() ?? "—";
    public string RatingText => Rating.HasValue
        ? $"{ProviderName} {Rating:0.0} ({RatingCount ?? 0:N0})"
        : ProviderName;
}

public sealed record ExternalCatalogDetails
{
    public required ExternalCatalogCandidate Candidate { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Authors { get; init; } = [];
    public IReadOnlyList<string> Developers { get; init; } = [];
    public string? Publisher { get; init; }
    public string? Isbn10 { get; init; }
    public string? Isbn13 { get; init; }
    public int? PageCount { get; init; }
    public string? Language { get; init; }
    public IReadOnlyList<string> Genres { get; init; } = [];
    public int? VolumeCount { get; init; }
    public int? IssueCount { get; init; }
    public string? Platform { get; init; }
    public string? GameMode { get; init; }
    public string? Engine { get; init; }
    public string? PrimaryRatingSource { get; init; }
    public decimal? PrimaryRating { get; init; }
    public int? PrimaryRatingCount { get; init; }
    public string? SecondaryRatingSource { get; init; }
    public decimal? SecondaryRating { get; init; }
    public int? SecondaryRatingCount { get; init; }
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}

public interface IExternalCatalogMetadataService
{
    bool Supports(MediaType mediaType);
    bool IsConfigured(MediaType mediaType);
    Task<IReadOnlyList<ExternalCatalogCandidate>> SearchAsync(string query, MediaType mediaType, CancellationToken cancellationToken = default);
    Task<ExternalCatalogDetails> GetDetailsAsync(ExternalCatalogCandidate candidate, CancellationToken cancellationToken = default);
    Task<ExternalCatalogCandidate> GetCandidateAsync(ExternalCatalogProvider provider, string externalId, MediaType mediaType, CancellationToken cancellationToken = default);
    Task<string?> DownloadImageAsync(ExternalCatalogCandidate candidate, CancellationToken cancellationToken = default);
}
