using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Services;
using PersonalCollectionShelf.Application.Validation;
using PersonalCollectionShelf.Domain.Enums;
using PersonalCollectionShelf.Infrastructure.Persistence;
using PersonalCollectionShelf.Infrastructure.Repositories;

namespace PersonalCollectionShelf.Tests.Infrastructure;

public sealed class MoviePersistenceTests
{
    [Fact]
    public async Task Create_and_reload_movie_persists_people_studio_and_details()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"pcs-movie-{Guid.NewGuid():N}.db3");
        LocalDatabaseService? database = null;
        try
        {
            database = new LocalDatabaseService(databasePath);
            var service = CreateService(database);

            var created = await service.CreateMediaItemAsync(new CreateMediaItemRequest
            {
                UserId = "user-1",
                Title = "Arrival",
                MediaType = MediaType.Movie,
                Contributions =
                [
                    new PersonCreditInput { Name = "Denis Villeneuve", Role = ContributionRole.Director },
                    new PersonCreditInput { Name = "Eric Heisserer", Role = ContributionRole.Screenwriter },
                    new PersonCreditInput { Name = "Amy Adams", Role = ContributionRole.Actor, Details = "Louise Banks" },
                    new PersonCreditInput { Name = "Jeremy Renner", Role = ContributionRole.Actor, Details = "Ian Donnelly" }
                ],
                StudioCredits =
                [
                    new StudioCreditInput { Name = "Paramount Pictures", Role = StudioRole.Distributor },
                    new StudioCreditInput { Name = "FilmNation Entertainment", Role = StudioRole.ProductionCompany }
                ],
                MovieDetails = new MovieDetailsInput
                {
                    RuntimeMinutes = 116,
                    OriginalLanguage = "English",
                    Language = "Russian",
                    CountryOfOrigin = "United States",
                    AgeRating = "PG-13"
                }
            });

            Assert.Equal("Denis Villeneuve", created.Creator);
            Assert.Equal("FilmNation Entertainment", created.Publisher);
            Assert.Equal(["Amy Adams", "Jeremy Renner"], created.Cast);
            Assert.Equal(116, created.MovieDetails?.RuntimeMinutes);

            var reloaded = await service.GetMediaItemAsync(created.Id, "user-1");

            Assert.NotNull(reloaded);
            Assert.Equal(116, reloaded!.MovieDetails?.RuntimeMinutes);
            Assert.Equal("Russian", reloaded.MovieDetails?.Language);
            Assert.Contains(reloaded.Contributions, value =>
                value.Role == ContributionRole.Director &&
                value.PersonName == "Denis Villeneuve");
            Assert.Equal(2, reloaded.Contributions.Count(value => value.Role == ContributionRole.Actor));
            Assert.Contains(reloaded.Contributions, value =>
                value.Role == ContributionRole.Actor &&
                value.PersonName == "Amy Adams" &&
                value.Details == "Louise Banks");
            Assert.Contains(reloaded.StudioCredits, value =>
                value.Role == StudioRole.Distributor &&
                value.StudioName == "Paramount Pictures");
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
    public async Task Movie_runtime_outside_supported_range_is_rejected()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"pcs-movie-validation-{Guid.NewGuid():N}.db3");
        LocalDatabaseService? database = null;
        try
        {
            database = new LocalDatabaseService(databasePath);
            var service = CreateService(database);

            var exception = await Assert.ThrowsAsync<ValidationException>(() =>
                service.CreateMediaItemAsync(new CreateMediaItemRequest
                {
                    UserId = "user-1",
                    Title = "Invalid movie",
                    MediaType = MediaType.Movie,
                    MovieDetails = new MovieDetailsInput { RuntimeMinutes = 0 }
                }));

            Assert.Contains(exception.Errors, value => value.Code == "Validation.MovieRuntimeInvalid");
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
            new MediaCategoryRepository(database),
            new LocalDatabaseTransactionRunner(database),
            new MovieDetailsRepository(database),
            new MediaStudioCreditRepository(database));
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
