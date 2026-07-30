using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Infrastructure.Persistence;

namespace PersonalCollectionShelf.Infrastructure.Repositories;

public sealed class StudioRepository(LocalDatabaseService databaseService) : IStudioRepository
{
    public async Task<Studio?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);

        var idText = id.ToString();
        var record = await databaseService.Connection
            .Table<StudioRecord>()
            .FirstOrDefaultAsync(record => record.Id == idText && record.UserId == userId);

        return record is null ? null : ToDomain(record);
    }

    public async Task<Studio?> GetByNameAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);

        var records = await databaseService.Connection
            .Table<StudioRecord>()
            .Where(record => record.UserId == userId)
            .ToListAsync();

        var match = records.FirstOrDefault(record => string.Equals(record.Name, name, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : ToDomain(match);
    }

    public async Task<Studio> AddAsync(Studio studio, CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await databaseService.Connection.InsertAsync(ToRecord(studio));
        return studio;
    }

    private static Studio ToDomain(StudioRecord record)
    {
        return new Studio
        {
            Id = Guid.Parse(record.Id),
            UserId = record.UserId,
            Name = record.Name,
            CreatedAt = record.CreatedAt
        };
    }

    private static StudioRecord ToRecord(Studio studio)
    {
        return new StudioRecord
        {
            Id = studio.Id.ToString(),
            UserId = studio.UserId,
            Name = studio.Name,
            CreatedAt = studio.CreatedAt
        };
    }
}
