using PersonalCollectionShelf.Infrastructure.Persistence;
using PersonalCollectionShelf.Infrastructure.Services.Firebase;
using PersonalCollectionShelf.Infrastructure.Services.Sync;

namespace PersonalCollectionShelf.Infrastructure.Services;

public sealed class FirestoreSyncService(
    LocalDatabaseService database,
    FirestoreRestClient firestoreClient,
    CloudAssetSyncService assetSyncService)
{
    private const string PullCursorKey = "firestore.pullCursorUtc";
    private const string LastSyncKey = "firestore.lastSyncUtc";
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public bool IsConfigured => firestoreClient.IsConfigured;

    public async Task<DateTime?> GetLastSyncedAtAsync(CancellationToken cancellationToken = default)
    {
        await database.InitializeAsync(cancellationToken);
        var value = await database.Connection.FindAsync<CloudSyncMetadataRecord>(LastSyncKey);
        return TryParseUtc(value?.Value);
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Firebase is not configured.");
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            await database.InitializeAsync(cancellationToken);
            await assetSyncService.UploadLocalChangesAsync(cancellationToken);
            var adapters = CloudTableRegistry.Create(database);
            var adapterMap = adapters.ToDictionary(adapter => adapter.EntityType, StringComparer.Ordinal);
            var states = (await database.Connection.Table<CloudEntityStateRecord>().ToListAsync())
                .ToDictionary(state => state.Id, StringComparer.Ordinal);

            var localEntities = await ScanLocalChangesAsync(adapters, states, cancellationToken);
            var cursorRecord = await database.Connection.FindAsync<CloudSyncMetadataRecord>(PullCursorKey);
            var cursor = TryParseUtc(cursorRecord?.Value);
            var remoteChanges = await firestoreClient.GetChangesAsync(cursor, cancellationToken);

            foreach (var remote in remoteChanges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (remote.EntityType == CloudAssetSyncService.EntityType)
                {
                    await assetSyncService.ApplyRemoteAsync(remote, cancellationToken);
                    continue;
                }

                if (!adapterMap.TryGetValue(remote.EntityType, out var adapter))
                {
                    continue;
                }

                var stateId = CloudKey.CreateStateId(remote.EntityType, remote.EntityKey);
                states.TryGetValue(stateId, out var localState);
                if (localState is not null && localState.ChangedAtUtc > remote.ChangedAtUtc)
                {
                    continue;
                }

                await adapter.ApplyAsync(remote, cancellationToken);
                var state = localState ?? new CloudEntityStateRecord
                {
                    Id = stateId,
                    EntityType = remote.EntityType,
                    EntityKey = remote.EntityKey
                };
                state.ContentHash = remote.ContentHash;
                state.CloudHash = remote.ContentHash;
                state.ChangedAtUtc = remote.ChangedAtUtc;
                state.IsDeleted = remote.IsDeleted;
                await database.Connection.InsertOrReplaceAsync(state);
                states[stateId] = state;
                localEntities.Remove(stateId);
            }

            foreach (var state in states.Values.Where(state => state.CloudHash != state.ContentHash))
            {
                cancellationToken.ThrowIfCancellationRequested();
                localEntities.TryGetValue(state.Id, out var local);
                var cloudDocument = new CloudDocument(
                    state.EntityType,
                    state.EntityKey,
                    state.IsDeleted ? null : local?.Payload,
                    state.ContentHash,
                    state.ChangedAtUtc,
                    state.IsDeleted);

                if (!state.IsDeleted && cloudDocument.Payload is null)
                {
                    continue;
                }

                await firestoreClient.PutAsync(cloudDocument, cancellationToken);
                state.CloudHash = state.ContentHash;
                await database.Connection.UpdateAsync(state);
            }

            var newestRemote = remoteChanges.Count == 0 ? cursor : remoteChanges.Max(change => change.ChangedAtUtc);
            if (newestRemote is not null)
            {
                await SetMetadataAsync(PullCursorKey, newestRemote.Value, cancellationToken);
            }

            await SetMetadataAsync(LastSyncKey, DateTime.UtcNow, cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<Dictionary<string, LocalCloudEntity>> ScanLocalChangesAsync(
        IReadOnlyList<ICloudTableAdapter> adapters,
        Dictionary<string, CloudEntityStateRecord> states,
        CancellationToken cancellationToken)
    {
        var entities = new Dictionary<string, LocalCloudEntity>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var adapter in adapters)
        {
            foreach (var entity in await adapter.ReadAsync(cancellationToken))
            {
                var stateId = CloudKey.CreateStateId(entity.EntityType, entity.EntityKey);
                seen.Add(stateId);
                entities[stateId] = entity;
                if (!states.TryGetValue(stateId, out var state))
                {
                    state = new CloudEntityStateRecord
                    {
                        Id = stateId,
                        EntityType = entity.EntityType,
                        EntityKey = entity.EntityKey,
                        ContentHash = entity.ContentHash,
                        ChangedAtUtc = entity.EntityUpdatedAtUtc,
                        IsDeleted = entity.IsDeleted
                    };
                    await database.Connection.InsertAsync(state);
                    states[stateId] = state;
                    continue;
                }

                if (state.ContentHash == entity.ContentHash && state.IsDeleted == entity.IsDeleted)
                {
                    continue;
                }

                state.ContentHash = entity.ContentHash;
                state.ChangedAtUtc = DateTime.UtcNow;
                state.IsDeleted = entity.IsDeleted;
                await database.Connection.UpdateAsync(state);
            }
        }

        foreach (var state in states.Values.Where(state => !seen.Contains(state.Id) && !state.IsDeleted))
        {
            state.IsDeleted = true;
            state.ContentHash = CloudKey.Hash($"deleted:{state.Id}:{DateTime.UtcNow:O}");
            state.ChangedAtUtc = DateTime.UtcNow;
            await database.Connection.UpdateAsync(state);
        }

        return entities;
    }

    private async Task SetMetadataAsync(string key, DateTime value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await database.Connection.InsertOrReplaceAsync(new CloudSyncMetadataRecord
        {
            Key = key,
            Value = value.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)
        });
    }

    private static DateTime? TryParseUtc(string? value) =>
        DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
}
