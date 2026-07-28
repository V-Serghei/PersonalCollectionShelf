using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Domain.Enums;
using PersonalCollectionShelf.Infrastructure.Persistence;

namespace PersonalCollectionShelf.Infrastructure.Repositories;

public sealed class MediaItemRepository(LocalDatabaseService databaseService) : IMediaItemRepository
{
    public async Task<IReadOnlyList<MediaItem>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);

        var records = await databaseService.Connection
            .Table<MediaItemRecord>()
            .Where(record => record.UserId == userId && record.DeletedAt == null)
            .OrderBy(record => record.Title)
            .ToListAsync();

        return records.Select(ToDomain).ToList();
    }

    public async Task<int> CountAsync(string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);

        return await databaseService.Connection
            .Table<MediaItemRecord>()
            .Where(record => record.UserId == userId && record.DeletedAt == null)
            .CountAsync();
    }

    public async Task<MediaItem?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var idText = id.ToString();

        var record = await databaseService.Connection
            .Table<MediaItemRecord>()
            .FirstOrDefaultAsync(item => item.Id == idText && item.UserId == userId && item.DeletedAt == null);

        return record is null ? null : ToDomain(record);
    }

    public async Task<MediaItem> AddAsync(MediaItem mediaItem, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.InsertAsync(ToRecord(mediaItem));
        return mediaItem;
    }

    public async Task<MediaItem> UpdateAsync(MediaItem mediaItem, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.UpdateAsync(ToRecord(mediaItem));
        return mediaItem;
    }

    public async Task DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var mediaItem = await GetByIdAsync(id, userId, cancellationToken);
        if (mediaItem is null)
        {
            return;
        }

        mediaItem.MarkDeleted();
        await UpdateAsync(mediaItem, cancellationToken);
    }

    public async Task<IReadOnlyList<MediaItem>> SearchAsync(
        string userId,
        string? searchTerm,
        MediaType? mediaType,
        MediaStatus? status,
        string? category,
        string? tag,
        CancellationToken cancellationToken = default)
    {
        var items = await GetAllAsync(userId, cancellationToken);

        var query = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(item =>
                Contains(item.Title, searchTerm) ||
                Contains(item.OriginalTitle, searchTerm) ||
                Contains(item.Description, searchTerm) ||
                Contains(item.Category, searchTerm) ||
                Contains(item.Tags, searchTerm) ||
                Contains(item.SerialNumber, searchTerm) ||
                Contains(item.Notes, searchTerm));
        }

        if (mediaType.HasValue)
        {
            query = query.Where(item => item.MediaType == mediaType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => string.Equals(item.Category, category.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            query = query.Where(item => HasTag(item.Tags, tag));
        }

        return query
            .OrderByDescending(item => item.IsFavorite)
            .ThenBy(item => item.Title)
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetCastPersonIdsAsync(Guid mediaItemId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);

        var mediaItemIdText = mediaItemId.ToString();
        var records = await databaseService.Connection
            .Table<MediaItemCastMemberRecord>()
            .Where(record => record.MediaItemId == mediaItemIdText)
            .ToListAsync();

        return records.Select(record => Guid.Parse(record.PersonId)).ToList();
    }

    public async Task ReplaceCastAsync(Guid mediaItemId, IReadOnlyList<Guid> personIds, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);

        var mediaItemIdText = mediaItemId.ToString();
        var existing = await databaseService.Connection
            .Table<MediaItemCastMemberRecord>()
            .Where(record => record.MediaItemId == mediaItemIdText)
            .ToListAsync();

        foreach (var record in existing)
        {
            await databaseService.Connection.DeleteAsync(record);
        }

        foreach (var personId in personIds)
        {
            await databaseService.Connection.InsertAsync(new MediaItemCastMemberRecord
            {
                MediaItemId = mediaItemIdText,
                PersonId = personId.ToString()
            });
        }
    }

    private static Guid? ParseGuid(string? value)
    {
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool Contains(string? source, string searchTerm)
    {
        return source?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool HasTag(string? tags, string tag)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return false;
        }

        return tags
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(candidate => string.Equals(candidate, tag.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static MediaItem ToDomain(MediaItemRecord record)
    {
        return new MediaItem
        {
            Id = Guid.Parse(record.Id),
            UserId = record.UserId,
            Title = record.Title,
            OriginalTitle = record.OriginalTitle,
            Description = record.Description,
            Category = record.Category,
            CategoryId = ParseGuid(record.CategoryId),
            Tags = record.Tags,
            CreatorId = ParseGuid(record.CreatorId),
            StudioId = ParseGuid(record.StudioId),
            SerialNumber = record.SerialNumber,
            MediaType = (MediaType)record.MediaType,
            Status = (MediaStatus)record.Status,
            Rating = record.Rating,
            ProgressCurrent = record.ProgressCurrent,
            ProgressTotal = record.ProgressTotal,
            StartDate = record.StartDate,
            FinishDate = record.FinishDate,
            ReleaseYear = record.ReleaseYear,
            CoverUrl = record.CoverUrl,
            Notes = record.Notes,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
            DeletedAt = record.DeletedAt,
            IsFavorite = record.IsFavorite
        };
    }

    private static MediaItemRecord ToRecord(MediaItem item)
    {
        return new MediaItemRecord
        {
            Id = item.Id.ToString(),
            UserId = item.UserId,
            Title = item.Title,
            OriginalTitle = item.OriginalTitle,
            Description = item.Description,
            Category = item.Category,
            CategoryId = item.CategoryId?.ToString(),
            Tags = item.Tags,
            CreatorId = item.CreatorId?.ToString(),
            StudioId = item.StudioId?.ToString(),
            SerialNumber = item.SerialNumber,
            MediaType = (int)item.MediaType,
            Status = (int)item.Status,
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
