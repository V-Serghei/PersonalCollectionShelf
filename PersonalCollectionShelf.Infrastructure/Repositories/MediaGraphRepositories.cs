using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Domain.Enums;
using PersonalCollectionShelf.Domain;
using PersonalCollectionShelf.Infrastructure.Persistence;

namespace PersonalCollectionShelf.Infrastructure.Repositories;

public sealed class MediaCategoryRepository(LocalDatabaseService databaseService) : IMediaCategoryRepository
{
    public async Task<IReadOnlyList<MediaCategory>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var records = await databaseService.Connection.Table<MediaCategoryRecord>()
            .Where(record => record.DeletedAt == null)
            .ToListAsync();
        return records.Where(record => record.IsSystem || record.UserId == userId)
            .OrderByDescending(record => record.IsSystem)
            .ThenBy(record => record.Name)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<MediaCategory?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var record = await databaseService.Connection.FindAsync<MediaCategoryRecord>(id.ToString());
        return record is null || record.DeletedAt.HasValue || (!record.IsSystem && record.UserId != userId) ? null : ToDomain(record);
    }

    public async Task<MediaCategory> GetSystemAsync(MediaType mediaType, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var records = await databaseService.Connection.Table<MediaCategoryRecord>()
            .Where(record => record.IsSystem && record.DeletedAt == null)
            .ToListAsync();
        var record = records.Single(value => value.BaseMediaType == (int)mediaType);
        return ToDomain(record);
    }

    public async Task<MediaCategory> GetOrCreateCustomAsync(string userId, string name, MediaType? baseMediaType, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A category name is required.", nameof(name));
        }

        var normalized = Normalize(trimmed);
        var records = await databaseService.Connection.Table<MediaCategoryRecord>()
            .Where(record => record.UserId == userId && record.IsSystem == false && record.DeletedAt == null)
            .ToListAsync();
        var existing = records.FirstOrDefault(record => record.NormalizedName == normalized);
        if (existing is not null)
        {
            return ToDomain(existing);
        }

        var now = DateTime.UtcNow;
        var created = new MediaCategoryRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Key = $"custom-{Guid.NewGuid():N}",
            Name = trimmed,
            NormalizedName = normalized,
            BaseMediaType = baseMediaType.HasValue ? (int)baseMediaType.Value : null,
            IsSystem = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        await databaseService.Connection.InsertAsync(created);
        return ToDomain(created);
    }

    public async Task<MediaCategory> UpdateAsync(MediaCategory category, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var record = await databaseService.Connection.FindAsync<MediaCategoryRecord>(category.Id.ToString())
            ?? throw new InvalidOperationException("Category was not found.");
        if (record.IsSystem || category.IsSystem || record.UserId != category.UserId)
        {
            throw new InvalidOperationException("System categories cannot be changed.");
        }

        record.Name = category.Name.Trim();
        record.NormalizedName = Normalize(record.Name);
        record.BaseMediaType = category.BaseMediaType.HasValue ? (int)category.BaseMediaType.Value : null;
        record.FieldSchemaJson = category.FieldSchemaJson;
        record.UpdatedAt = DateTime.UtcNow;
        await databaseService.Connection.UpdateAsync(record);
        return ToDomain(record);
    }

    public async Task DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var record = await databaseService.Connection.FindAsync<MediaCategoryRecord>(id.ToString());
        if (record is null || record.UserId != userId)
        {
            return;
        }

        if (record.IsSystem)
        {
            throw new InvalidOperationException("System categories cannot be deleted.");
        }

        record.DeletedAt = DateTime.UtcNow;
        record.UpdatedAt = record.DeletedAt.Value;
        await databaseService.Connection.UpdateAsync(record);
    }

    private static MediaCategory ToDomain(MediaCategoryRecord value) => new()
    {
        Id = Guid.Parse(value.Id), UserId = value.UserId, Key = value.Key, Name = value.Name,
        BaseMediaType = value.BaseMediaType.HasValue ? (MediaType)value.BaseMediaType.Value : null,
        IsSystem = value.IsSystem, FieldSchemaJson = value.FieldSchemaJson,
        CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt, DeletedAt = value.DeletedAt
    };

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}

public sealed class BookDetailsRepository(LocalDatabaseService databaseService) : IBookDetailsRepository
{
    public async Task<IReadOnlyList<BookDetails>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        return (await databaseService.Connection.Table<BookDetailsRecord>().Where(record => record.UserId == userId).ToListAsync())
            .Select(ToDomain).ToList();
    }

    public async Task<BookDetails?> GetAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var record = await databaseService.Connection.FindAsync<BookDetailsRecord>(mediaItemId.ToString());
        return record is null || record.UserId != userId ? null : ToDomain(record);
    }

    public async Task UpsertAsync(BookDetails details, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.InsertOrReplaceAsync(ToRecord(details));
    }

    private static BookDetails ToDomain(BookDetailsRecord record) => new()
    {
        MediaItemId = Guid.Parse(record.MediaItemId),
        UserId = record.UserId,
        PublisherId = ParseGuid(record.PublisherId),
        Subtitle = record.Subtitle,
        Edition = record.Edition,
        EditionNumber = record.EditionNumber,
        EditionYear = record.EditionYear,
        OriginalPublicationYear = record.OriginalPublicationYear,
        TranslationYear = record.TranslationYear,
        OriginalLanguage = record.OriginalLanguage,
        Language = record.Language,
        PageCount = record.PageCount,
        Isbn10 = record.Isbn10,
        Isbn13 = record.Isbn13,
        Format = record.Format.HasValue ? (BookFormat)record.Format.Value : null,
        Binding = record.Binding,
        CountryOfOrigin = record.CountryOfOrigin,
        AgeRating = record.AgeRating,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt
    };

    private static BookDetailsRecord ToRecord(BookDetails details) => new()
    {
        MediaItemId = details.MediaItemId.ToString(),
        UserId = details.UserId,
        PublisherId = details.PublisherId?.ToString(),
        Subtitle = details.Subtitle,
        Edition = details.Edition,
        EditionNumber = details.EditionNumber,
        EditionYear = details.EditionYear,
        OriginalPublicationYear = details.OriginalPublicationYear,
        TranslationYear = details.TranslationYear,
        OriginalLanguage = details.OriginalLanguage,
        Language = details.Language,
        PageCount = details.PageCount,
        Isbn10 = details.Isbn10,
        Isbn13 = details.Isbn13,
        Format = details.Format.HasValue ? (int)details.Format.Value : null,
        Binding = details.Binding,
        CountryOfOrigin = details.CountryOfOrigin,
        AgeRating = details.AgeRating,
        CreatedAt = details.CreatedAt,
        UpdatedAt = details.UpdatedAt
    };

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var parsed) ? parsed : null;
}

public sealed class MovieDetailsRepository(LocalDatabaseService databaseService) : IMovieDetailsRepository
{
    public async Task<IReadOnlyList<MovieDetails>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        return (await databaseService.Connection.Table<MovieDetailsRecord>().Where(record => record.UserId == userId).ToListAsync())
            .Select(ToDomain).ToList();
    }

    public async Task<MovieDetails?> GetAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var record = await databaseService.Connection.FindAsync<MovieDetailsRecord>(mediaItemId.ToString());
        return record is null || record.UserId != userId ? null : ToDomain(record);
    }

    public async Task UpsertAsync(MovieDetails details, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.InsertOrReplaceAsync(ToRecord(details));
    }

    private static MovieDetails ToDomain(MovieDetailsRecord record) => new()
    {
        MediaItemId = Guid.Parse(record.MediaItemId),
        UserId = record.UserId,
        RuntimeMinutes = record.RuntimeMinutes,
        OriginalLanguage = record.OriginalLanguage,
        Language = record.Language,
        CountryOfOrigin = record.CountryOfOrigin,
        AgeRating = record.AgeRating,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt
    };

    private static MovieDetailsRecord ToRecord(MovieDetails details) => new()
    {
        MediaItemId = details.MediaItemId.ToString(),
        UserId = details.UserId,
        RuntimeMinutes = details.RuntimeMinutes,
        OriginalLanguage = details.OriginalLanguage,
        Language = details.Language,
        CountryOfOrigin = details.CountryOfOrigin,
        AgeRating = details.AgeRating,
        CreatedAt = details.CreatedAt,
        UpdatedAt = details.UpdatedAt
    };
}

public sealed class TagRepository(LocalDatabaseService databaseService) : ITagRepository
{
    public async Task<IReadOnlyList<Tag>> GetAllAsync(string userId, TagKind? kind = null, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var records = await databaseService.Connection.Table<TagRecord>()
            .Where(record => record.UserId == userId && record.DeletedAt == null)
            .ToListAsync();

        return records
            .Where(record => !kind.HasValue || record.Kind == (int)kind.Value)
            .OrderBy(record => record.Name)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<IReadOnlyList<Tag>> GetForItemAsync(Guid mediaItemId, string userId, TagKind? kind = null, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var itemId = mediaItemId.ToString();
        var links = await databaseService.Connection.Table<MediaItemTagRecord>()
            .Where(record => record.UserId == userId && record.MediaItemId == itemId)
            .ToListAsync();
        if (links.Count == 0)
        {
            return [];
        }

        var ids = links.Select(link => link.TagId).ToHashSet(StringComparer.Ordinal);
        var tags = await databaseService.Connection.Table<TagRecord>()
            .Where(record => record.UserId == userId && record.DeletedAt == null)
            .ToListAsync();
        return tags
            .Where(record => ids.Contains(record.Id) && (!kind.HasValue || record.Kind == (int)kind.Value))
            .OrderBy(record => record.Name)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Tag>>> GetForItemsAsync(
        IReadOnlyCollection<Guid> mediaItemIds,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (mediaItemIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Tag>>();
        }

        await databaseService.InitializeAsync(cancellationToken);
        var itemIds = mediaItemIds.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);
        var links = (await databaseService.Connection.Table<MediaItemTagRecord>()
                .Where(record => record.UserId == userId)
                .ToListAsync())
            .Where(record => itemIds.Contains(record.MediaItemId))
            .ToList();

        if (links.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Tag>>();
        }

        var tagIds = links.Select(link => link.TagId).ToHashSet(StringComparer.Ordinal);
        var tagsById = (await databaseService.Connection.Table<TagRecord>()
                .Where(record => record.UserId == userId && record.DeletedAt == null)
                .ToListAsync())
            .Where(record => tagIds.Contains(record.Id))
            .ToDictionary(record => record.Id, ToDomain, StringComparer.Ordinal);

        return links
            .Where(link => tagsById.ContainsKey(link.TagId) && Guid.TryParse(link.MediaItemId, out _))
            .GroupBy(link => Guid.Parse(link.MediaItemId))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Tag>)group
                    .Select(link => tagsById[link.TagId])
                    .DistinctBy(tag => tag.Id)
                    .OrderBy(tag => tag.Name)
                    .ToList());
    }

    public async Task<IReadOnlyList<Tag>> SearchAsync(string userId, string? searchTerm, TagKind kind, int limit = 20, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(userId, kind, cancellationToken);
        var term = searchTerm?.Trim();
        return all
            .Where(tag => string.IsNullOrWhiteSpace(term) || tag.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(Math.Clamp(limit, 1, 100))
            .ToList();
    }

    public async Task<Tag> GetOrCreateAsync(string userId, string name, TagKind kind, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A tag name is required.", nameof(name));
        }

        var normalized = Normalize(trimmed);
        var records = await databaseService.Connection.Table<TagRecord>()
            .Where(record => record.UserId == userId)
            .ToListAsync();
        var existing = records.FirstOrDefault(record => record.Kind == (int)kind && record.NormalizedName == normalized);
        if (existing is not null)
        {
            if (existing.DeletedAt.HasValue)
            {
                existing.DeletedAt = null;
                existing.UpdatedAt = DateTime.UtcNow;
                await databaseService.Connection.UpdateAsync(existing);
            }

            return ToDomain(existing);
        }

        var now = DateTime.UtcNow;
        var created = new TagRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Name = trimmed,
            NormalizedName = normalized,
            Kind = (int)kind,
            CreatedAt = now,
            UpdatedAt = now
        };
        await databaseService.Connection.InsertAsync(created);
        return ToDomain(created);
    }

    public async Task SetForItemAsync(Guid mediaItemId, string userId, IReadOnlyCollection<string> tagNames, TagKind kind = TagKind.Tag, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var itemId = mediaItemId.ToString();
        var links = await databaseService.Connection.Table<MediaItemTagRecord>()
            .Where(record => record.UserId == userId && record.MediaItemId == itemId)
            .ToListAsync();
        var allTags = await databaseService.Connection.Table<TagRecord>()
            .Where(record => record.UserId == userId)
            .ToListAsync();
        var targetKindIds = allTags.Where(tag => tag.Kind == (int)kind).Select(tag => tag.Id).ToHashSet();

        foreach (var link in links.Where(link => targetKindIds.Contains(link.TagId)))
        {
            await databaseService.Connection.DeleteAsync(link);
        }

        foreach (var name in tagNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var tag = await GetOrCreateAsync(userId, name, kind, cancellationToken);
            await databaseService.Connection.InsertAsync(new MediaItemTagRecord
            {
                UserId = userId,
                MediaItemId = itemId,
                TagId = tag.Id.ToString()
            });
        }
    }

    private static Tag ToDomain(TagRecord record) => new()
    {
        Id = Guid.Parse(record.Id),
        UserId = record.UserId,
        Name = record.Name,
        Kind = (TagKind)record.Kind,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt,
        DeletedAt = record.DeletedAt
    };

    private static string Normalize(string name) => name.Trim().ToUpperInvariant();
}

public sealed class MediaContributionRepository(LocalDatabaseService databaseService) : IMediaContributionRepository
{
    public async Task<IReadOnlyList<MediaContribution>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var records = await databaseService.Connection.Table<MediaContributionRecord>()
            .Where(record => record.UserId == userId)
            .ToListAsync();
        return records.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<MediaContribution>> GetForItemAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var itemId = mediaItemId.ToString();
        var records = await databaseService.Connection.Table<MediaContributionRecord>()
            .Where(record => record.UserId == userId && record.MediaItemId == itemId)
            .ToListAsync();
        return records.OrderBy(record => record.Role).ThenBy(record => record.SortOrder).Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<MediaContribution>> GetForPersonAsync(Guid personId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var id = personId.ToString();
        var records = await databaseService.Connection.Table<MediaContributionRecord>()
            .Where(record => record.UserId == userId && record.PersonId == id)
            .ToListAsync();
        return records.Select(ToDomain).ToList();
    }

    public async Task ReplaceForItemAsync(Guid mediaItemId, string userId, IReadOnlyCollection<MediaContribution> contributions, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var itemId = mediaItemId.ToString();
        var existing = await databaseService.Connection.Table<MediaContributionRecord>()
            .Where(record => record.UserId == userId && record.MediaItemId == itemId)
            .ToListAsync();
        foreach (var record in existing)
        {
            await databaseService.Connection.DeleteAsync(record);
        }

        foreach (var contribution in contributions
                     .GroupBy(value => new { value.PersonId, value.Role })
                     .Select(group => group.OrderBy(value => value.SortOrder).First()))
        {
            await databaseService.Connection.InsertAsync(ToRecord(contribution));
        }
    }

    private static MediaContribution ToDomain(MediaContributionRecord record) => new()
    {
        Id = Guid.Parse(record.Id),
        UserId = record.UserId,
        MediaItemId = Guid.Parse(record.MediaItemId),
        PersonId = Guid.Parse(record.PersonId),
        CreditRoleId = Guid.TryParse(record.CreditRoleId, out var creditRoleId) ? creditRoleId : SystemEntityIds.CreditRole((ContributionRole)record.Role),
        Role = (ContributionRole)record.Role,
        SortOrder = record.SortOrder,
        Details = record.Details,
        CreditedAs = record.CreditedAs,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt
    };

    private static MediaContributionRecord ToRecord(MediaContribution value) => new()
    {
        Id = value.Id.ToString(),
        UserId = value.UserId,
        MediaItemId = value.MediaItemId.ToString(),
        PersonId = value.PersonId.ToString(),
        CreditRoleId = (value.CreditRoleId == Guid.Empty ? SystemEntityIds.CreditRole(value.Role) : value.CreditRoleId).ToString(),
        Role = (int)value.Role,
        SortOrder = value.SortOrder,
        Details = value.Details,
        CreditedAs = value.CreditedAs,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };
}

public sealed class MediaStudioCreditRepository(LocalDatabaseService databaseService) : IMediaStudioCreditRepository
{
    public async Task<IReadOnlyList<MediaStudioCredit>> GetForItemAsync(
        Guid mediaItemId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var itemId = mediaItemId.ToString();
        var records = await databaseService.Connection.Table<MediaStudioCreditRecord>()
            .Where(record => record.UserId == userId && record.MediaItemId == itemId)
            .ToListAsync();
        return records
            .OrderBy(record => record.Role)
            .ThenBy(record => record.SortOrder)
            .Select(ToDomain)
            .ToList();
    }

    public async Task ReplaceForItemAsync(
        Guid mediaItemId,
        string userId,
        IReadOnlyCollection<MediaStudioCredit> credits,
        CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var itemId = mediaItemId.ToString();
        var existing = await databaseService.Connection.Table<MediaStudioCreditRecord>()
            .Where(record => record.UserId == userId && record.MediaItemId == itemId)
            .ToListAsync();
        foreach (var record in existing)
        {
            await databaseService.Connection.DeleteAsync(record);
        }

        foreach (var credit in credits
                     .GroupBy(value => new { value.StudioId, value.Role })
                     .Select(group => group.OrderBy(value => value.SortOrder).First()))
        {
            await databaseService.Connection.InsertAsync(ToRecord(credit));
        }
    }

    private static MediaStudioCredit ToDomain(MediaStudioCreditRecord record) => new()
    {
        Id = Guid.Parse(record.Id),
        UserId = record.UserId,
        MediaItemId = Guid.Parse(record.MediaItemId),
        StudioId = Guid.Parse(record.StudioId),
        Role = (StudioRole)record.Role,
        SortOrder = record.SortOrder,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt
    };

    private static MediaStudioCreditRecord ToRecord(MediaStudioCredit value) => new()
    {
        Id = value.Id.ToString(),
        UserId = value.UserId,
        MediaItemId = value.MediaItemId.ToString(),
        StudioId = value.StudioId.ToString(),
        Role = (int)value.Role,
        SortOrder = value.SortOrder,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };
}

public sealed class MediaCollectionRepository(LocalDatabaseService databaseService) : IMediaCollectionRepository
{
    public async Task<IReadOnlyList<MediaCollection>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var records = await databaseService.Connection.Table<MediaCollectionRecord>()
            .Where(record => record.UserId == userId && record.DeletedAt == null)
            .ToListAsync();
        return records.OrderBy(record => record.Name).Select(ToDomain).ToList();
    }

    public async Task<MediaCollection?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var record = await databaseService.Connection.FindAsync<MediaCollectionRecord>(id.ToString());
        return record is null || record.UserId != userId || record.DeletedAt.HasValue ? null : ToDomain(record);
    }

    public async Task<MediaCollection?> GetByNameAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(name);
        return (await GetAllAsync(userId, cancellationToken)).FirstOrDefault(collection => Normalize(collection.Name) == normalized);
    }

    public async Task<MediaCollection> AddAsync(MediaCollection collection, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.InsertAsync(ToRecord(collection));
        return collection;
    }

    public async Task UpdateAsync(MediaCollection collection, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        collection.UpdatedAt = DateTime.UtcNow;
        await databaseService.Connection.UpdateAsync(ToRecord(collection));
    }

    public async Task<IReadOnlyList<MediaCollectionEntry>> GetEntriesAsync(Guid collectionId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var id = collectionId.ToString();
        var records = await databaseService.Connection.Table<MediaCollectionEntryRecord>()
            .Where(record => record.UserId == userId && record.CollectionId == id)
            .ToListAsync();
        return records.OrderBy(record => record.Position).Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<MediaCollectionEntry>> GetAllEntriesAsync(string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var records = await databaseService.Connection.Table<MediaCollectionEntryRecord>()
            .Where(record => record.UserId == userId)
            .ToListAsync();
        return records.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<MediaCollectionEntry>> GetEntriesForItemAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var id = mediaItemId.ToString();
        var records = await databaseService.Connection.Table<MediaCollectionEntryRecord>()
            .Where(record => record.UserId == userId && record.MediaItemId == id)
            .ToListAsync();
        return records.Select(ToDomain).ToList();
    }

    public async Task UpsertEntryAsync(MediaCollectionEntry entry, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        entry.UpdatedAt = DateTime.UtcNow;
        await databaseService.Connection.InsertOrReplaceAsync(ToRecord(entry));
    }

    public async Task RemoveEntryAsync(Guid entryId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var record = await databaseService.Connection.FindAsync<MediaCollectionEntryRecord>(entryId.ToString());
        if (record is not null && record.UserId == userId)
        {
            await databaseService.Connection.DeleteAsync(record);
        }
    }

    public async Task ReplaceEntryForItemAsync(Guid mediaItemId, string userId, MediaCollectionEntry? entry, CancellationToken cancellationToken = default)
    {
        var existing = await GetEntriesForItemAsync(mediaItemId, userId, cancellationToken);
        foreach (var value in existing)
        {
            await RemoveEntryAsync(value.Id, userId, cancellationToken);
        }

        if (entry is not null)
        {
            await UpsertEntryAsync(entry, cancellationToken);
        }
    }

    private static MediaCollection ToDomain(MediaCollectionRecord record) => new()
    {
        Id = Guid.Parse(record.Id), UserId = record.UserId, Name = record.Name,
        Kind = (MediaCollectionKind)record.Kind, Description = record.Description,
        ParentCollectionId = ParseGuid(record.ParentCollectionId), CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt, DeletedAt = record.DeletedAt
    };

    private static MediaCollectionRecord ToRecord(MediaCollection value) => new()
    {
        Id = value.Id.ToString(), UserId = value.UserId, Name = value.Name,
        NormalizedName = Normalize(value.Name), Kind = (int)value.Kind, Description = value.Description,
        ParentCollectionId = value.ParentCollectionId?.ToString(), CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt, DeletedAt = value.DeletedAt
    };

    private static MediaCollectionEntry ToDomain(MediaCollectionEntryRecord record) => new()
    {
        Id = Guid.Parse(record.Id), UserId = record.UserId, CollectionId = Guid.Parse(record.CollectionId),
        MediaItemId = Guid.Parse(record.MediaItemId), Position = record.Position,
        CreatedAt = record.CreatedAt, UpdatedAt = record.UpdatedAt
    };

    private static MediaCollectionEntryRecord ToRecord(MediaCollectionEntry value) => new()
    {
        Id = value.Id.ToString(), UserId = value.UserId, CollectionId = value.CollectionId.ToString(),
        MediaItemId = value.MediaItemId.ToString(), Position = value.Position,
        CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt
    };

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var parsed) ? parsed : null;
}

public sealed class MediaRelationRepository(LocalDatabaseService databaseService) : IMediaRelationRepository
{
    public async Task<IReadOnlyList<MediaRelation>> GetForItemAsync(Guid mediaItemId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var id = mediaItemId.ToString();
        var records = await databaseService.Connection.Table<MediaRelationRecord>()
            .Where(record => record.UserId == userId && (record.FromItemId == id || record.ToItemId == id))
            .ToListAsync();
        return records.Select(ToDomain).ToList();
    }

    public async Task<MediaRelation> AddAsync(MediaRelation relation, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.InsertAsync(ToRecord(relation));
        return relation;
    }

    public async Task RemoveAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var record = await databaseService.Connection.FindAsync<MediaRelationRecord>(id.ToString());
        if (record is not null && record.UserId == userId)
        {
            await databaseService.Connection.DeleteAsync(record);
        }
    }

    public async Task ReplaceFromItemAsync(Guid mediaItemId, string userId, IReadOnlyCollection<MediaRelation> relations, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var id = mediaItemId.ToString();
        var existing = await databaseService.Connection.Table<MediaRelationRecord>()
            .Where(record => record.UserId == userId && record.FromItemId == id)
            .ToListAsync();
        foreach (var record in existing)
        {
            await databaseService.Connection.DeleteAsync(record);
        }

        foreach (var relation in relations.Where(value => value.ToItemId != mediaItemId)
                     .GroupBy(value => new { value.ToItemId, value.Kind }).Select(group => group.First()))
        {
            await databaseService.Connection.InsertAsync(ToRecord(relation));
        }
    }

    private static MediaRelation ToDomain(MediaRelationRecord record) => new()
    {
        Id = Guid.Parse(record.Id), UserId = record.UserId, FromItemId = Guid.Parse(record.FromItemId),
        ToItemId = Guid.Parse(record.ToItemId), Kind = (MediaRelationKind)record.Kind, Notes = record.Notes,
        CreatedAt = record.CreatedAt, UpdatedAt = record.UpdatedAt
    };

    private static MediaRelationRecord ToRecord(MediaRelation value) => new()
    {
        Id = value.Id.ToString(), UserId = value.UserId, FromItemId = value.FromItemId.ToString(),
        ToItemId = value.ToItemId.ToString(), Kind = (int)value.Kind, Notes = value.Notes,
        CreatedAt = value.CreatedAt, UpdatedAt = value.UpdatedAt
    };
}
