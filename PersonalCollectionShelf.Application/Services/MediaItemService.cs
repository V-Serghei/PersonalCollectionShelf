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
    private readonly IMediaStudioCreditRepository? _studioCredits;
    private readonly IMediaTypeDetailsRepository? _typeDetails;
    private readonly IBookDetailsRepository? _bookDetails;
    private readonly IMovieDetailsRepository? _movieDetails;
    private readonly IMediaCollectionRepository? _collections;
    private readonly IMediaRelationRepository? _relations;
    private readonly IMediaCategoryRepository? _categories;
    private readonly ITransactionRunner? _transactionRunner;

    public MediaItemService(
        IMediaItemRepository mediaItems,
        IPersonService people,
        IStudioService studios)
        : this(mediaItems, people, studios, null, null, null, null, null, null, null, null, null, null)
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
        ITransactionRunner? transactionRunner = null,
        IMovieDetailsRepository? movieDetails = null,
        IMediaStudioCreditRepository? studioCredits = null,
        IMediaTypeDetailsRepository? typeDetails = null)
    {
        _mediaItems = mediaItems;
        _people = people;
        _studios = studios;
        _tags = tags;
        _contributions = contributions;
        _studioCredits = studioCredits;
        _typeDetails = typeDetails;
        _bookDetails = bookDetails;
        _movieDetails = movieDetails;
        _collections = collections;
        _relations = relations;
        _categories = categories;
        _transactionRunner = transactionRunner;
    }

    public async Task<IReadOnlyList<MediaItemDto>> GetLibraryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var items = await _mediaItems.GetAllAsync(userId, cancellationToken);
        if (_tags is null)
        {
            return items.Select(item => ToLibraryDto(item, SplitTags(item.Tags), [])).ToList();
        }

        var tagsByItem = await _tags.GetForItemsAsync(
            items.Select(item => item.Id).ToList(),
            userId,
            cancellationToken);

        return items.Select(item =>
        {
            var itemTags = tagsByItem.GetValueOrDefault(item.Id) ?? [];
            return ToLibraryDto(
                item,
                itemTags.Where(tag => tag.Kind == TagKind.Tag).Select(tag => tag.Name).ToList(),
                itemTags.Where(tag => tag.Kind == TagKind.Genre).Select(tag => tag.Name).ToList());
        }).ToList();
    }

    public Task<int> GetLibraryItemCountAsync(string userId, CancellationToken cancellationToken = default) =>
        _mediaItems.CountAsync(userId, cancellationToken);

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
        var studioId = await ResolveStudioIdAsync(
            userId,
            ResolvePrimaryStudioName(request.Publisher ?? request.BookDetails?.Publisher, request.StudioCredits),
            cancellationToken);
        var mediaItem = new MediaItem
        {
            Id = request.Id ?? Guid.NewGuid(),
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
            TmdbId = request.TmdbId,
            ImdbId = Normalize(request.ImdbId),
            KinopoiskId = request.KinopoiskId,
            TmdbRating = request.TmdbRating,
            TmdbVoteCount = request.TmdbVoteCount,
            ImdbRating = request.ImdbRating,
            ImdbVoteCount = request.ImdbVoteCount,
            KinopoiskRating = request.KinopoiskRating,
            KinopoiskVoteCount = request.KinopoiskVoteCount,
            ExternalRatingsUpdatedAt = request.ExternalRatingsUpdatedAt,
            CatalogProvider = request.CatalogProvider,
            CatalogItemId = request.CatalogItemId,
            CatalogSourceUrl = request.CatalogSourceUrl,
            CatalogRatingPrimarySource = request.CatalogRatingPrimarySource,
            CatalogRatingPrimary = request.CatalogRatingPrimary,
            CatalogRatingPrimaryCount = request.CatalogRatingPrimaryCount,
            CatalogRatingSecondarySource = request.CatalogRatingSecondarySource,
            CatalogRatingSecondary = request.CatalogRatingSecondary,
            CatalogRatingSecondaryCount = request.CatalogRatingSecondaryCount,
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
        existing.StudioId = await ResolveStudioIdAsync(
            userId,
            ResolvePrimaryStudioName(request.Publisher ?? request.BookDetails?.Publisher, request.StudioCredits),
            cancellationToken);
        existing.SerialNumber = Normalize(request.SerialNumber);
        existing.TmdbId = request.TmdbId;
        existing.ImdbId = Normalize(request.ImdbId);
        existing.KinopoiskId = request.KinopoiskId;
        existing.TmdbRating = request.TmdbRating;
        existing.TmdbVoteCount = request.TmdbVoteCount;
        existing.ImdbRating = request.ImdbRating;
        existing.ImdbVoteCount = request.ImdbVoteCount;
        existing.KinopoiskRating = request.KinopoiskRating;
        existing.KinopoiskVoteCount = request.KinopoiskVoteCount;
        existing.ExternalRatingsUpdatedAt = request.ExternalRatingsUpdatedAt;
        existing.CatalogProvider = request.CatalogProvider;
        existing.CatalogItemId = request.CatalogItemId;
        existing.CatalogSourceUrl = request.CatalogSourceUrl;
        existing.CatalogRatingPrimarySource = request.CatalogRatingPrimarySource;
        existing.CatalogRatingPrimary = request.CatalogRatingPrimary;
        existing.CatalogRatingPrimaryCount = request.CatalogRatingPrimaryCount;
        existing.CatalogRatingSecondarySource = request.CatalogRatingSecondarySource;
        existing.CatalogRatingSecondary = request.CatalogRatingSecondary;
        existing.CatalogRatingSecondaryCount = request.CatalogRatingSecondaryCount;
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
            request.StudioCredits,
            request.BookDetails,
            request.MovieDetails,
            request.EpisodicDetails,
            request.GraphicPublicationDetails,
            request.GameDetails,
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
            request.StudioCredits,
            request.BookDetails,
            request.MovieDetails,
            request.EpisodicDetails,
            request.GraphicPublicationDetails,
            request.GameDetails,
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
        IReadOnlyList<StudioCreditInput>? studioCreditInputs,
        BookDetailsInput? bookInput,
        MovieDetailsInput? movieInput,
        EpisodicDetailsInput? episodicInput,
        GraphicPublicationDetailsInput? graphicInput,
        GameDetailsInput? gameInput,
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

        if (_studioCredits is not null)
        {
            var credits = await ResolveStudioCreditsAsync(item, studioCreditInputs, cancellationToken);
            await _studioCredits.ReplaceForItemAsync(item.Id, item.UserId, credits, cancellationToken);
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

        if (_movieDetails is not null &&
            (item.MediaType is MediaType.Movie or MediaType.Cartoon) &&
            movieInput is not null)
        {
            var existing = await _movieDetails.GetAsync(item.Id, item.UserId, cancellationToken);
            var now = DateTime.UtcNow;
            await _movieDetails.UpsertAsync(new MovieDetails
            {
                MediaItemId = item.Id,
                UserId = item.UserId,
                RuntimeMinutes = movieInput.RuntimeMinutes,
                OriginalLanguage = Normalize(movieInput.OriginalLanguage),
                Language = Normalize(movieInput.Language),
                CountryOfOrigin = Normalize(movieInput.CountryOfOrigin),
                AgeRating = Normalize(movieInput.AgeRating),
                CreatedAt = existing?.CreatedAt ?? now,
                UpdatedAt = now
            }, cancellationToken);
        }

        if (_typeDetails is not null &&
            (item.MediaType is MediaType.Series or MediaType.Anime or MediaType.AnimatedSeries) &&
            episodicInput is not null)
        {
            var existing = await _typeDetails.GetEpisodicAsync(item.Id, item.UserId, cancellationToken);
            var now = DateTime.UtcNow;
            await _typeDetails.UpsertEpisodicAsync(new EpisodicDetails
            {
                MediaItemId = item.Id, UserId = item.UserId,
                SeasonCount = episodicInput.SeasonCount, EpisodeCount = episodicInput.EpisodeCount,
                EpisodeRuntimeMinutes = episodicInput.EpisodeRuntimeMinutes,
                Network = Normalize(episodicInput.Network), AiringStatus = Normalize(episodicInput.AiringStatus),
                SourceMaterial = Normalize(episodicInput.SourceMaterial),
                OriginalLanguage = Normalize(episodicInput.OriginalLanguage),
                CreatedAt = existing?.CreatedAt ?? now, UpdatedAt = now
            }, cancellationToken);
        }

        if (_typeDetails is not null && item.MediaType is MediaType.Manga or MediaType.Comic && graphicInput is not null)
        {
            var existing = await _typeDetails.GetGraphicPublicationAsync(item.Id, item.UserId, cancellationToken);
            var now = DateTime.UtcNow;
            await _typeDetails.UpsertGraphicPublicationAsync(new GraphicPublicationDetails
            {
                MediaItemId = item.Id, UserId = item.UserId,
                VolumeCount = graphicInput.VolumeCount, ChapterOrIssueCount = graphicInput.ChapterOrIssueCount,
                ReadingDirection = Normalize(graphicInput.ReadingDirection), IsColor = graphicInput.IsColor,
                PublicationStatus = Normalize(graphicInput.PublicationStatus), Imprint = Normalize(graphicInput.Imprint),
                OriginalLanguage = Normalize(graphicInput.OriginalLanguage),
                CreatedAt = existing?.CreatedAt ?? now, UpdatedAt = now
            }, cancellationToken);
        }

        if (_typeDetails is not null && item.MediaType == MediaType.Game && gameInput is not null)
        {
            var existing = await _typeDetails.GetGameAsync(item.Id, item.UserId, cancellationToken);
            var now = DateTime.UtcNow;
            await _typeDetails.UpsertGameAsync(new GameDetails
            {
                MediaItemId = item.Id, UserId = item.UserId, Platform = Normalize(gameInput.Platform),
                MainStoryHours = gameInput.MainStoryHours, CompletionistHours = gameInput.CompletionistHours,
                GameMode = Normalize(gameInput.GameMode), Engine = Normalize(gameInput.Engine),
                Region = Normalize(gameInput.Region), CreatedAt = existing?.CreatedAt ?? now, UpdatedAt = now
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

    private async Task<IReadOnlyCollection<MediaStudioCredit>> ResolveStudioCreditsAsync(
        MediaItem item,
        IReadOnlyList<StudioCreditInput>? inputs,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var result = new List<MediaStudioCredit>();
        foreach (var input in inputs ?? [])
        {
            Guid? studioId = null;
            if (input.StudioId.HasValue)
            {
                studioId = (await _studios.GetByIdAsync(item.UserId, input.StudioId.Value, cancellationToken))?.Id;
            }

            studioId ??= await ResolveStudioIdAsync(item.UserId, input.Name, cancellationToken);
            if (!studioId.HasValue)
            {
                continue;
            }

            result.Add(new MediaStudioCredit
            {
                Id = Guid.NewGuid(),
                UserId = item.UserId,
                MediaItemId = item.Id,
                StudioId = studioId.Value,
                Role = input.Role,
                SortOrder = Math.Max(0, input.SortOrder),
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

    private static MediaItemDto ToLibraryDto(
        MediaItem item,
        IReadOnlyList<string> tagNames,
        IReadOnlyList<string> genres) => new()
    {
        Id = item.Id,
        UserId = item.UserId,
        Title = item.Title,
        OriginalTitle = item.OriginalTitle,
        Description = item.Description,
        Category = item.Category,
        MediaCategoryId = item.CategoryId,
        Tags = tagNames.Count == 0 ? null : string.Join(", ", tagNames),
        TagNames = tagNames,
        Genres = genres,
        CreatorId = item.CreatorId,
        StudioId = item.StudioId,
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

        var studioCreditEntities = _studioCredits is null
            ? []
            : await _studioCredits.GetForItemAsync(item.Id, item.UserId, cancellationToken);
        var studioCreditDtos = new List<MediaStudioCreditDto>();
        foreach (var credit in studioCreditEntities)
        {
            var studio = await _studios.GetByIdAsync(item.UserId, credit.StudioId, cancellationToken);
            if (studio is not null)
            {
                studioCreditDtos.Add(new MediaStudioCreditDto(
                    credit.Id,
                    credit.StudioId,
                    studio.Name,
                    credit.Role,
                    credit.SortOrder));
            }
        }

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

        MovieDetailsDto? movieDto = null;
        if (_movieDetails is not null && item.MediaType is MediaType.Movie or MediaType.Cartoon)
        {
            var details = await _movieDetails.GetAsync(item.Id, item.UserId, cancellationToken);
            if (details is not null)
            {
                movieDto = new MovieDetailsDto
                {
                    RuntimeMinutes = details.RuntimeMinutes,
                    OriginalLanguage = details.OriginalLanguage,
                    Language = details.Language,
                    CountryOfOrigin = details.CountryOfOrigin,
                    AgeRating = details.AgeRating
                };
            }
        }

        EpisodicDetailsInput? episodicDto = null;
        GraphicPublicationDetailsInput? graphicDto = null;
        GameDetailsInput? gameDto = null;
        if (_typeDetails is not null && item.MediaType is MediaType.Series or MediaType.Anime or MediaType.AnimatedSeries)
        {
            var value = await _typeDetails.GetEpisodicAsync(item.Id, item.UserId, cancellationToken);
            if (value is not null)
            {
                episodicDto = new EpisodicDetailsInput
                {
                    SeasonCount = value.SeasonCount, EpisodeCount = value.EpisodeCount,
                    EpisodeRuntimeMinutes = value.EpisodeRuntimeMinutes, Network = value.Network,
                    AiringStatus = value.AiringStatus, SourceMaterial = value.SourceMaterial,
                    OriginalLanguage = value.OriginalLanguage
                };
            }
        }
        else if (_typeDetails is not null && item.MediaType is MediaType.Manga or MediaType.Comic)
        {
            var value = await _typeDetails.GetGraphicPublicationAsync(item.Id, item.UserId, cancellationToken);
            if (value is not null)
            {
                graphicDto = new GraphicPublicationDetailsInput
                {
                    VolumeCount = value.VolumeCount, ChapterOrIssueCount = value.ChapterOrIssueCount,
                    ReadingDirection = value.ReadingDirection, IsColor = value.IsColor,
                    PublicationStatus = value.PublicationStatus, Imprint = value.Imprint,
                    OriginalLanguage = value.OriginalLanguage
                };
            }
        }
        else if (_typeDetails is not null && item.MediaType == MediaType.Game)
        {
            var value = await _typeDetails.GetGameAsync(item.Id, item.UserId, cancellationToken);
            if (value is not null)
            {
                gameDto = new GameDetailsInput
                {
                    Platform = value.Platform, MainStoryHours = value.MainStoryHours,
                    CompletionistHours = value.CompletionistHours, GameMode = value.GameMode,
                    Engine = value.Engine, Region = value.Region
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
            TmdbId = item.TmdbId,
            ImdbId = item.ImdbId,
            KinopoiskId = item.KinopoiskId,
            TmdbRating = item.TmdbRating,
            TmdbVoteCount = item.TmdbVoteCount,
            ImdbRating = item.ImdbRating,
            ImdbVoteCount = item.ImdbVoteCount,
            KinopoiskRating = item.KinopoiskRating,
            KinopoiskVoteCount = item.KinopoiskVoteCount,
            ExternalRatingsUpdatedAt = item.ExternalRatingsUpdatedAt,
            CatalogProvider = item.CatalogProvider,
            CatalogItemId = item.CatalogItemId,
            CatalogSourceUrl = item.CatalogSourceUrl,
            CatalogRatingPrimarySource = item.CatalogRatingPrimarySource,
            CatalogRatingPrimary = item.CatalogRatingPrimary,
            CatalogRatingPrimaryCount = item.CatalogRatingPrimaryCount,
            CatalogRatingSecondarySource = item.CatalogRatingSecondarySource,
            CatalogRatingSecondary = item.CatalogRatingSecondary,
            CatalogRatingSecondaryCount = item.CatalogRatingSecondaryCount,
            Cast = castNames,
            Contributions = contributionDtos,
            StudioCredits = studioCreditDtos,
            BookDetails = bookDto,
            MovieDetails = movieDto,
            EpisodicDetails = episodicDto,
            GraphicPublicationDetails = graphicDto,
            GameDetails = gameDto,
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
        MediaType.Movie or MediaType.Series or MediaType.Cartoon or MediaType.AnimatedSeries => ContributionRole.Director,
        MediaType.Game => ContributionRole.Developer,
        _ => ContributionRole.Other
    };

    private static string? ResolvePrimaryStudioName(
        string? legacyName,
        IReadOnlyList<StudioCreditInput>? studioCredits)
    {
        var productionCompany = studioCredits?
            .Where(value => value.Role == StudioRole.ProductionCompany)
            .OrderBy(value => value.SortOrder)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value.Name));
        return productionCompany?.Name ?? legacyName;
    }

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
