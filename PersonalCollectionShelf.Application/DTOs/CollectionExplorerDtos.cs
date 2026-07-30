using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.DTOs;

public sealed record CollectionItemDto(
    Guid MediaItemId,
    string Title,
    MediaType MediaType,
    double? Position,
    string? CoverUrl = null,
    string? Category = null,
    int? ReleaseYear = null,
    decimal? Rating = null,
    MediaStatus Status = MediaStatus.Planned);

public sealed record CollectionExplorerDto(
    Guid Id,
    string Name,
    MediaCollectionKind Kind,
    IReadOnlyList<CollectionItemDto> Items,
    string? Description = null);
