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
            .FirstOrDefaultAsync(record => record.Id == idText && record.UserId == userId && record.DeletedAt == null);

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
            .Where(record => record.UserId == userId && record.DeletedAt == null)
            .ToListAsync();

        return records.Where(record => idTexts.Contains(record.Id)).Select(ToDomain).ToList();
    }

    public async Task<Person?> GetByNameAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);

        var records = await databaseService.Connection
            .Table<PersonRecord>()
            .Where(record => record.UserId == userId && record.DeletedAt == null)
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

    public async Task<IReadOnlyList<Person>> SearchAsync(
        string userId,
        string? searchTerm,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);

        var records = await databaseService.Connection
            .Table<PersonRecord>()
            .Where(record => record.UserId == userId && record.DeletedAt == null)
            .ToListAsync();

        var term = searchTerm?.Trim();
        return records
            .Where(record => string.IsNullOrWhiteSpace(term) ||
                record.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                record.SortName?.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
                record.PenName?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(record => record.Name)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(ToDomain)
            .ToList();
    }

    private static Person ToDomain(PersonRecord record)
    {
        return new Person
        {
            Id = Guid.Parse(record.Id),
            UserId = record.UserId,
            Name = record.Name,
            FirstName = record.FirstName,
            MiddleName = record.MiddleName,
            LastName = record.LastName,
            PenName = record.PenName,
            SortName = record.SortName,
            BirthYear = record.BirthYear,
            DeathYear = record.DeathYear,
            BirthDate = record.BirthDate,
            DeathDate = record.DeathDate,
            PlaceOfBirth = record.PlaceOfBirth,
            Country = record.Country,
            Gender = record.Gender,
            OfficialWebsite = record.OfficialWebsite,
            PhotoPath = record.PhotoPath,
            Tagline = record.Tagline,
            Description = record.Description,
            Notes = record.Notes,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt == default ? record.CreatedAt : record.UpdatedAt,
            DeletedAt = record.DeletedAt
        };
    }

    private static PersonRecord ToRecord(Person person)
    {
        return new PersonRecord
        {
            Id = person.Id.ToString(),
            UserId = person.UserId,
            Name = person.Name,
            FirstName = person.FirstName,
            MiddleName = person.MiddleName,
            LastName = person.LastName,
            PenName = person.PenName,
            SortName = person.SortName,
            BirthYear = person.BirthYear,
            DeathYear = person.DeathYear,
            BirthDate = person.BirthDate,
            DeathDate = person.DeathDate,
            PlaceOfBirth = person.PlaceOfBirth,
            Country = person.Country,
            Gender = person.Gender,
            OfficialWebsite = person.OfficialWebsite,
            PhotoPath = person.PhotoPath,
            Tagline = person.Tagline,
            Description = person.Description,
            Notes = person.Notes,
            CreatedAt = person.CreatedAt,
            UpdatedAt = person.UpdatedAt,
            DeletedAt = person.DeletedAt
        };
    }
}
