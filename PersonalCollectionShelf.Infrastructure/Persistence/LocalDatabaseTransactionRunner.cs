using PersonalCollectionShelf.Application.Interfaces;

namespace PersonalCollectionShelf.Infrastructure.Persistence;

public sealed class LocalDatabaseTransactionRunner(LocalDatabaseService databaseService) : ITransactionRunner
{
    private readonly SemaphoreSlim _transactionLock = new(1, 1);

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await databaseService.InitializeAsync(cancellationToken);
        await _transactionLock.WaitAsync(cancellationToken);
        try
        {
            await databaseService.Connection.ExecuteAsync("BEGIN IMMEDIATE TRANSACTION");
            try
            {
                var result = await operation(cancellationToken);
                await databaseService.Connection.ExecuteAsync("COMMIT");
                return result;
            }
            catch
            {
                try
                {
                    await databaseService.Connection.ExecuteAsync("ROLLBACK");
                }
                catch
                {
                    // Preserve the original failure. The next connection use will surface any rollback problem.
                }

                throw;
            }
        }
        finally
        {
            _transactionLock.Release();
        }
    }
}
