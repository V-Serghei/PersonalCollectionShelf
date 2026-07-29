using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.DTOs;

public sealed record MediaItemDto
{
    public Guid Id { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? OriginalTitle { get; init; }

    public string? Description { get; init; }

    public string? Category { get; init; }

    public Guid? MediaCategoryId { get; init; }

    public string? MediaCategoryName { get; init; }

    public string? Tags { get; init; }

    public Guid? CreatorId { get; init; }

    public string? Creator { get; init; }

    public Guid? StudioId { get; init; }

    public string? Publisher { get; init; }

    public string? SerialNumber { get; init; }

    public int? TmdbId { get; init; }
    public string? ImdbId { get; init; }
    public int? KinopoiskId { get; init; }
    public decimal? TmdbRating { get; init; }
    public int? TmdbVoteCount { get; init; }
    public decimal? ImdbRating { get; init; }
    public int? ImdbVoteCount { get; init; }
    public decimal? KinopoiskRating { get; init; }
    public int? KinopoiskVoteCount { get; init; }
    public DateTime? ExternalRatingsUpdatedAt { get; init; }

    public IReadOnlyList<string> Cast { get; init; } = [];

    public MediaType MediaType { get; init; }

    public MediaStatus Status { get; init; }

    public decimal? Rating { get; init; }

    public IReadOnlyList<string> TagNames { get; init; } = [];

    public IReadOnlyList<string> Genres { get; init; } = [];

    public IReadOnlyList<MediaContributionDto> Contributions { get; init; } = [];

    public IReadOnlyList<MediaStudioCreditDto> StudioCredits { get; init; } = [];

    public BookDetailsDto? BookDetails { get; init; }

    public MovieDetailsDto? MovieDetails { get; init; }

    public EpisodicDetailsInput? EpisodicDetails { get; init; }

    public GraphicPublicationDetailsInput? GraphicPublicationDetails { get; init; }

    public GameDetailsInput? GameDetails { get; init; }

    public CollectionMembershipDto? Collection { get; init; }

    public IReadOnlyList<MediaRelationDto> Relations { get; init; } = [];

    public int ProgressCurrent { get; init; }

    public int? ProgressTotal { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? FinishDate { get; init; }

    public int? ReleaseYear { get; init; }

    public string? CoverUrl { get; init; }

    public string? Notes { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public DateTime? DeletedAt { get; init; }

    public bool IsFavorite { get; init; }
}
