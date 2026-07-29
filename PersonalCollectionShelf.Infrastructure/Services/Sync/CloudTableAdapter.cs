using System.Text.Json;
using PersonalCollectionShelf.Infrastructure.Persistence;
using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.Infrastructure.Services.Sync;

internal sealed class CloudTableAdapter<T> : ICloudTableAdapter where T : class, new()
{
    private const string LocalUserId = "local-user";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly LocalDatabaseService _database;
    private readonly Func<T, string> _keySelector;
    private readonly Func<T, bool> _include;
    private readonly Func<T, DateTime> _updatedAt;
    private readonly Func<T, bool> _isDeleted;
    private readonly Action<T> _sanitizeForCloud;
    private readonly Action<T, T?> _prepareForLocal;

    public CloudTableAdapter(
        LocalDatabaseService database,
        string entityType,
        Func<T, string> keySelector,
        Func<T, bool>? include = null,
        Func<T, DateTime>? updatedAt = null,
        Func<T, bool>? isDeleted = null,
        Action<T>? sanitizeForCloud = null,
        Action<T, T?>? prepareForLocal = null)
    {
        _database = database;
        EntityType = entityType;
        _keySelector = keySelector;
        _include = include ?? (_ => true);
        _updatedAt = updatedAt ?? (_ => DateTime.UtcNow);
        _isDeleted = isDeleted ?? (_ => false);
        _sanitizeForCloud = sanitizeForCloud ?? (_ => { });
        _prepareForLocal = prepareForLocal ?? NormalizeUserId;
    }

    public string EntityType { get; }

    public async Task<IReadOnlyList<LocalCloudEntity>> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = await _database.Connection.Table<T>().ToListAsync();
        var result = new List<LocalCloudEntity>();

        foreach (var row in rows.Where(_include))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clone = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(row, JsonOptions), JsonOptions)!;
            _sanitizeForCloud(clone);
            NormalizeUserId(clone, null);
            var payload = JsonSerializer.Serialize(clone, JsonOptions);
            result.Add(new LocalCloudEntity(
                EntityType,
                _keySelector(row),
                payload,
                CloudKey.Hash(payload),
                EnsureUtc(_updatedAt(row)),
                _isDeleted(row)));
        }

        return result;
    }

    public async Task ApplyAsync(CloudDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = await _database.Connection.Table<T>().ToListAsync();
        var existing = rows.FirstOrDefault(row => _include(row) && _keySelector(row) == document.EntityKey);

        if (document.IsDeleted)
        {
            if (existing is not null)
            {
                await _database.Connection.DeleteAsync(existing);
            }

            return;
        }

        if (document.Payload is null)
        {
            throw new InvalidDataException($"Cloud entity '{EntityType}/{document.EntityKey}' has no payload.");
        }

        var incoming = JsonSerializer.Deserialize<T>(document.Payload, JsonOptions)
            ?? throw new InvalidDataException($"Cloud entity '{EntityType}/{document.EntityKey}' is invalid.");
        _prepareForLocal(incoming, existing);
        await _database.Connection.InsertOrReplaceAsync(incoming);
    }

    private static void NormalizeUserId(T row, T? existing)
    {
        var property = typeof(T).GetProperty("UserId");
        if (property?.CanWrite == true)
        {
            property.SetValue(row, LocalUserId);
        }
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value == default)
        {
            return DateTime.UtcNow;
        }

        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }
}
