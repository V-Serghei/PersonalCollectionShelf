using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.DTOs;

public sealed record CreateMediaItemRequest
{
    public string UserId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? OriginalTitle { get; init; }

    public string? Description { get; init; }

    public MediaType MediaType { get; init; } = MediaType.Other;

    public MediaStatus Status { get; init; } = MediaStatus.Planned;

    public int? Rating { get; init; }

    public int ProgressCurrent { get; init; }

    public int? ProgressTotal { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? FinishDate { get; init; }

    public int? ReleaseYear { get; init; }

    public string? CoverUrl { get; init; }

    public string? Notes { get; init; }

    public bool IsFavorite { get; init; }
}
