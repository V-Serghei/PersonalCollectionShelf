using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;

namespace PersonalCollectionShelf.Application.Services;

public sealed class CollectionExplorerService(IMediaCollectionRepository collections, IMediaItemRepository mediaItems) : ICollectionExplorerService
{
    public async Task<IReadOnlyList<CollectionExplorerDto>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var itemLookup = (await mediaItems.GetAllAsync(userId, cancellationToken)).ToDictionary(item => item.Id);
        var entriesByCollection = (await collections.GetAllEntriesAsync(userId, cancellationToken))
            .GroupBy(entry => entry.CollectionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<MediaCollectionEntry>)group.ToList());
        var result = new List<CollectionExplorerDto>();
        foreach (var collection in await collections.GetAllAsync(userId, cancellationToken))
        {
            var entries = entriesByCollection.GetValueOrDefault(collection.Id) ?? [];
            result.Add(ToDto(collection, entries, itemLookup));
        }
        return result;
    }

    public async Task<CollectionExplorerDto?> GetByIdAsync(
        Guid id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var collection = await collections.GetByIdAsync(id, userId, cancellationToken);
        if (collection is null) return null;

        var entries = await collections.GetEntriesAsync(id, userId, cancellationToken);
        var itemLookup = (await mediaItems.GetAllAsync(userId, cancellationToken)).ToDictionary(item => item.Id);
        return ToDto(collection, entries, itemLookup);
    }

    private static CollectionExplorerDto ToDto(
        MediaCollection collection,
        IReadOnlyList<MediaCollectionEntry> entries,
        IReadOnlyDictionary<Guid, MediaItem> itemLookup)
    {
        var items = entries
            .Where(entry => itemLookup.ContainsKey(entry.MediaItemId))
            .Select(entry =>
            {
                var item = itemLookup[entry.MediaItemId];
                return new CollectionItemDto(
                    item.Id,
                    item.Title,
                    item.MediaType,
                    entry.Position,
                    item.CoverUrl,
                    item.Category,
                    item.ReleaseYear,
                    item.Rating,
                    item.Status);
            })
            .OrderBy(value => value.Position ?? double.MaxValue)
            .ThenBy(value => value.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CollectionExplorerDto(
            collection.Id,
            collection.Name,
            collection.Kind,
            items,
            collection.Description);
    }
}
