using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Application.Validation;
using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Application.Services;

public sealed class MediaItemService(
    IMediaItemRepository mediaItemRepository,
    IPersonService personService,
    IStudioService studioService) : IMediaItemService
{
    public async Task<IReadOnlyList<MediaItemDto>> GetLibraryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var items = await mediaItemRepository.GetAllAsync(userId, cancellationToken);
        return await ToDtosAsync(items, cancellationToken);
    }

    public async Task<MediaItemDto?> GetMediaItemAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var item = await mediaItemRepository.GetByIdAsync(id, userId, cancellationToken);
        return item is null ? null : await ToDtoAsync(item, null, cancellationToken);
    }

    public async Task<MediaItemDto> CreateMediaItemAsync(CreateMediaItemRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = MediaItemValidator.Validate(request);
        ThrowIfInvalid(validationResult);

        var now = DateTime.UtcNow;
        var userId = request.UserId.Trim();

        var creatorId = await ResolvePersonIdAsync(userId, request.Creator, cancellationToken);
        var studioId = await ResolveStudioIdAsync(userId, request.Publisher, cancellationToken);
        var castIds = await ResolveCastIdsAsync(userId, request.Cast, cancellationToken);

        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title.Trim(),
            OriginalTitle = Normalize(request.OriginalTitle),
            Description = Normalize(request.Description),
            Category = Normalize(request.Category),
            Tags = NormalizeTags(request.Tags),
            CreatorId = creatorId,
            StudioId = studioId,
            SerialNumber = Normalize(request.SerialNumber),
            MediaType = request.MediaType,
            Status = request.Status,
            Rating = request.Rating,
            ProgressCurrent = request.ProgressCurrent,
            ProgressTotal = request.ProgressTotal,
            StartDate = request.StartDate,
            FinishDate = request.FinishDate,
            ReleaseYear = request.ReleaseYear,
            CoverUrl = Normalize(request.CoverUrl),
            Notes = Normalize(request.Notes),
            CreatedAt = now,
            UpdatedAt = now,
            IsFavorite = request.IsFavorite
        };

        var created = await mediaItemRepository.AddAsync(mediaItem, cancellationToken);
        await mediaItemRepository.ReplaceCastAsync(created.Id, castIds, cancellationToken);
        return await ToDtoAsync(created, castIds, cancellationToken);
    }

    public async Task<MediaItemDto> UpdateMediaItemAsync(UpdateMediaItemRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = MediaItemValidator.Validate(request);
        ThrowIfInvalid(validationResult);

        var existing = await mediaItemRepository.GetByIdAsync(request.Id, request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Media item was not found.");

        var userId = request.UserId.Trim();
        var creatorId = await ResolvePersonIdAsync(userId, request.Creator, cancellationToken);
        var studioId = await ResolveStudioIdAsync(userId, request.Publisher, cancellationToken);
        var castIds = await ResolveCastIdsAsync(userId, request.Cast, cancellationToken);

        existing.Title = request.Title.Trim();
        existing.OriginalTitle = Normalize(request.OriginalTitle);
        existing.Description = Normalize(request.Description);
        existing.Category = Normalize(request.Category);
        existing.Tags = NormalizeTags(request.Tags);
        existing.CreatorId = creatorId;
        existing.StudioId = studioId;
        existing.SerialNumber = Normalize(request.SerialNumber);
        existing.MediaType = request.MediaType;
        existing.Status = request.Status;
        existing.Rating = request.Rating;
        existing.ProgressCurrent = request.ProgressCurrent;
        existing.ProgressTotal = request.ProgressTotal;
        existing.StartDate = request.StartDate;
        existing.FinishDate = request.FinishDate;
        existing.ReleaseYear = request.ReleaseYear;
        existing.CoverUrl = Normalize(request.CoverUrl);
        existing.Notes = Normalize(request.Notes);
        existing.IsFavorite = request.IsFavorite;
        existing.Touch();

        var updated = await mediaItemRepository.UpdateAsync(existing, cancellationToken);
        await mediaItemRepository.ReplaceCastAsync(updated.Id, castIds, cancellationToken);
        return await ToDtoAsync(updated, castIds, cancellationToken);
    }

    public Task DeleteMediaItemAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        return mediaItemRepository.DeleteAsync(id, userId, cancellationToken);
    }

    public async Task<IReadOnlyList<MediaItemDto>> SearchMediaItemsAsync(MediaItemSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var items = await mediaItemRepository.SearchAsync(
            criteria.UserId,
            criteria.SearchTerm,
            criteria.MediaType,
            criteria.Status,
            criteria.Category,
            criteria.Tag,
            cancellationToken);

        return await ToDtosAsync(items, cancellationToken);
    }

    private async Task<Guid?> ResolvePersonIdAsync(string userId, string? name, CancellationToken cancellationToken)
    {
        var trimmed = Normalize(name);
        if (trimmed is null)
        {
            return null;
        }

        var person = await personService.GetOrCreateAsync(userId, trimmed, cancellationToken);
        return person.Id;
    }

    private async Task<Guid?> ResolveStudioIdAsync(string userId, string? name, CancellationToken cancellationToken)
    {
        var trimmed = Normalize(name);
        if (trimmed is null)
        {
            return null;
        }

        var studio = await studioService.GetOrCreateAsync(userId, trimmed, cancellationToken);
        return studio.Id;
    }

    private async Task<IReadOnlyList<Guid>> ResolveCastIdsAsync(string userId, IReadOnlyList<string>? names, CancellationToken cancellationToken)
    {
        if (names is null || names.Count == 0)
        {
            return [];
        }

        var ids = new List<Guid>();
        foreach (var name in names)
        {
            var trimmed = Normalize(name);
            if (trimmed is null)
            {
                continue;
            }

            var person = await personService.GetOrCreateAsync(userId, trimmed, cancellationToken);
            if (!ids.Contains(person.Id))
            {
                ids.Add(person.Id);
            }
        }

        return ids;
    }

    private static void ThrowIfInvalid(ValidationResult validationResult)
    {
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var tags = value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return tags.Count == 0 ? null : string.Join(", ", tags);
    }

    private async Task<IReadOnlyList<MediaItemDto>> ToDtosAsync(IReadOnlyList<MediaItem> items, CancellationToken cancellationToken)
    {
        var dtos = new List<MediaItemDto>(items.Count);
        foreach (var item in items)
        {
            dtos.Add(await ToDtoAsync(item, null, cancellationToken));
        }

        return dtos;
    }

    private async Task<MediaItemDto> ToDtoAsync(MediaItem item, IReadOnlyList<Guid>? castIds, CancellationToken cancellationToken)
    {
        var resolvedCastIds = castIds ?? await mediaItemRepository.GetCastPersonIdsAsync(item.Id, cancellationToken);

        string? creatorName = null;
        if (item.CreatorId.HasValue)
        {
            var creator = await personService.GetByIdAsync(item.UserId, item.CreatorId.Value, cancellationToken);
            creatorName = creator?.Name;
        }

        string? studioName = null;
        if (item.StudioId.HasValue)
        {
            var studio = await studioService.GetByIdAsync(item.UserId, item.StudioId.Value, cancellationToken);
            studioName = studio?.Name;
        }

        var castNames = resolvedCastIds.Count == 0
            ? []
            : (await personService.GetByIdsAsync(item.UserId, resolvedCastIds, cancellationToken)).Select(person => person.Name).ToList();

        return new MediaItemDto
        {
            Id = item.Id,
            UserId = item.UserId,
            Title = item.Title,
            OriginalTitle = item.OriginalTitle,
            Description = item.Description,
            Category = item.Category,
            Tags = item.Tags,
            CreatorId = item.CreatorId,
            Creator = creatorName,
            StudioId = item.StudioId,
            Publisher = studioName,
            SerialNumber = item.SerialNumber,
            Cast = castNames,
            MediaType = item.MediaType,
            Status = item.Status,
            Rating = item.Rating,
            ProgressCurrent = item.ProgressCurrent,
            ProgressTotal = item.ProgressTotal,
            StartDate = item.StartDate,
            FinishDate = item.FinishDate,
            ReleaseYear = item.ReleaseYear,
            CoverUrl = item.CoverUrl,
            Notes = item.Notes,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            DeletedAt = item.DeletedAt,
            IsFavorite = item.IsFavorite
        };
    }
}
