using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.DTOs;

public sealed record UpdateMediaItemRequest
{
    public Guid Id { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? OriginalTitle { get; init; }

    public string? Description { get; init; }

    public string? Category { get; init; }

    public Guid? MediaCategoryId { get; init; }

    public string? Tags { get; init; }

    public string? Creator { get; init; }

    public string? Publisher { get; init; }

    public string? SerialNumber { get; init; }

    public IReadOnlyList<string>? Cast { get; init; }

    public MediaType MediaType { get; init; } = MediaType.Other;

    public MediaStatus Status { get; init; } = MediaStatus.Planned;

    public decimal? Rating { get; init; }

    public IReadOnlyList<string>? TagNames { get; init; }

    public IReadOnlyList<string>? Genres { get; init; }

    public IReadOnlyList<PersonCreditInput>? Contributions { get; init; }

    public BookDetailsInput? BookDetails { get; init; }

    public CollectionMembershipInput? Collection { get; init; }

    public IReadOnlyList<MediaRelationInput>? Relations { get; init; }

    public int ProgressCurrent { get; init; }

    public int? ProgressTotal { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? FinishDate { get; init; }

    public int? ReleaseYear { get; init; }

    public string? CoverUrl { get; init; }

    public string? Notes { get; init; }

    public bool IsFavorite { get; init; }
}
