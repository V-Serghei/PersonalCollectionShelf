using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Services;
using PersonalCollectionShelf.Domain.Enums;
using PersonalCollectionShelf.Infrastructure.Persistence;
using PersonalCollectionShelf.Infrastructure.Repositories;

namespace PersonalCollectionShelf.Tests.Infrastructure;

public sealed class PeopleAndCategoryPersistenceTests
{
    [Fact]
    public async Task Person_photo_gallery_and_primary_photo_round_trip()
    {
        var path = TempPath("person-photos");
        var database = new LocalDatabaseService(path);
        try
        {
            var service = new PeopleManagementService(
                new PersonRepository(database),
                new ProfessionRepository(database),
                new PersonRelationRepository(database),
                new LocalDatabaseTransactionRunner(database),
                photos: new PersonPhotoRepository(database));
            var person = await service.SaveAsync(new SavePersonRequest { UserId = "user-1", Name = "Greta Gerwig" });
            var first = await service.AddPhotoAsync(new AddPersonPhotoRequest { UserId = "user-1", PersonId = person.Id, FilePath = "first.jpg" });
            var second = await service.AddPhotoAsync(new AddPersonPhotoRequest { UserId = "user-1", PersonId = person.Id, FilePath = "second.jpg" });

            await service.SetPrimaryPhotoAsync("user-1", person.Id, second.Id);
            var reloaded = await service.GetAsync("user-1", person.Id);

            Assert.Equal(2, reloaded!.Photos.Count);
            Assert.Equal("second.jpg", reloaded.PhotoPath);
            Assert.Equal(second.Id, reloaded.Photos.Single(photo => photo.IsPrimary).Id);
            Assert.NotEqual(first.Id, second.Id);
        }
        finally
        {
            await database.Connection.CloseAsync();
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Person_professions_and_bidirectional_relations_round_trip()
    {
        var path = TempPath("people");
        var database = new LocalDatabaseService(path);
        try
        {
            var service = new PeopleManagementService(
                new PersonRepository(database),
                new ProfessionRepository(database),
                new PersonRelationRepository(database),
                new LocalDatabaseTransactionRunner(database));

            var parent = await service.SaveAsync(new SavePersonRequest
            {
                UserId = "user-1",
                Name = "Octavia Butler",
                BirthYear = 1947,
                Professions = ["Writer", "Essayist"]
            });
            var child = await service.SaveAsync(new SavePersonRequest
            {
                UserId = "user-1",
                Name = "Related person"
            });

            await service.SaveRelationAsync(new SavePersonRelationRequest
            {
                UserId = "user-1",
                PersonId = parent.Id,
                RelatedPersonId = child.Id,
                Kind = PersonRelationKind.Parent,
                InverseKind = PersonRelationKind.Child
            });

            var reloadedParent = await service.GetAsync("user-1", parent.Id);
            var reloadedChild = await service.GetAsync("user-1", child.Id);

            Assert.Equal(["Essayist", "Writer"], reloadedParent!.Professions.Order());
            Assert.Equal(PersonRelationKind.Parent, Assert.Single(reloadedParent.Relations).Kind);
            Assert.Equal(PersonRelationKind.Child, Assert.Single(reloadedChild!.Relations).Kind);
        }
        finally
        {
            await database.Connection.CloseAsync();
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Custom_category_schema_round_trips_and_can_be_deleted()
    {
        var path = TempPath("category");
        var database = new LocalDatabaseService(path);
        try
        {
            var service = new CategoryManagementService(new MediaCategoryRepository(database));
            var saved = await service.SaveAsync(new SaveMediaCategoryRequest
            {
                UserId = "user-1",
                Name = "Board games",
                BaseMediaType = MediaType.Game,
                Fields =
                [
                    new CategoryFieldDefinitionDto { Key = "players", Label = "Players", FieldType = "number", IsRequired = true },
                    new CategoryFieldDefinitionDto { Key = "designer", Label = "Designer", FieldType = "text" }
                ]
            });

            var reloaded = (await service.GetAllAsync("user-1")).Single(value => value.Id == saved.Id);
            Assert.False(reloaded.IsSystem);
            Assert.Equal(MediaType.Game, reloaded.BaseMediaType);
            Assert.Equal(2, reloaded.Fields.Count);
            Assert.True(reloaded.Fields.Single(value => value.Key == "players").IsRequired);

            await service.DeleteAsync("user-1", saved.Id);
            Assert.DoesNotContain(await service.GetAllAsync("user-1"), value => value.Id == saved.Id);
        }
        finally
        {
            await database.Connection.CloseAsync();
            TryDelete(path);
        }
    }

    [Fact]
    public async Task Category_schema_rejects_duplicate_field_keys()
    {
        var path = TempPath("category-validation");
        var database = new LocalDatabaseService(path);
        try
        {
            var service = new CategoryManagementService(new MediaCategoryRepository(database));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new SaveMediaCategoryRequest
            {
                UserId = "user-1",
                Name = "Invalid",
                Fields =
                [
                    new CategoryFieldDefinitionDto { Key = "score", Label = "Score", FieldType = "number" },
                    new CategoryFieldDefinitionDto { Key = "SCORE", Label = "Other score", FieldType = "number" }
                ]
            }));
        }
        finally
        {
            await database.Connection.CloseAsync();
            TryDelete(path);
        }
    }

    private static string TempPath(string name) => Path.Combine(Path.GetTempPath(), $"pcs-{name}-{Guid.NewGuid():N}.db3");

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
    }
}
