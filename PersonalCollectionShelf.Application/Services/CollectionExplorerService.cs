using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Domain.Abstractions;

namespace PersonalCollectionShelf.Application.Services;

public sealed class CollectionExplorerService(IMediaCollectionRepository collections, IMediaItemRepository mediaItems) : ICollectionExplorerService
{
    public async Task<IReadOnlyList<CollectionExplorerDto>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var result = new List<CollectionExplorerDto>();
        foreach (var collection in await collections.GetAllAsync(userId, cancellationToken))
        {
            var items = new List<CollectionItemDto>();
            foreach (var entry in await collections.GetEntriesAsync(collection.Id, userId, cancellationToken))
            {
                var item = await mediaItems.GetByIdAsync(entry.MediaItemId, userId, cancellationToken);
                if (item is not null) items.Add(new CollectionItemDto(item.Id, item.Title, item.MediaType, entry.Position));
            }
            result.Add(new CollectionExplorerDto(collection.Id, collection.Name, collection.Kind, items.OrderBy(value => value.Position).ThenBy(value => value.Title).ToList()));
        }
        return result;
    }
}
