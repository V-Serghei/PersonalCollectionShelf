using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Domain.Enums;
using PersonalCollectionShelf.Infrastructure.Persistence;
using PersonalCollectionShelf.Infrastructure.Repositories;

namespace PersonalCollectionShelf.Tests.Infrastructure;

public sealed class MediaItemRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_loads_item_without_sqlite_tostring_function()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"pcs-{Guid.NewGuid():N}.db3");
        LocalDatabaseService? database = null;

        try
        {
            database = new LocalDatabaseService(databasePath);
            var repository = new MediaItemRepository(database);
            var item = new MediaItem
            {
                UserId = "user-1",
                Title = "Dune",
                MediaType = MediaType.Book,
                Status = MediaStatus.InProgress
            };

            await repository.AddAsync(item);

            var loaded = await repository.GetByIdAsync(item.Id, "user-1");

            Assert.NotNull(loaded);
            Assert.Equal(item.Id, loaded!.Id);
            Assert.Equal("Dune", loaded.Title);
        }
        finally
        {
            if (database is not null)
            {
                await database.Connection.CloseAsync();
            }

            if (File.Exists(databasePath))
            {
                TryDelete(databasePath);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // SQLite can keep the file handle alive briefly on Windows after CloseAsync.
        }
    }
}
