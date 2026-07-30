using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Infrastructure.Persistence;

namespace PersonalCollectionShelf.Infrastructure.Repositories;

public sealed class PersonPhotoRepository(LocalDatabaseService databaseService) : IPersonPhotoRepository
{
    public async Task<IReadOnlyList<PersonPhoto>> GetForPersonAsync(Guid personId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var personIdText = personId.ToString();
        var records = await databaseService.Connection.Table<PersonPhotoRecord>()
            .Where(value => value.UserId == userId && value.PersonId == personIdText)
            .ToListAsync();
        return records.OrderByDescending(value => value.IsPrimary).ThenBy(value => value.SortOrder).ThenBy(value => value.CreatedAt)
            .Select(ToDomain).ToList();
    }

    public async Task<PersonPhoto> AddAsync(PersonPhoto photo, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.InsertAsync(ToRecord(photo));
        return photo;
    }

    public async Task RemoveAsync(Guid photoId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var record = await databaseService.Connection.FindAsync<PersonPhotoRecord>(photoId.ToString());
        if (record is not null && record.UserId == userId)
        {
            await databaseService.Connection.DeleteAsync(record);
        }
    }

    public async Task SetPrimaryAsync(Guid personId, Guid photoId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var personIdText = personId.ToString();
        var records = await databaseService.Connection.Table<PersonPhotoRecord>()
            .Where(value => value.UserId == userId && value.PersonId == personIdText)
            .ToListAsync();
        foreach (var record in records)
        {
            var shouldBePrimary = record.Id == photoId.ToString();
            if (record.IsPrimary != shouldBePrimary)
            {
                record.IsPrimary = shouldBePrimary;
                await databaseService.Connection.UpdateAsync(record);
            }
        }
    }

    private static PersonPhoto ToDomain(PersonPhotoRecord value) => new()
    {
        Id = Guid.Parse(value.Id),
        UserId = value.UserId,
        PersonId = Guid.Parse(value.PersonId),
        FilePath = value.FilePath,
        Caption = value.Caption,
        IsPrimary = value.IsPrimary,
        SortOrder = value.SortOrder,
        CreatedAt = value.CreatedAt
    };

    private static PersonPhotoRecord ToRecord(PersonPhoto value) => new()
    {
        Id = value.Id.ToString(),
        UserId = value.UserId,
        PersonId = value.PersonId.ToString(),
        FilePath = value.FilePath,
        Caption = value.Caption,
        IsPrimary = value.IsPrimary,
        SortOrder = value.SortOrder,
        CreatedAt = value.CreatedAt
    };
}
