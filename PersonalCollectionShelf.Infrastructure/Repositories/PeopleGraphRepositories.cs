using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Domain.Enums;
using PersonalCollectionShelf.Infrastructure.Persistence;

namespace PersonalCollectionShelf.Infrastructure.Repositories;

public sealed class PersonRelationRepository(LocalDatabaseService databaseService) : IPersonRelationRepository
{
    public async Task<IReadOnlyList<PersonRelation>> GetForPersonAsync(Guid personId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var id = personId.ToString();
        var records = await databaseService.Connection.Table<PersonRelationRecord>()
            .Where(record => record.UserId == userId && (record.PersonId == id || record.RelatedPersonId == id))
            .ToListAsync();
        return records.Select(ToDomain).ToList();
    }

    public async Task<PersonRelation> UpsertAsync(PersonRelation relation, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        relation.UpdatedAt = DateTime.UtcNow;
        await databaseService.Connection.InsertOrReplaceAsync(ToRecord(relation));
        return relation;
    }

    public async Task RemoveAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var record = await databaseService.Connection.FindAsync<PersonRelationRecord>(id.ToString());
        if (record is not null && record.UserId == userId)
        {
            await databaseService.Connection.DeleteAsync(record);
        }
    }

    private static PersonRelation ToDomain(PersonRelationRecord value) => new()
    {
        Id = Guid.Parse(value.Id),
        UserId = value.UserId,
        PersonId = Guid.Parse(value.PersonId),
        RelatedPersonId = Guid.Parse(value.RelatedPersonId),
        Kind = (PersonRelationKind)value.Kind,
        InverseKind = value.InverseKind.HasValue ? (PersonRelationKind)value.InverseKind.Value : null,
        StartDate = value.StartDate,
        EndDate = value.EndDate,
        Notes = value.Notes,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };

    private static PersonRelationRecord ToRecord(PersonRelation value) => new()
    {
        Id = value.Id.ToString(),
        UserId = value.UserId,
        PersonId = value.PersonId.ToString(),
        RelatedPersonId = value.RelatedPersonId.ToString(),
        Kind = (int)value.Kind,
        InverseKind = value.InverseKind.HasValue ? (int)value.InverseKind.Value : null,
        StartDate = value.StartDate,
        EndDate = value.EndDate,
        Notes = value.Notes,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };
}

public sealed class ProfessionRepository(LocalDatabaseService databaseService) : IProfessionRepository
{
    public async Task<IReadOnlyList<Profession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var records = await databaseService.Connection.Table<ProfessionRecord>().ToListAsync();
        return records.OrderBy(record => record.DisplayName).Select(ToDomain).ToList();
    }

    public async Task<Profession> GetOrCreateAsync(string name, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A profession name is required.", nameof(name));
        }

        var key = Normalize(trimmed);
        var records = await databaseService.Connection.Table<ProfessionRecord>().ToListAsync();
        var existing = records.FirstOrDefault(record => Normalize(record.DisplayName) == key || record.Key == key);
        if (existing is not null)
        {
            return ToDomain(existing);
        }

        var created = new ProfessionRecord
        {
            Id = Guid.NewGuid().ToString(),
            Key = key,
            DisplayName = trimmed,
            IsSystem = false
        };
        await databaseService.Connection.InsertAsync(created);
        return ToDomain(created);
    }

    public async Task<IReadOnlyList<Profession>> GetForPersonAsync(Guid personId, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var id = personId.ToString();
        var links = await databaseService.Connection.Table<PersonProfessionRecord>()
            .Where(record => record.UserId == userId && record.PersonId == id)
            .ToListAsync();
        var ids = links.Select(link => link.ProfessionId).ToHashSet();
        return (await GetAllAsync(cancellationToken)).Where(profession => ids.Contains(profession.Id.ToString())).ToList();
    }

    public async Task SetForPersonAsync(Guid personId, string userId, IReadOnlyCollection<string> professionNames, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        var id = personId.ToString();
        var existing = await databaseService.Connection.Table<PersonProfessionRecord>()
            .Where(record => record.UserId == userId && record.PersonId == id)
            .ToListAsync();
        foreach (var link in existing)
        {
            await databaseService.Connection.DeleteAsync(link);
        }

        foreach (var name in professionNames.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var profession = await GetOrCreateAsync(name, cancellationToken);
            await databaseService.Connection.InsertAsync(new PersonProfessionRecord
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                PersonId = id,
                ProfessionId = profession.Id.ToString()
            });
        }
    }

    private static Profession ToDomain(ProfessionRecord value) => new()
    {
        Id = Guid.Parse(value.Id), Key = value.Key, DisplayName = value.DisplayName, IsSystem = value.IsSystem
    };

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
