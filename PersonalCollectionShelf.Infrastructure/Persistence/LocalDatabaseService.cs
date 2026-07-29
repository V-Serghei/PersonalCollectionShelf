using SQLite;
using PersonalCollectionShelf.Domain.Enums;
using PersonalCollectionShelf.Domain;

namespace PersonalCollectionShelf.Infrastructure.Persistence;

public sealed class LocalDatabaseService
{
    private static readonly object ProviderLock = new();
    private static bool _providerInitialized;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized;

    public LocalDatabaseService(string databasePath)
    {
        InitializeProvider();

        Connection = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    public SQLiteAsyncConnection Connection { get; }

    private static void InitializeProvider()
    {
        lock (ProviderLock)
        {
            if (_providerInitialized)
            {
                return;
            }

            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
            _providerInitialized = true;
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            await Connection.CreateTableAsync<MediaItemRecord>();
            await Connection.CreateTableAsync<PersonRecord>();
            await Connection.CreateTableAsync<PersonPhotoRecord>();
            await Connection.CreateTableAsync<StudioRecord>();
            await Connection.CreateTableAsync<MediaItemCastMemberRecord>();
            await Connection.CreateTableAsync<MediaCategoryRecord>();
            await Connection.CreateTableAsync<BookDetailsRecord>();
            await Connection.CreateTableAsync<MovieDetailsRecord>();
            await Connection.CreateTableAsync<EpisodicDetailsRecord>();
            await Connection.CreateTableAsync<GraphicPublicationDetailsRecord>();
            await Connection.CreateTableAsync<GameDetailsRecord>();
            await Connection.CreateTableAsync<TagRecord>();
            await Connection.CreateTableAsync<MediaItemTagRecord>();
            await Connection.CreateTableAsync<MediaContributionRecord>();
            await Connection.CreateTableAsync<MediaStudioCreditRecord>();
            await Connection.CreateTableAsync<CreditRoleRecord>();
            await Connection.CreateTableAsync<MediaCollectionRecord>();
            await Connection.CreateTableAsync<MediaCollectionEntryRecord>();
            await Connection.CreateTableAsync<MediaRelationRecord>();
            await Connection.CreateTableAsync<PersonRelationRecord>();
            await Connection.CreateTableAsync<ProfessionRecord>();
            await Connection.CreateTableAsync<PersonProfessionRecord>();

            await MigrateLegacyDataAsync();
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task MigrateLegacyDataAsync()
    {
        await SeedSystemCategoriesAsync();
        await SeedCreditRolesAsync();
        var mediaItems = await Connection.Table<MediaItemRecord>().ToListAsync();
        var tags = await Connection.Table<TagRecord>().ToListAsync();
        var itemTags = await Connection.Table<MediaItemTagRecord>().ToListAsync();
        var contributions = await Connection.Table<MediaContributionRecord>().ToListAsync();
        var studioCredits = await Connection.Table<MediaStudioCreditRecord>().ToListAsync();

        foreach (var contribution in contributions.Where(value => string.IsNullOrWhiteSpace(value.CreditRoleId)))
        {
            contribution.CreditRoleId = SystemEntityIds.CreditRole((ContributionRole)contribution.Role).ToString();
            await Connection.UpdateAsync(contribution);
        }

        foreach (var movie in mediaItems.Where(value =>
                     (value.MediaType == (int)MediaType.Movie || value.MediaType == (int)MediaType.Cartoon) &&
                     !string.IsNullOrWhiteSpace(value.StudioId) &&
                     !studioCredits.Any(credit =>
                         credit.MediaItemId == value.Id &&
                         credit.StudioId == value.StudioId)))
        {
            var now = DateTime.UtcNow;
            var credit = new MediaStudioCreditRecord
            {
                Id = Guid.NewGuid().ToString(),
                UserId = movie.UserId,
                MediaItemId = movie.Id,
                StudioId = movie.StudioId!,
                Role = (int)StudioRole.ProductionCompany,
                SortOrder = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            await Connection.InsertAsync(credit);
            studioCredits.Add(credit);
        }

        foreach (var item in mediaItems)
        {
            if (string.IsNullOrWhiteSpace(item.CategoryId) && Enum.IsDefined(typeof(MediaType), item.MediaType))
            {
                item.CategoryId = SystemEntityIds.MediaCategory((MediaType)item.MediaType).ToString();
                await Connection.UpdateAsync(item);
            }

            if (!string.IsNullOrWhiteSpace(item.Tags))
            {
                foreach (var tagName in SplitNames(item.Tags))
                {
                    var normalizedName = NormalizeName(tagName);
                    var tag = tags.FirstOrDefault(candidate =>
                        candidate.UserId == item.UserId &&
                        candidate.Kind == (int)TagKind.Tag &&
                        candidate.NormalizedName == normalizedName);

                    if (tag is null)
                    {
                        var now = DateTime.UtcNow;
                        tag = new TagRecord
                        {
                            Id = Guid.NewGuid().ToString(),
                            UserId = item.UserId,
                            Name = tagName,
                            NormalizedName = normalizedName,
                            Kind = (int)TagKind.Tag,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        await Connection.InsertAsync(tag);
                        tags.Add(tag);
                    }

                    if (!itemTags.Any(link => link.MediaItemId == item.Id && link.TagId == tag.Id))
                    {
                        var link = new MediaItemTagRecord
                        {
                            UserId = item.UserId,
                            MediaItemId = item.Id,
                            TagId = tag.Id
                        };
                        await Connection.InsertAsync(link);
                        itemTags.Add(link);
                    }
                }

                item.Tags = null;
                await Connection.UpdateAsync(item);
            }

            if (!string.IsNullOrWhiteSpace(item.CreatorId) &&
                !contributions.Any(credit => credit.MediaItemId == item.Id && credit.PersonId == item.CreatorId))
            {
                var role = ((MediaType)item.MediaType) switch
                {
                    MediaType.Book or MediaType.Manga or MediaType.Comic => ContributionRole.Author,
                    MediaType.Movie or MediaType.Series or MediaType.Cartoon or MediaType.AnimatedSeries => ContributionRole.Director,
                    MediaType.Game => ContributionRole.Developer,
                    _ => ContributionRole.Other
                };

                var now = DateTime.UtcNow;
                var contribution = new MediaContributionRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = item.UserId,
                    MediaItemId = item.Id,
                    PersonId = item.CreatorId,
                    Role = (int)role,
                    CreditRoleId = SystemEntityIds.CreditRole(role).ToString(),
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await Connection.InsertAsync(contribution);
                contributions.Add(contribution);
            }

            if ((MediaType)item.MediaType == MediaType.Book && !string.IsNullOrWhiteSpace(item.StudioId))
            {
                var existingDetails = await Connection.FindAsync<BookDetailsRecord>(item.Id);
                if (existingDetails is null)
                {
                    var now = DateTime.UtcNow;
                    await Connection.InsertAsync(new BookDetailsRecord
                    {
                        MediaItemId = item.Id,
                        UserId = item.UserId,
                        PublisherId = item.StudioId,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }
        }

        var legacyCast = await Connection.Table<MediaItemCastMemberRecord>().ToListAsync();
        foreach (var castMember in legacyCast)
        {
            var item = mediaItems.FirstOrDefault(candidate => candidate.Id == castMember.MediaItemId);
            if (item is null || contributions.Any(credit =>
                    credit.MediaItemId == castMember.MediaItemId &&
                    credit.PersonId == castMember.PersonId &&
                    credit.Role == (int)ContributionRole.Actor))
            {
                continue;
            }

            var now = DateTime.UtcNow;
            var contribution = new MediaContributionRecord
            {
                Id = Guid.NewGuid().ToString(),
                UserId = item.UserId,
                MediaItemId = castMember.MediaItemId,
                PersonId = castMember.PersonId,
                Role = (int)ContributionRole.Actor,
                CreditRoleId = SystemEntityIds.CreditRole(ContributionRole.Actor).ToString(),
                CreatedAt = now,
                UpdatedAt = now
            };
            await Connection.InsertAsync(contribution);
            contributions.Add(contribution);
        }
    }

    private static IEnumerable<string> SplitNames(string value)
    {
        return value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    private async Task SeedSystemCategoriesAsync()
    {
        var existing = await Connection.Table<MediaCategoryRecord>().Where(record => record.IsSystem).ToListAsync();
        foreach (var mediaType in Enum.GetValues<MediaType>())
        {
            var id = SystemEntityIds.MediaCategory(mediaType).ToString();
            if (existing.Any(record => record.Id == id))
            {
                continue;
            }

            var now = DateTime.UtcNow;
            await Connection.InsertAsync(new MediaCategoryRecord
            {
                Id = id,
                Key = mediaType.ToString(),
                Name = mediaType.ToString(),
                NormalizedName = mediaType.ToString().ToUpperInvariant(),
                BaseMediaType = (int)mediaType,
                IsSystem = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private async Task SeedCreditRolesAsync()
    {
        var existing = await Connection.Table<CreditRoleRecord>().ToListAsync();
        foreach (var role in Enum.GetValues<ContributionRole>())
        {
            var id = SystemEntityIds.CreditRole(role).ToString();
            if (existing.Any(record => record.Id == id))
            {
                continue;
            }

            await Connection.InsertAsync(new CreditRoleRecord
            {
                Id = id,
                Key = role.ToString(),
                DisplayName = role.ToString(),
                IsSystem = true
            });
        }
    }
}
