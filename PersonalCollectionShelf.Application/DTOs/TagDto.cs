using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.DTOs;

public sealed record TagDto(Guid Id, string Name, TagKind Kind);
