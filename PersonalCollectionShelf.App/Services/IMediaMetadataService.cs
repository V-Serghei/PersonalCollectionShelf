using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.Services;

public sealed record MediaMetadataCandidate(
    int TmdbId,
    bool IsTv,
    string Title,
    string EnglishTitle,
    string OriginalTitle,
    int? Year,
    string? PosterUrl,
    string CastSummary,
    decimal? TmdbRating,
    int? TmdbVoteCount)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(EnglishTitle) ||
                                  string.Equals(Title, EnglishTitle, StringComparison.OrdinalIgnoreCase)
        ? Title
        : $"{Title} / {EnglishTitle}";

    public string YearText => Year?.ToString() ?? "—";
    public string TmdbRatingText => TmdbRating.HasValue
        ? $"TMDB {TmdbRating:0.0} ({TmdbVoteCount ?? 0:N0})"
        : "TMDB —";
}

public sealed record MetadataPerson(string Name, ContributionRole Role, string? Details = null);
public sealed record MetadataStudio(string Name, StudioRole Role);

public sealed record MediaMetadataDetails
{
    public required MediaMetadataCandidate Candidate { get; init; }
    public string? Description { get; init; }
    public string? ImdbId { get; init; }
    public int? KinopoiskId { get; init; }
    public decimal? ImdbRating { get; init; }
    public int? ImdbVoteCount { get; init; }
    public decimal? KinopoiskRating { get; init; }
    public int? KinopoiskVoteCount { get; init; }
    public int? RuntimeMinutes { get; init; }
    public int? SeasonCount { get; init; }
    public int? EpisodeCount { get; init; }
    public string? OriginalLanguage { get; init; }
    public string? Country { get; init; }
    public string? AgeRating { get; init; }
    public string? Network { get; init; }
    public string? AiringStatus { get; init; }
    public string? CollectionName { get; init; }
    public IReadOnlyList<string> Genres { get; init; } = [];
    public IReadOnlyList<MetadataPerson> People { get; init; } = [];
    public IReadOnlyList<MetadataStudio> Studios { get; init; } = [];
    public DateTime RatingsUpdatedAtUtc { get; init; } = DateTime.UtcNow;
}

public interface IMediaMetadataService
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<MediaMetadataCandidate>> SearchAsync(
        string query,
        MediaType mediaType,
        CancellationToken cancellationToken = default);
    Task<MediaMetadataCandidate> GetCandidateAsync(
        int tmdbId,
        MediaType mediaType,
        CancellationToken cancellationToken = default);
    Task<MediaMetadataDetails> GetDetailsAsync(
        MediaMetadataCandidate candidate,
        CancellationToken cancellationToken = default);
    Task<string?> DownloadPosterAsync(
        MediaMetadataCandidate candidate,
        CancellationToken cancellationToken = default);
}
