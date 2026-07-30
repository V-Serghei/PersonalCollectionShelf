using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Application.DTOs;

public sealed record CategoryFieldDefinitionDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string FieldType { get; init; } = "text";
    public bool IsRequired { get; init; }
}

public sealed record MediaCategoryDetailsDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public MediaType? BaseMediaType { get; init; }
    public bool IsSystem { get; init; }
    public IReadOnlyList<CategoryFieldDefinitionDto> Fields { get; init; } = [];
}

public sealed record SaveMediaCategoryRequest
{
    public Guid? Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public MediaType? BaseMediaType { get; init; }
    public IReadOnlyList<CategoryFieldDefinitionDto> Fields { get; init; } = [];
}
