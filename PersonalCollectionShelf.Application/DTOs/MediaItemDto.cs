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

    public string? Tags { get; init; }

    public string? Creator { get; init; }

    public string? Publisher { get; init; }

    public string? SerialNumber { get; init; }

    public MediaType MediaType { get; init; }

    public MediaStatus Status { get; init; }

    public int? Rating { get; init; }

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
