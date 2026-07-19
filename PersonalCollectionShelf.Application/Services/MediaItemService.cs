using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Application.Validation;
using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Domain.Enums;
using PersonalCollectionShelf.Domain;

namespace PersonalCollectionShelf.Application.Services;

public sealed class MediaItemService : IMediaItemService
{
    private readonly IMediaItemRepository _mediaItems;
    private readonly IPersonService _people;
    private readonly IStudioService _studios;
    private readonly ITagRepository? _tags;
    private readonly IMediaContributionRepository? _contributions;
    private readonly IBookDetailsRepository? _bookDetails;
    private readonly IMediaCollectionRepository? _collections;
    private readonly IMediaRelationRepository? _relations;
    private readonly IMediaCategoryRepository? _categories;
    private readonly ITransactionRunner? _transactionRunner;

    public MediaItemService(
        IMediaItemRepository mediaItems,
        IPersonService people,
        IStudioService studios)
        : this(mediaItems, people, studios, null, null, null, null, null, null, null)
    {
    }

    public MediaItemService(
        IMediaItemRepository mediaItems,
        IPersonService people,
        IStudioService studios,
        ITagRepository? tags,
        IMediaContributionRepository? contributions,
        IBookDetailsRepository? bookDetails,
        IMediaCollectionRepository? collections,
        IMediaRelationRepository? relations,
        IMediaCategoryRepository? categories = null,
        ITransactionRunner? transactionRunner = null)
    {
        _mediaItems = mediaItems;
        _people = people;
        _studios = studios;
        _tags = tags;
        _contributions = contributions;
        _bookDetails = bookDetails;
        _collections = collections;
        _relations = relations;
        _categories = categories;
        _transactionRunner = transactionRunner;
    }

    public async Task<IReadOnlyList<MediaItemDto>> GetLibraryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var items = await _mediaItems.GetAllAsync(userId, cancellationToken);
        return await ToDtosAsync(items, cancellationToken);
    }

    public async Task<MediaItemDto?> GetMediaItemAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var item = await _mediaItems.GetByIdAsync(id, userId, cancellationToken);
        return item is null ? null : await ToDtoAsync(item, cancellationToken);
    }

    public async Task<MediaItemDto> CreateMediaItemAsync(CreateMediaItemRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfInvalid(MediaItemValidator.Validate(request));

        if (_transactionRunner is not null)
        {
            return await _transactionRunner.ExecuteAsync(
                transactionCancellation => CreateMediaItemCoreAsync(request, transactionCancellation),
                cancellationToken);
        }

        return await CreateMediaItemCoreAsync(request, cancellationToken);
    }

    private async Task<MediaItemDto> CreateMediaItemCoreAsync(CreateMediaItemRequest request, CancellationToken cancellationToken)
    {

        var now = DateTime.UtcNow;
        var userId = request.UserId.Trim();
        var creatorId = await ResolvePersonIdAsync(userId, request.Creator, cancellationToken);
        var studioId = await ResolveStudioIdAsync(userId, request.Publisher ?? request.BookDetails?.Publisher, cancellationToken);
        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title.Trim(),
            OriginalTitle = Normalize(request.OriginalTitle),
            Description = Normalize(request.Description),
            Category = Normalize(request.Category),
            CategoryId = await ResolveCategoryIdAsync(request.MediaCategoryId, userId, request.MediaType, cancellationToken),
            Tags = _tags is null ? NormalizeTags(request.Tags) : null,
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

        var created = await _mediaItems.AddAsync(mediaItem, cancellationToken);
        if (_transactionRunner is not null)
        {
            await SaveAssociatedDataAsync(created, request, cancellationToken);
            return await ToDtoAsync(created, cancellationToken);
        }

        try
        {
            await SaveAssociatedDataAsync(created, request, cancellationToken);
            return await ToDtoAsync(created, cancellationToken);
        }
        catch
        {
            await _mediaItems.DeleteAsync(created.Id, created.UserId, cancellationToken);
            throw;
        }
    }

    public async Task<MediaItemDto> UpdateMediaItemAsync(UpdateMediaItemRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfInvalid(MediaItemValidator.Validate(request));

        if (_transactionRunner is not null)
        {
            return await _transactionRunner.ExecuteAsync(
                transactionCancellation => UpdateMediaItemCoreAsync(request, transactionCancellation),
                cancellationToken);
        }

        return await UpdateMediaItemCoreAsync(request, cancellationToken);
    }

    private async Task<MediaItemDto> UpdateMediaItemCoreAsync(UpdateMediaItemRequest request, CancellationToken cancellationToken)
    {

        var existing = await _mediaItems.GetByIdAsync(request.Id, request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Media item was not found.");
        var userId = request.UserId.Trim();

        existing.Title = request.Title.Trim();
        existing.OriginalTitle = Normalize(request.OriginalTitle);
        existing.Description = Normalize(request.Description);
        existing.Category = Normalize(request.Category);
        existing.CategoryId = await ResolveCategoryIdAsync(request.MediaCategoryId, userId, request.MediaType, cancellationToken);
        existing.Tags = _tags is null ? NormalizeTags(request.Tags) : null;
        existing.CreatorId = await ResolvePersonIdAsync(userId, request.Creator, cancellationToken);
        existing.StudioId = await ResolveStudioIdAsync(userId, request.Publisher ?? request.BookDetails?.Publisher, cancellationToken);
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

        var updated = await _mediaItems.UpdateAsync(existing, cancellationToken);
        await SaveAssociatedDataAsync(updated, request, cancellationToken);
        return await ToDtoAsync(updated, cancellationToken);
    }

    public Task DeleteMediaItemAsync(Guid id, string userId, CancellationToken cancellationToken = default) =>
        _mediaItems.DeleteAsync(id, userId, cancellationToken);

    public async Task<IReadOnlyList<MediaItemDto>> SearchMediaItemsAsync(MediaItemSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var items = await _mediaItems.SearchAsync(
            criteria.UserId,
            criteria.SearchTerm,
            criteria.MediaType,
            criteria.Status,
            criteria.Category,
            _tags is null ? criteria.Tag : null,
            cancellationToken);

        if (_tags is not null && !string.IsNullOrWhiteSpace(criteria.Tag))
        {
            var filtered = new List<MediaItem>();
            foreach (var item in items)
            {
                var itemTags = await _tags.GetForItemAsync(item.Id, item.UserId, TagKind.Tag, cancellationToken);
                if (itemTags.Any(tag => string.Equals(tag.Name, criteria.Tag.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    filtered.Add(item);
                }
            }

            items = filtered;
        }

        return await ToDtosAsync(items, cancellationToken);
    }

    private async Task SaveAssociatedDataAsync(MediaItem item, CreateMediaItemRequest request, CancellationToken cancellationToken)
    {
        await SaveAssociatedDataCoreAsync(
            item,
            request.Tags,
            request.TagNames,
            request.Genres,
            request.Creator,
            request.Cast,
            request.Contributions,
            request.BookDetails,
            request.Collection,
            request.Relations,
            cancellationToken);
    }

    private async Task SaveAssociatedDataAsync(MediaItem item, UpdateMediaItemRequest request, CancellationToken cancellationToken)
    {
        await SaveAssociatedDataCoreAsync(
            item,
            request.Tags,
            request.TagNames,
            request.Genres,
            request.Creator,
            request.Cast,
            request.Contributions,
            request.BookDetails,
            request.Collection,
            request.Relations,
            cancellationToken);
    }

    private async Task SaveAssociatedDataCoreAsync(
        MediaItem item,
        string? legacyTags,
        IReadOnlyList<string>? tagNames,
        IReadOnlyList<string>? genres,
        string? legacyCreator,
        IReadOnlyList<string>? legacyCast,
        IReadOnlyList<PersonCreditInput>? contributionInputs,
        BookDetailsInput? bookInput,
        CollectionMembershipInput? collectionInput,
        IReadOnlyList<MediaRelationInput>? relationInputs,
        CancellationToken cancellationToken)
    {
        if (_tags is not null)
        {
            await _tags.SetForItemAsync(item.Id, item.UserId, tagNames ?? SplitTags(legacyTags), TagKind.Tag, cancellationToken);
            await _tags.SetForItemAsync(item.Id, item.UserId, genres ?? [], TagKind.Genre, cancellationToken);
        }

        if (_contributions is not null)
        {
            var credits = await ResolveContributionsAsync(item, contributionInputs, legacyCreator, legacyCast, cancellationToken);
            await _contributions.ReplaceForItemAsync(item.Id, item.UserId, credits, cancellationToken);
        }
        else
        {
            var castIds = await ResolvePersonIdsAsync(item.UserId, legacyCast, cancellationToken);
            await _mediaItems.ReplaceCastAsync(item.Id, castIds, cancellationToken);
        }

        if (_bookDetails is not null && item.MediaType == MediaType.Book && (bookInput is not null || item.StudioId.HasValue))
        {
            var existing = await _bookDetails.GetAsync(item.Id, item.UserId, cancellationToken);
            var now = DateTime.UtcNow;
            await _bookDetails.UpsertAsync(new BookDetails
            {
                MediaItemId = item.Id,
                UserId = item.UserId,
                PublisherId = item.StudioId,
                Subtitle = Normalize(bookInput?.Subtitle),
                Edition = Normalize(bookInput?.Edition),
                EditionNumber = bookInput?.EditionNumber,
                EditionYear = bookInput?.EditionYear,
                OriginalPublicationYear = bookInput?.OriginalPublicationYear,
                TranslationYear = bookInput?.TranslationYear,
                OriginalLanguage = Normalize(bookInput?.OriginalLanguage),
                Language = Normalize(bookInput?.Language),
                PageCount = bookInput?.PageCount,
                Isbn10 = NormalizeIsbn(bookInput?.Isbn10),
                Isbn13 = NormalizeIsbn(bookInput?.Isbn13),
                Format = bookInput?.Format,
                Binding = Normalize(bookInput?.Binding),
                CountryOfOrigin = Normalize(bookInput?.CountryOfOrigin),
                AgeRating = Normalize(bookInput?.AgeRating),
                CreatedAt = existing?.CreatedAt ?? now,
                UpdatedAt = now
            }, cancellationToken);
        }

        if (_collections is not null)
        {
            MediaCollectionEntry? entry = null;
            if (collectionInput is not null &&
                (collectionInput.CollectionId.HasValue || !string.IsNullOrWhiteSpace(collectionInput.Name)))
            {
                MediaCollection? collection = null;
                if (collectionInput.CollectionId.HasValue)
                {
                    collection = await _collections.GetByIdAsync(collectionInput.CollectionId.Value, item.UserId, cancellationToken);
                }

                if (collection is null && !string.IsNullOrWhiteSpace(collectionInput.Name))
                {
                    collection = await _collections.GetByNameAsync(item.UserId, collectionInput.Name, cancellationToken);
                    if (collection is null)
                    {
                        var now = DateTime.UtcNow;
                        collection = await _collections.AddAsync(new MediaCollection
                        {
                            Id = Guid.NewGuid(),
                            UserId = item.UserId,
                            Name = collectionInput.Name.Trim(),
                            Kind = collectionInput.Kind,
                            CreatedAt = now,
                            UpdatedAt = now
                        }, cancellationToken);
                    }
                }

                if (collection is not null)
                {
                    var now = DateTime.UtcNow;
                    entry = new MediaCollectionEntry
                    {
                        Id = Guid.NewGuid(),
                        UserId = item.UserId,
                        CollectionId = collection.Id,
                        MediaItemId = item.Id,
                        Position = collectionInput.Position,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                }
            }

            await _collections.ReplaceEntryForItemAsync(item.Id, item.UserId, entry, cancellationToken);
        }

        if (_relations is not null)
        {
            var now = DateTime.UtcNow;
            var relations = (relationInputs ?? [])
                .Where(input => input.RelatedItemId != Guid.Empty && input.RelatedItemId != item.Id)
                .Select(input => new MediaRelation
                {
                    Id = Guid.NewGuid(),
                    UserId = item.UserId,
                    FromItemId = item.Id,
                    ToItemId = input.RelatedItemId,
                    Kind = input.Kind,
                    Notes = Normalize(input.Notes),
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList();
            await _relations.ReplaceFromItemAsync(item.Id, item.UserId, relations, cancellationToken);
        }
    }

    private async Task<IReadOnlyCollection<MediaContribution>> ResolveContributionsAsync(
        MediaItem item,
        IReadOnlyList<PersonCreditInput>? inputs,
        string? legacyCreator,
        IReadOnlyList<string>? legacyCast,
        CancellationToken cancellationToken)
    {
        var normalizedInputs = new List<PersonCreditInput>(inputs ?? []);
        if (normalizedInputs.Count == 0 && !string.IsNullOrWhiteSpace(legacyCreator))
        {
            normalizedInputs.Add(new PersonCreditInput
            {
                Name = legacyCreator,
                Role = GetPrimaryRole(item.MediaType),
                SortOrder = 0
            });
        }

        foreach (var actor in legacyCast ?? [])
        {
            normalizedInputs.Add(new PersonCreditInput { Name = actor, Role = ContributionRole.Actor });
        }

        var now = DateTime.UtcNow;
        var result = new List<MediaContribution>();
        foreach (var input in normalizedInputs)
        {
            Guid? personId = null;
            if (input.PersonId.HasValue)
            {
                personId = (await _people.GetByIdAsync(item.UserId, input.PersonId.Value, cancellationToken))?.Id;
            }

            personId ??= await ResolvePersonIdAsync(item.UserId, input.Name, cancellationToken);
            if (!personId.HasValue)
            {
                continue;
            }

            result.Add(new MediaContribution
            {
                Id = Guid.NewGuid(),
                UserId = item.UserId,
                MediaItemId = item.Id,
                PersonId = personId.Value,
                CreditRoleId = input.CreditRoleId ?? SystemEntityIds.CreditRole(input.Role),
                Role = input.Role,
                SortOrder = Math.Max(0, input.SortOrder),
                Details = Normalize(input.Details),
                CreditedAs = Normalize(input.CreditedAs),
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        return result;
    }

    private async Task<IReadOnlyList<Guid>> ResolvePersonIdsAsync(string userId, IReadOnlyList<string>? names, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();
        foreach (var name in names ?? [])
        {
            var id = await ResolvePersonIdAsync(userId, name, cancellationToken);
            if (id.HasValue && !ids.Contains(id.Value))
            {
                ids.Add(id.Value);
            }
        }

        return ids;
    }

    private async Task<Guid?> ResolvePersonIdAsync(string userId, string? name, CancellationToken cancellationToken)
    {
        var normalized = Normalize(name);
        return normalized is null ? null : (await _people.GetOrCreateAsync(userId, normalized, cancellationToken)).Id;
    }

    private async Task<Guid?> ResolveStudioIdAsync(string userId, string? name, CancellationToken cancellationToken)
    {
        var normalized = Normalize(name);
        return normalized is null ? null : (await _studios.GetOrCreateAsync(userId, normalized, cancellationToken)).Id;
    }

    private async Task<Guid?> ResolveCategoryIdAsync(Guid? requestedId, string userId, MediaType mediaType, CancellationToken cancellationToken)
    {
        if (_categories is null)
        {
            return requestedId;
        }

        if (requestedId.HasValue)
        {
            var requested = await _categories.GetByIdAsync(requestedId.Value, userId, cancellationToken);
            if (requested is not null)
            {
                return requested.Id;
            }
        }

        return (await _categories.GetSystemAsync(mediaType, cancellationToken)).Id;
    }

    private async Task<IReadOnlyList<MediaItemDto>> ToDtosAsync(IReadOnlyList<MediaItem> items, CancellationToken cancellationToken)
    {
        var result = new List<MediaItemDto>(items.Count);
        foreach (var item in items)
        {
            result.Add(await ToDtoAsync(item, cancellationToken));
        }

        return result;
    }

    private async Task<MediaItemDto> ToDtoAsync(MediaItem item, CancellationToken cancellationToken)
    {
        var tagNames = _tags is null
            ? SplitTags(item.Tags)
            : (await _tags.GetForItemAsync(item.Id, item.UserId, TagKind.Tag, cancellationToken)).Select(tag => tag.Name).ToList();
        var genres = _tags is null
            ? []
            : (await _tags.GetForItemAsync(item.Id, item.UserId, TagKind.Genre, cancellationToken)).Select(tag => tag.Name).ToList();

        var contributionEntities = _contributions is null
            ? []
            : await _contributions.GetForItemAsync(item.Id, item.UserId, cancellationToken);
        var peopleById = contributionEntities.Count == 0
            ? new Dictionary<Guid, PersonDto>()
            : (await _people.GetByIdsAsync(item.UserId, contributionEntities.Select(value => value.PersonId).Distinct().ToList(), cancellationToken))
                .ToDictionary(person => person.Id);
        var contributionDtos = contributionEntities
            .Where(value => peopleById.ContainsKey(value.PersonId))
            .Select(value => new MediaContributionDto(
                value.Id,
                value.PersonId,
                peopleById[value.PersonId].Name,
                value.CreditRoleId,
                value.Role,
                value.SortOrder,
                value.Details,
                value.CreditedAs))
            .ToList();

        string? creatorName = null;
        if (item.CreatorId.HasValue)
        {
            creatorName = (await _people.GetByIdAsync(item.UserId, item.CreatorId.Value, cancellationToken))?.Name;
        }
        creatorName ??= contributionDtos.OrderBy(value => value.SortOrder)
            .FirstOrDefault(value => value.Role == GetPrimaryRole(item.MediaType))?.PersonName;

        var castNames = contributionDtos.Where(value => value.Role == ContributionRole.Actor)
            .OrderBy(value => value.SortOrder).Select(value => value.PersonName).ToList();
        if (_contributions is null)
        {
            var castIds = await _mediaItems.GetCastPersonIdsAsync(item.Id, cancellationToken);
            castNames = castIds.Count == 0
                ? []
                : (await _people.GetByIdsAsync(item.UserId, castIds, cancellationToken)).Select(person => person.Name).ToList();
        }

        string? publisherName = null;
        if (item.StudioId.HasValue)
        {
            publisherName = (await _studios.GetByIdAsync(item.UserId, item.StudioId.Value, cancellationToken))?.Name;
        }

        BookDetailsDto? bookDto = null;
        if (_bookDetails is not null && item.MediaType == MediaType.Book)
        {
            var details = await _bookDetails.GetAsync(item.Id, item.UserId, cancellationToken);
            if (details is not null)
            {
                if (details.PublisherId.HasValue)
                {
                    publisherName = (await _studios.GetByIdAsync(item.UserId, details.PublisherId.Value, cancellationToken))?.Name;
                }

                bookDto = new BookDetailsDto
                {
                    Subtitle = details.Subtitle,
                    PublisherId = details.PublisherId,
                    Publisher = publisherName,
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
                    Format = details.Format,
                    Binding = details.Binding,
                    CountryOfOrigin = details.CountryOfOrigin,
                    AgeRating = details.AgeRating
                };
            }
        }

        CollectionMembershipDto? collectionDto = null;
        if (_collections is not null)
        {
            var entry = (await _collections.GetEntriesForItemAsync(item.Id, item.UserId, cancellationToken)).FirstOrDefault();
            if (entry is not null)
            {
                var collection = await _collections.GetByIdAsync(entry.CollectionId, item.UserId, cancellationToken);
                if (collection is not null)
                {
                    collectionDto = new CollectionMembershipDto(entry.Id, collection.Id, collection.Name, collection.Kind, entry.Position);
                }
            }
        }

        var relationDtos = new List<MediaRelationDto>();
        if (_relations is not null)
        {
            foreach (var relation in await _relations.GetForItemAsync(item.Id, item.UserId, cancellationToken))
            {
                var outgoing = relation.FromItemId == item.Id;
                var relatedId = outgoing ? relation.ToItemId : relation.FromItemId;
                var related = await _mediaItems.GetByIdAsync(relatedId, item.UserId, cancellationToken);
                if (related is not null)
                {
                    relationDtos.Add(new MediaRelationDto(relation.Id, relatedId, related.Title, relation.Kind, relation.Notes, outgoing));
                }
            }
        }

        string? mediaCategoryName = null;
        if (_categories is not null && item.CategoryId.HasValue)
        {
            mediaCategoryName = (await _categories.GetByIdAsync(item.CategoryId.Value, item.UserId, cancellationToken))?.Name;
        }

        return new MediaItemDto
        {
            Id = item.Id,
            UserId = item.UserId,
            Title = item.Title,
            OriginalTitle = item.OriginalTitle,
            Description = item.Description,
            Category = item.Category,
            MediaCategoryId = item.CategoryId,
            MediaCategoryName = mediaCategoryName,
            Tags = tagNames.Count == 0 ? null : string.Join(", ", tagNames),
            TagNames = tagNames,
            Genres = genres,
            CreatorId = item.CreatorId,
            Creator = creatorName,
            StudioId = item.StudioId,
            Publisher = publisherName,
            SerialNumber = item.SerialNumber,
            Cast = castNames,
            Contributions = contributionDtos,
            BookDetails = bookDto,
            Collection = collectionDto,
            Relations = relationDtos,
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

    private static ContributionRole GetPrimaryRole(MediaType mediaType) => mediaType switch
    {
        MediaType.Book or MediaType.Manga or MediaType.Comic => ContributionRole.Author,
        MediaType.Movie or MediaType.Series => ContributionRole.Director,
        MediaType.Game => ContributionRole.Developer,
        _ => ContributionRole.Other
    };

    private static void ThrowIfInvalid(ValidationResult result)
    {
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeIsbn(string? value)
    {
        var normalized = Normalize(value);
        return normalized?.Replace("-", string.Empty).Replace(" ", string.Empty);
    }

    private static IReadOnlyList<string> SplitTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeTags(string? value)
    {
        var tags = SplitTags(value);
        return tags.Count == 0 ? null : string.Join(", ", tags);
    }
}
