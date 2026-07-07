using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Application.Validation;
using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Application.Services;

public sealed class MediaItemService(IMediaItemRepository mediaItemRepository) : IMediaItemService
{
    public async Task<IReadOnlyList<MediaItemDto>> GetLibraryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var items = await mediaItemRepository.GetAllAsync(userId, cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<MediaItemDto?> GetMediaItemAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var item = await mediaItemRepository.GetByIdAsync(id, userId, cancellationToken);
        return item is null ? null : ToDto(item);
    }

    public async Task<MediaItemDto> CreateMediaItemAsync(CreateMediaItemRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = MediaItemValidator.Validate(request);
        ThrowIfInvalid(validationResult);

        var now = DateTime.UtcNow;
        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId.Trim(),
            Title = request.Title.Trim(),
            OriginalTitle = Normalize(request.OriginalTitle),
            Description = Normalize(request.Description),
            Category = Normalize(request.Category),
            Tags = NormalizeTags(request.Tags),
            Creator = Normalize(request.Creator),
            Publisher = Normalize(request.Publisher),
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
        return ToDto(created);
    }

    public async Task<MediaItemDto> UpdateMediaItemAsync(UpdateMediaItemRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = MediaItemValidator.Validate(request);
        ThrowIfInvalid(validationResult);

        var existing = await mediaItemRepository.GetByIdAsync(request.Id, request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Media item was not found.");

        existing.Title = request.Title.Trim();
        existing.OriginalTitle = Normalize(request.OriginalTitle);
        existing.Description = Normalize(request.Description);
        existing.Category = Normalize(request.Category);
        existing.Tags = NormalizeTags(request.Tags);
        existing.Creator = Normalize(request.Creator);
        existing.Publisher = Normalize(request.Publisher);
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
        return ToDto(updated);
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

        return items.Select(ToDto).ToList();
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

    private static MediaItemDto ToDto(MediaItem item)
    {
        return new MediaItemDto
        {
            Id = item.Id,
            UserId = item.UserId,
            Title = item.Title,
            OriginalTitle = item.OriginalTitle,
            Description = item.Description,
            Category = item.Category,
            Tags = item.Tags,
            Creator = item.Creator,
            Publisher = item.Publisher,
            SerialNumber = item.SerialNumber,
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
