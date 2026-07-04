using SQLite;

namespace PersonalCollectionShelf.Infrastructure.Persistence;

public sealed class LocalDatabaseService
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized;

    public LocalDatabaseService(string databasePath)
    {
        SQLitePCL.Batteries_V2.Init();

        Connection = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    public SQLiteAsyncConnection Connection { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            await Connection.CreateTableAsync<MediaItemRecord>();
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
