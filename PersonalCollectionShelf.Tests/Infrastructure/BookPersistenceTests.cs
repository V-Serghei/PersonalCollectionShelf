using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Services;
using PersonalCollectionShelf.Application.Validation;
using PersonalCollectionShelf.Domain.Enums;
using PersonalCollectionShelf.Infrastructure.Persistence;
using PersonalCollectionShelf.Infrastructure.Repositories;

namespace PersonalCollectionShelf.Tests.Infrastructure;

public sealed class BookPersistenceTests
{
    [Fact]
    public async Task Create_and_reload_book_persists_normalized_graph()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"pcs-book-{Guid.NewGuid():N}.db3");
        LocalDatabaseService? database = null;
        try
        {
            database = new LocalDatabaseService(databasePath);
            var service = CreateService(database);

            var adaptation = await service.CreateMediaItemAsync(new CreateMediaItemRequest
            {
                UserId = "user-1",
                Title = "The Left Hand of Darkness — adaptation",
                MediaType = MediaType.Movie
            });

            var created = await service.CreateMediaItemAsync(new CreateMediaItemRequest
            {
                UserId = "user-1",
                Title = "The Left Hand of Darkness",
                MediaType = MediaType.Book,
                Rating = 9.3m,
                TagNames = ["favorite", "Favorite", "winter"],
                Genres = ["Science fiction"],
                Contributions =
                [
                    new PersonCreditInput { Name = "Ursula K. Le Guin", Role = ContributionRole.Author, SortOrder = 0 },
                    new PersonCreditInput { Name = "Test Translator", Role = ContributionRole.Translator, SortOrder = 0 }
                ],
                BookDetails = new BookDetailsInput
                {
                    Publisher = "Ace Books",
                    Subtitle = "A novel",
                    OriginalPublicationYear = 1969,
                    EditionYear = 2019,
                    PageCount = 304,
                    Isbn13 = "9780441478125",
                    Format = BookFormat.Paperback,
                    Language = "English"
                },
                Collection = new CollectionMembershipInput
                {
                    Name = "Hainish Cycle",
                    Kind = MediaCollectionKind.Cycle,
                    Position = 4
                },
                Relations = [new MediaRelationInput { RelatedItemId = adaptation.Id, Kind = MediaRelationKind.Adaptation }]
            });

            Assert.Equal(9.3m, created.Rating);
            Assert.NotNull(created.MediaCategoryId);
            Assert.Equal("Book", created.MediaCategoryName);
            Assert.Equal(["favorite", "winter"], created.TagNames, StringComparer.OrdinalIgnoreCase);
            Assert.Single(created.Genres);
            Assert.Equal(2, created.Contributions.Count);
            Assert.Equal("Ace Books", created.BookDetails?.Publisher);
            Assert.Equal(304, created.BookDetails?.PageCount);
            Assert.Equal("Hainish Cycle", created.Collection?.Name);
            Assert.Equal(adaptation.Id, Assert.Single(created.Relations).RelatedItemId);

            var reloaded = await service.GetMediaItemAsync(created.Id, "user-1");

            Assert.NotNull(reloaded);
            Assert.Equal(9.3m, reloaded!.Rating);
            Assert.Equal("Ursula K. Le Guin", reloaded.Contributions.Single(value => value.Role == ContributionRole.Author).PersonName);
            Assert.Equal("9780441478125", reloaded.BookDetails?.Isbn13);
            Assert.Equal(MediaCollectionKind.Cycle, reloaded.Collection?.Kind);

            var record = await database.Connection.FindAsync<MediaItemRecord>(created.Id.ToString());
            Assert.Null(record?.Tags);
        }
        finally
        {
            if (database is not null)
            {
                await database.Connection.CloseAsync();
            }

            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task Startup_migration_moves_legacy_tags_to_join_table()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"pcs-migration-{Guid.NewGuid():N}.db3");
        LocalDatabaseService? firstDatabase = null;
        LocalDatabaseService? migratedDatabase = null;
        try
        {
            firstDatabase = new LocalDatabaseService(databasePath);
            await firstDatabase.InitializeAsync();
            var id = Guid.NewGuid();
            await firstDatabase.Connection.InsertAsync(new MediaItemRecord
            {
                Id = id.ToString(),
                UserId = "user-1",
                Title = "Legacy book",
                MediaType = (int)MediaType.Book,
                Status = (int)MediaStatus.Planned,
                Tags = "classic, winter; CLASSIC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await firstDatabase.Connection.CloseAsync();
            firstDatabase = null;

            migratedDatabase = new LocalDatabaseService(databasePath);
            var service = CreateService(migratedDatabase);
            var migrated = await service.GetMediaItemAsync(id, "user-1");

            Assert.NotNull(migrated);
            Assert.Equal(2, migrated!.TagNames.Count);
            Assert.Contains(migrated.TagNames, value => value.Equals("classic", StringComparison.OrdinalIgnoreCase));
            var raw = await migratedDatabase.Connection.FindAsync<MediaItemRecord>(id.ToString());
            Assert.Null(raw?.Tags);
        }
        finally
        {
            if (firstDatabase is not null)
            {
                await firstDatabase.Connection.CloseAsync();
            }

            if (migratedDatabase is not null)
            {
                await migratedDatabase.Connection.CloseAsync();
            }

            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task Update_book_replaces_graph_and_can_clear_rating()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"pcs-update-{Guid.NewGuid():N}.db3");
        LocalDatabaseService? database = null;
        try
        {
            database = new LocalDatabaseService(databasePath);
            var service = CreateService(database);
            var created = await service.CreateMediaItemAsync(new CreateMediaItemRequest
            {
                UserId = "user-1",
                Title = "Book",
                MediaType = MediaType.Book,
                Rating = 8.5m,
                TagNames = ["old"],
                Contributions = [new PersonCreditInput { Name = "First Author", Role = ContributionRole.Author }],
                BookDetails = new BookDetailsInput { PageCount = 100 },
                Collection = new CollectionMembershipInput { Name = "Old series" }
            });

            var updated = await service.UpdateMediaItemAsync(new UpdateMediaItemRequest
            {
                Id = created.Id,
                UserId = "user-1",
                Title = "Updated book",
                MediaType = MediaType.Book,
                Rating = null,
                TagNames = ["new"],
                Contributions =
                [
                    new PersonCreditInput { Name = "Second Author", Role = ContributionRole.Author, SortOrder = 0 },
                    new PersonCreditInput { Name = "First Author", Role = ContributionRole.Author, SortOrder = 1 }
                ],
                BookDetails = new BookDetailsInput { PageCount = 120, Isbn10 = "123456789X" },
                Collection = new CollectionMembershipInput { Name = "New cycle", Kind = MediaCollectionKind.Cycle, Position = 2 }
            });

            Assert.Null(updated.Rating);
            Assert.Equal(["new"], updated.TagNames);
            Assert.Equal(["Second Author", "First Author"], updated.Contributions
                .Where(value => value.Role == ContributionRole.Author)
                .OrderBy(value => value.SortOrder)
                .Select(value => value.PersonName));
            Assert.Equal(120, updated.BookDetails?.PageCount);
            Assert.Equal("New cycle", updated.Collection?.Name);
        }
        finally
        {
            if (database is not null)
            {
                await database.Connection.CloseAsync();
            }

            TryDelete(databasePath);
        }
    }

    [Theory]
    [InlineData("7.55")]
    [InlineData("10.1")]
    [InlineData("-0.1")]
    public async Task Rating_rejects_out_of_range_or_excess_precision(string value)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"pcs-rating-{Guid.NewGuid():N}.db3");
        LocalDatabaseService? database = null;
        try
        {
            database = new LocalDatabaseService(databasePath);
            var service = CreateService(database);

            var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateMediaItemAsync(new CreateMediaItemRequest
            {
                UserId = "user-1",
                Title = "Rating test",
                MediaType = MediaType.Book,
                Rating = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
            }));

            Assert.Contains(exception.Errors, error => error.Code == "Validation.RatingRange");
        }
        finally
        {
            if (database is not null)
            {
                await database.Connection.CloseAsync();
            }

            TryDelete(databasePath);
        }
    }

    private static MediaItemService CreateService(LocalDatabaseService database)
    {
        var people = new PersonService(new PersonRepository(database));
        var studios = new StudioService(new StudioRepository(database));
        return new MediaItemService(
            new MediaItemRepository(database),
            people,
            studios,
            new TagRepository(database),
            new MediaContributionRepository(database),
            new BookDetailsRepository(database),
            new MediaCollectionRepository(database),
            new MediaRelationRepository(database),
            new MediaCategoryRepository(database));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
