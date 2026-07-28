using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface IMediaItemService
{
    Task<IReadOnlyList<MediaItemDto>> GetLibraryAsync(string userId, CancellationToken cancellationToken = default);

    Task<int> GetLibraryItemCountAsync(string userId, CancellationToken cancellationToken = default);

    Task<MediaItemDto?> GetMediaItemAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<MediaItemDto> CreateMediaItemAsync(CreateMediaItemRequest request, CancellationToken cancellationToken = default);

    Task<MediaItemDto> UpdateMediaItemAsync(UpdateMediaItemRequest request, CancellationToken cancellationToken = default);

    Task DeleteMediaItemAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaItemDto>> SearchMediaItemsAsync(MediaItemSearchCriteria criteria, CancellationToken cancellationToken = default);
}
