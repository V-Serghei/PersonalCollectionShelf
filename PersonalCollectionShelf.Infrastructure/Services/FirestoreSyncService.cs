using PersonalCollectionShelf.Application.DTOs;
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
    private const string DirtyQueueInitializedKey = "firestore.dirtyQueueInitialized";
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public bool IsConfigured => firestoreClient.IsConfigured;

    public async Task<DateTime?> GetLastSyncedAtAsync(CancellationToken cancellationToken = default)
    {
        await database.InitializeAsync(cancellationToken);
        var value = await database.Connection.FindAsync<CloudSyncMetadataRecord>(LastSyncKey);
        return TryParseUtc(value?.Value);
    }

    public async Task<SyncResultDto> SyncAsync(
        IProgress<SyncProgressDto>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Firebase is not configured.");
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            Report(progress, 0, "Sync.Progress.Preparing");
            await database.InitializeAsync(cancellationToken);
            var queueMarker = await database.Connection.FindAsync<CloudSyncMetadataRecord>(DirtyQueueInitializedKey);
            var useDirtyQueue = string.Equals(queueMarker?.Value, "1", StringComparison.Ordinal);
            var dirtySnapshot = await database.Connection.Table<CloudDirtyEntityRecord>().ToListAsync();
            var failedAssetCount = await assetSyncService.UploadLocalChangesAsync(
                useDirtyQueue ? dirtySnapshot : null,
                (completed, total) =>
            {
                var percent = total == 0 ? 42 : 3 + (int)Math.Round(completed / (double)total * 39);
                Report(progress, percent, "Sync.Progress.Assets", completed, total);
            }, cancellationToken);
            var adapters = CloudTableRegistry.Create(database);
            var adapterMap = adapters.ToDictionary(adapter => adapter.EntityType, StringComparer.Ordinal);
            var states = (await database.Connection.Table<CloudEntityStateRecord>().ToListAsync())
                .ToDictionary(state => state.Id, StringComparer.Ordinal);

            var localEntities = useDirtyQueue
                ? await ScanDirtyLocalChangesAsync(adapters, states, dirtySnapshot, (completed, total) =>
                {
                    var percent = total == 0 ? 57 : 42 + (int)Math.Round(completed / (double)total * 15);
                    Report(progress, percent, "Sync.Progress.Scanning", completed, total);
                }, cancellationToken)
                : await ScanLocalChangesAsync(adapters, states, (completed, total) =>
                {
                    var percent = total == 0 ? 57 : 42 + (int)Math.Round(completed / (double)total * 15);
                    Report(progress, percent, "Sync.Progress.Scanning", completed, total);
                }, cancellationToken);
            var cursorRecord = await database.Connection.FindAsync<CloudSyncMetadataRecord>(PullCursorKey);
            var cursor = TryParseUtc(cursorRecord?.Value);
            Report(progress, 59, "Sync.Progress.Downloading");
            var remoteChanges = await firestoreClient.GetChangesAsync(cursor, cancellationToken);

            for (var remoteIndex = 0; remoteIndex < remoteChanges.Count; remoteIndex++)
            {
                var remote = remoteChanges[remoteIndex];
                cancellationToken.ThrowIfCancellationRequested();
                if (remote.EntityType == CloudAssetSyncService.EntityType)
                {
                    await assetSyncService.ApplyRemoteAsync(remote, cancellationToken);
                    ReportRemoteProgress(progress, remoteIndex + 1, remoteChanges.Count);
                    continue;
                }

                if (!adapterMap.TryGetValue(remote.EntityType, out var adapter))
                {
                    ReportRemoteProgress(progress, remoteIndex + 1, remoteChanges.Count);
                    continue;
                }

                var stateId = CloudKey.CreateStateId(remote.EntityType, remote.EntityKey);
                states.TryGetValue(stateId, out var localState);
                if (localState is not null && localState.ChangedAtUtc > remote.ChangedAtUtc)
                {
                    ReportRemoteProgress(progress, remoteIndex + 1, remoteChanges.Count);
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
                ReportRemoteProgress(progress, remoteIndex + 1, remoteChanges.Count);
            }

            var pendingUploads = new List<(CloudEntityStateRecord State, CloudDocument Document)>();
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

                pendingUploads.Add((state, cloudDocument));
            }

            var uploaded = 0;
            Report(progress, 75, "Sync.Progress.Uploading", uploaded, pendingUploads.Count);
            foreach (var batch in pendingUploads.Chunk(100))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await firestoreClient.PutBatchAsync(batch.Select(value => value.Document).ToList(), cancellationToken);
                foreach (var value in batch)
                {
                    value.State.CloudHash = value.State.ContentHash;
                    await database.Connection.UpdateAsync(value.State);
                }
                uploaded += batch.Length;
                var percent = pendingUploads.Count == 0
                    ? 95
                    : 75 + (int)Math.Round(uploaded / (double)pendingUploads.Count * 20);
                Report(progress, percent, "Sync.Progress.Uploading", uploaded, pendingUploads.Count);
            }

            Report(progress, 97, "Sync.Progress.Finalizing");
            var newestRemote = remoteChanges.Count == 0 ? cursor : remoteChanges.Max(change => change.ChangedAtUtc);
            if (newestRemote is not null)
            {
                await SetMetadataAsync(PullCursorKey, newestRemote.Value, cancellationToken);
            }

            await SetMetadataAsync(LastSyncKey, DateTime.UtcNow, cancellationToken);
            await SetMetadataValueAsync(DirtyQueueInitializedKey, "1", cancellationToken);
            await ClearProcessedDirtyRecordsAsync(dirtySnapshot, cancellationToken);
            Report(progress, 100, failedAssetCount > 0 ? "Sync.Progress.CompleteWithWarnings" : "Sync.Progress.Complete");
            return new SyncResultDto(failedAssetCount);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<Dictionary<string, LocalCloudEntity>> ScanLocalChangesAsync(
        IReadOnlyList<ICloudTableAdapter> adapters,
        Dictionary<string, CloudEntityStateRecord> states,
        Action<int, int>? progress,
        CancellationToken cancellationToken)
    {
        var entities = new Dictionary<string, LocalCloudEntity>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        progress?.Invoke(0, adapters.Count);
        for (var adapterIndex = 0; adapterIndex < adapters.Count; adapterIndex++)
        {
            var adapter = adapters[adapterIndex];
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
            progress?.Invoke(adapterIndex + 1, adapters.Count);
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

    private async Task<Dictionary<string, LocalCloudEntity>> ScanDirtyLocalChangesAsync(
        IReadOnlyList<ICloudTableAdapter> adapters,
        Dictionary<string, CloudEntityStateRecord> states,
        IReadOnlyList<CloudDirtyEntityRecord> dirtySnapshot,
        Action<int, int>? progress,
        CancellationToken cancellationToken)
    {
        var keysByType = dirtySnapshot
            .GroupBy(record => record.EntityType, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(record => record.EntityKey).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        // A previous interrupted upload may no longer have a dirty marker.
        // Include every state whose local hash is still not acknowledged.
        foreach (var pending in states.Values.Where(state => state.CloudHash != state.ContentHash))
        {
            if (!keysByType.TryGetValue(pending.EntityType, out var keys))
            {
                keys = new HashSet<string>(StringComparer.Ordinal);
                keysByType[pending.EntityType] = keys;
            }
            keys.Add(pending.EntityKey);
        }

        var activeAdapters = adapters
            .Where(adapter => keysByType.TryGetValue(adapter.EntityType, out var keys) && keys.Count > 0)
            .ToList();
        var entities = new Dictionary<string, LocalCloudEntity>(StringComparer.Ordinal);
        progress?.Invoke(0, activeAdapters.Count);

        for (var adapterIndex = 0; adapterIndex < activeAdapters.Count; adapterIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var adapter = activeAdapters[adapterIndex];
            var requestedKeys = keysByType[adapter.EntityType];
            var current = await adapter.ReadAsync(requestedKeys, cancellationToken);
            var currentKeys = current.Select(entity => entity.EntityKey).ToHashSet(StringComparer.Ordinal);

            foreach (var entity in current)
            {
                var stateId = CloudKey.CreateStateId(entity.EntityType, entity.EntityKey);
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

            foreach (var missingKey in requestedKeys.Where(key => !currentKeys.Contains(key)))
            {
                var stateId = CloudKey.CreateStateId(adapter.EntityType, missingKey);
                if (!states.TryGetValue(stateId, out var state) || state.IsDeleted) continue;
                state.IsDeleted = true;
                state.ContentHash = CloudKey.Hash($"deleted:{state.Id}:{DateTime.UtcNow:O}");
                state.ChangedAtUtc = DateTime.UtcNow;
                await database.Connection.UpdateAsync(state);
            }

            progress?.Invoke(adapterIndex + 1, activeAdapters.Count);
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

    private async Task SetMetadataValueAsync(
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await database.Connection.InsertOrReplaceAsync(new CloudSyncMetadataRecord
        {
            Key = key,
            Value = value
        });
    }

    private async Task ClearProcessedDirtyRecordsAsync(
        IReadOnlyList<CloudDirtyEntityRecord> snapshot,
        CancellationToken cancellationToken)
    {
        foreach (var record in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await database.Connection.ExecuteAsync(
                "DELETE FROM CloudDirtyEntities WHERE Id = ? AND Revision = ?",
                record.Id,
                record.Revision);
        }
    }

    private static DateTime? TryParseUtc(string? value) =>
        DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    private static void ReportRemoteProgress(IProgress<SyncProgressDto>? progress, int completed, int total)
    {
        var percent = total == 0 ? 74 : 60 + (int)Math.Round(completed / (double)total * 14);
        Report(progress, percent, "Sync.Progress.Applying", completed, total);
    }

    private static void Report(
        IProgress<SyncProgressDto>? progress,
        int percent,
        string messageKey,
        int completed = 0,
        int total = 0) =>
        progress?.Report(new SyncProgressDto(Math.Clamp(percent, 0, 100), messageKey, completed, total));
}
