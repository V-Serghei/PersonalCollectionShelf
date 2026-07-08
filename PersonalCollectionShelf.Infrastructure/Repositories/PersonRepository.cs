using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Infrastructure.Persistence;

namespace PersonalCollectionShelf.Infrastructure.Repositories;

public sealed class PersonRepository(LocalDatabaseService databaseService) : IPersonRepository
{
    public async Task<Person?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);

        var idText = id.ToString();
        var record = await databaseService.Connection
            .Table<PersonRecord>()
            .FirstOrDefaultAsync(record => record.Id == idText && record.UserId == userId);

        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<Person>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, string userId, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await databaseService.InitializeAsync(cancellationToken);

        var idTexts = ids.Select(id => id.ToString()).ToHashSet();
        var records = await databaseService.Connection
            .Table<PersonRecord>()
            .Where(record => record.UserId == userId)
            .ToListAsync();

        return records.Where(record => idTexts.Contains(record.Id)).Select(ToDomain).ToList();
    }

    public async Task<Person?> GetByNameAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);

        var records = await databaseService.Connection
            .Table<PersonRecord>()
            .Where(record => record.UserId == userId)
            .ToListAsync();

        var match = records.FirstOrDefault(record => string.Equals(record.Name, name, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : ToDomain(match);
    }

    public async Task<Person> AddAsync(Person person, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.InsertAsync(ToRecord(person));
        return person;
    }

    private static Person ToDomain(PersonRecord record)
    {
        return new Person
        {
            Id = Guid.Parse(record.Id),
            UserId = record.UserId,
            Name = record.Name,
            CreatedAt = record.CreatedAt
        };
    }

    private static PersonRecord ToRecord(Person person)
    {
        return new PersonRecord
        {
            Id = person.Id.ToString(),
            UserId = person.UserId,
            Name = person.Name,
            CreatedAt = person.CreatedAt
        };
    }
}
