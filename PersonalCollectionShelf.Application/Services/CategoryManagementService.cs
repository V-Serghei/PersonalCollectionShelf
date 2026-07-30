using System.Text.Json;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Domain.Abstractions;

namespace PersonalCollectionShelf.Application.Services;

public sealed class CategoryManagementService(IMediaCategoryRepository categories) : ICategoryManagementService
{
    private static readonly HashSet<string> FieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "number", "date", "boolean", "choice", "multiline"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<MediaCategoryDetailsDto>> GetAllAsync(string userId, CancellationToken cancellationToken = default) =>
        (await categories.GetAllAsync(userId, cancellationToken)).Select(ToDto).ToList();

    public async Task<MediaCategoryDetailsDto> SaveAsync(SaveMediaCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = request.UserId.Trim();
        var name = request.Name.Trim();
        if (userId.Length == 0 || name.Length == 0)
        {
            throw new ArgumentException("User and category name are required.");
        }

        var fields = NormalizeFields(request.Fields);
        if (request.Id is null)
        {
            var created = await categories.GetOrCreateCustomAsync(userId, name, request.BaseMediaType, cancellationToken);
            created.Name = name;
            created.BaseMediaType = request.BaseMediaType;
            created.FieldSchemaJson = Serialize(fields);
            return ToDto(await categories.UpdateAsync(created, cancellationToken));
        }

        var category = await categories.GetByIdAsync(request.Id.Value, userId, cancellationToken)
            ?? throw new InvalidOperationException("Category was not found.");
        if (category.IsSystem)
        {
            throw new InvalidOperationException("System categories are read-only.");
        }

        category.Name = name;
        category.BaseMediaType = request.BaseMediaType;
        category.FieldSchemaJson = Serialize(fields);
        return ToDto(await categories.UpdateAsync(category, cancellationToken));
    }

    public Task DeleteAsync(string userId, Guid id, CancellationToken cancellationToken = default) =>
        categories.DeleteAsync(id, userId, cancellationToken);

    private static List<CategoryFieldDefinitionDto> NormalizeFields(IReadOnlyList<CategoryFieldDefinitionDto> fields)
    {
        var normalized = fields.Select(field => field with
        {
            Key = field.Key.Trim(),
            Label = field.Label.Trim(),
            FieldType = field.FieldType.Trim().ToLowerInvariant()
        }).ToList();

        if (normalized.Any(field => field.Key.Length == 0 || field.Label.Length == 0))
        {
            throw new ArgumentException("Every field requires a key and label.");
        }

        if (normalized.Any(field => !FieldTypes.Contains(field.FieldType)))
        {
            throw new ArgumentException("Unsupported category field type.");
        }

        if (normalized.GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Category field keys must be unique.");
        }

        return normalized;
    }

    private static string? Serialize(IReadOnlyList<CategoryFieldDefinitionDto> fields) =>
        fields.Count == 0 ? null : JsonSerializer.Serialize(fields, JsonOptions);

    private static MediaCategoryDetailsDto ToDto(Domain.Entities.MediaCategory category) => new()
    {
        Id = category.Id,
        Key = category.Key,
        Name = category.Name,
        BaseMediaType = category.BaseMediaType,
        IsSystem = category.IsSystem,
        Fields = Deserialize(category.FieldSchemaJson)
    };

    private static IReadOnlyList<CategoryFieldDefinitionDto> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<CategoryFieldDefinitionDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
