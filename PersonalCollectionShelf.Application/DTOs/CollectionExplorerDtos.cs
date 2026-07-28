using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.DTOs;

public sealed record CollectionItemDto(Guid MediaItemId, string Title, MediaType MediaType, double? Position);
public sealed record CollectionExplorerDto(Guid Id, string Name, MediaCollectionKind Kind, IReadOnlyList<CollectionItemDto> Items);
