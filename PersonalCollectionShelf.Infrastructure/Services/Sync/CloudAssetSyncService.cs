using System.Collections.Concurrent;
using System.Text.Json;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Infrastructure.Persistence;
using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.Infrastructure.Services.Sync;

public sealed class CloudAssetSyncService(
    LocalDatabaseService database,
    FirestoreRestClient firestore,
    ICloudAssetStore assetStore,
    CloudImageCompressor compressor)
{
    public const string EntityType = "cloudAsset";
    private const string LocalUserId = "local-user";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _cacheRoot = Path.Combine(
        Path.GetDirectoryName(database.DatabasePath) ?? AppContext.BaseDirectory,
        "cloud-cache");

    public async Task<int> UploadLocalChangesAsync(
        Action<int, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await assetStore.IsAvailableAsync(cancellationToken))
            {
                progress?.Invoke(0, 0);
                return 0;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Cloud image storage is unavailable: {exception}");
            progress?.Invoke(0, 0);
            return 1;
        }

        var candidates = await GetLocalCandidatesAsync(cancellationToken);
        var states = new ConcurrentDictionary<string, CloudAssetStateRecord>(
            (await database.Connection.Table<CloudAssetStateRecord>().ToListAsync())
                .ToDictionary(state => state.Id, StringComparer.Ordinal),
            StringComparer.Ordinal);
        var completed = 0;
        var failed = 0;
        var stopCloudRequests = 0;
        progress?.Invoke(completed, candidates.Count);
        await Parallel.ForEachAsync(
            candidates,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 3,
                CancellationToken = cancellationToken
            },
            async (candidate, token) =>
            {
                try
                {
                token.ThrowIfCancellationRequested();
                var localPath = ResolveLocalPath(candidate.Path);
                if (localPath is null || IsCloudCachePath(localPath))
                {
                    return;
                }

                states.TryGetValue(candidate.Id, out var state);
                var sourceFingerprint = GetSourceFingerprint(localPath);
                if (state?.SourceFingerprint == sourceFingerprint &&
                    !string.IsNullOrWhiteSpace(state.CloudObjectName))
                {
                    return;
                }

                CompressedCloudImage compressed;
                try
                {
                    compressed = await compressor.CompressAsync(localPath, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Interlocked.Increment(ref failed);
                    System.Diagnostics.Debug.WriteLine($"Cloud asset '{candidate.Id}' could not be compressed: {exception}");
                    return;
                }

                if (state?.SourceHash == compressed.SourceHash)
                {
                    if (!string.Equals(state.OriginalPath, candidate.Path, StringComparison.Ordinal))
                    {
                        state.OriginalPath = candidate.Path;
                        await database.Connection.UpdateAsync(state);
                    }
                    return;
                }

                // Once Drive or Firestore becomes unavailable, do not repeat a
                // multi-second retry sequence for every remaining image. The
                // data tables will still synchronize and assets retry next time.
                if (Volatile.Read(ref stopCloudRequests) != 0) return;

                var objectName = $"{compressed.SourceHash}.webp";
                try
                {
                    await assetStore.UploadAsync(objectName, compressed.Bytes, "image/webp", token);
                    var metadata = new CloudAssetMetadata(
                        candidate.OwnerType,
                        candidate.OwnerId,
                        candidate.Slot,
                        objectName,
                        compressed.CloudHash);
                    var payload = JsonSerializer.Serialize(metadata, JsonOptions);
                    var changedAt = DateTime.UtcNow;
                    await firestore.PutAsync(new CloudDocument(
                        EntityType,
                        candidate.Id,
                        payload,
                        CloudKey.Hash(payload),
                        changedAt,
                        false), token);

                    var updatedState = new CloudAssetStateRecord
                    {
                        Id = candidate.Id,
                        OwnerType = candidate.OwnerType,
                        OwnerId = candidate.OwnerId,
                        Slot = candidate.Slot,
                        OriginalPath = candidate.Path,
                        CachePath = state?.CachePath,
                        SourceHash = compressed.SourceHash,
                        SourceFingerprint = sourceFingerprint,
                        CloudObjectName = objectName,
                        CloudHash = compressed.CloudHash,
                        UpdatedAtUtc = changedAt
                    };
                    await database.Connection.InsertOrReplaceAsync(updatedState);
                    states[candidate.Id] = updatedState;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Interlocked.Increment(ref failed);
                    Interlocked.Exchange(ref stopCloudRequests, 1);
                    System.Diagnostics.Debug.WriteLine($"Cloud asset transfer stopped after '{candidate.Id}': {exception}");
                }
                }
                finally
                {
                    var current = Interlocked.Increment(ref completed);
                    progress?.Invoke(current, candidates.Count);
                }
            });

        return failed;
    }

    public async Task ApplyRemoteAsync(CloudDocument document, CancellationToken cancellationToken = default)
    {
        if (document.IsDeleted || document.Payload is null ||
            !await assetStore.IsAvailableAsync(cancellationToken))
        {
            return;
        }

        var metadata = JsonSerializer.Deserialize<CloudAssetMetadata>(document.Payload, JsonOptions)
            ?? throw new InvalidDataException("Invalid cloud asset metadata.");
        var state = await database.Connection.FindAsync<CloudAssetStateRecord>(document.EntityKey);
        if (state is not null && state.UpdatedAtUtc > document.ChangedAtUtc)
        {
            return;
        }

        var currentPath = await GetCurrentOwnerPathAsync(metadata, cancellationToken);
        var resolvedCurrent = ResolveLocalPath(currentPath);
        if (resolvedCurrent is not null && !IsCloudCachePath(resolvedCurrent))
        {
            await SaveRemoteStateAsync(document, metadata, state, currentPath, state?.CachePath);
            return;
        }

        var bytes = await assetStore.DownloadAsync(metadata.ObjectName, cancellationToken);
        Directory.CreateDirectory(_cacheRoot);
        var cachePath = Path.Combine(_cacheRoot, $"{metadata.CloudHash}.webp");
        if (!File.Exists(cachePath))
        {
            await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
        }

        await SetCurrentOwnerPathAsync(metadata, cachePath, cancellationToken);
        await SaveRemoteStateAsync(document, metadata, state, null, cachePath);
    }

    private async Task<IReadOnlyList<LocalAssetCandidate>> GetLocalCandidatesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = new List<LocalAssetCandidate>();
        var items = await database.Connection.Table<MediaItemRecord>()
            .Where(row => row.UserId == LocalUserId && row.DeletedAt == null)
            .ToListAsync();
        candidates.AddRange(items
            .Where(row => !string.IsNullOrWhiteSpace(row.CoverUrl))
            .Select(row => new LocalAssetCandidate($"media:{row.Id}:cover", "media", row.Id, "cover", row.CoverUrl!)));

        var people = await database.Connection.Table<PersonRecord>()
            .Where(row => row.UserId == LocalUserId && row.DeletedAt == null)
            .ToListAsync();
        candidates.AddRange(people
            .Where(row => !string.IsNullOrWhiteSpace(row.PhotoPath))
            .Select(row => new LocalAssetCandidate($"person:{row.Id}:primary", "person", row.Id, "primary", row.PhotoPath!)));

        var photos = await database.Connection.Table<PersonPhotoRecord>()
            .Where(row => row.UserId == LocalUserId)
            .ToListAsync();
        candidates.AddRange(photos
            .Where(row => !string.IsNullOrWhiteSpace(row.FilePath))
            .Select(row => new LocalAssetCandidate($"personPhoto:{row.Id}:image", "personPhoto", row.Id, "image", row.FilePath)));
        return candidates;
    }

    private async Task<string?> GetCurrentOwnerPathAsync(CloudAssetMetadata metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return metadata.OwnerType switch
        {
            "media" => (await database.Connection.FindAsync<MediaItemRecord>(metadata.OwnerId))?.CoverUrl,
            "person" => (await database.Connection.FindAsync<PersonRecord>(metadata.OwnerId))?.PhotoPath,
            "personPhoto" => (await database.Connection.FindAsync<PersonPhotoRecord>(metadata.OwnerId))?.FilePath,
            _ => null
        };
    }

    private async Task SetCurrentOwnerPathAsync(
        CloudAssetMetadata metadata,
        string cachePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (metadata.OwnerType)
        {
            case "media":
                var media = await database.Connection.FindAsync<MediaItemRecord>(metadata.OwnerId);
                if (media is not null)
                {
                    media.CoverUrl = new Uri(cachePath).AbsoluteUri;
                    await database.Connection.UpdateAsync(media);
                }
                break;
            case "person":
                var person = await database.Connection.FindAsync<PersonRecord>(metadata.OwnerId);
                if (person is not null)
                {
                    person.PhotoPath = cachePath;
                    await database.Connection.UpdateAsync(person);
                }
                break;
            case "personPhoto":
                var photo = await database.Connection.FindAsync<PersonPhotoRecord>(metadata.OwnerId);
                if (photo is not null)
                {
                    photo.FilePath = cachePath;
                    await database.Connection.UpdateAsync(photo);
                }
                break;
        }
    }

    private async Task SaveRemoteStateAsync(
        CloudDocument document,
        CloudAssetMetadata metadata,
        CloudAssetStateRecord? existing,
        string? originalPath,
        string? cachePath)
    {
        await database.Connection.InsertOrReplaceAsync(new CloudAssetStateRecord
        {
            Id = document.EntityKey,
            OwnerType = metadata.OwnerType,
            OwnerId = metadata.OwnerId,
            Slot = metadata.Slot,
            OriginalPath = originalPath ?? existing?.OriginalPath,
            CachePath = cachePath,
            SourceHash = existing?.SourceHash,
            SourceFingerprint = existing?.SourceFingerprint,
            CloudObjectName = metadata.ObjectName,
            CloudHash = metadata.CloudHash,
            UpdatedAtUtc = document.ChangedAtUtc
        });
    }

    private bool IsCloudCachePath(string path) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(_cacheRoot), StringComparison.OrdinalIgnoreCase);

    private static string? ResolveLocalPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            value = uri.LocalPath;
        }

        return File.Exists(value) ? Path.GetFullPath(value) : null;
    }

    private static string GetSourceFingerprint(string path)
    {
        var file = new FileInfo(path);
        return $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
    }

    private sealed record LocalAssetCandidate(string Id, string OwnerType, string OwnerId, string Slot, string Path);
    private sealed record CloudAssetMetadata(string OwnerType, string OwnerId, string Slot, string ObjectName, string CloudHash);
}
