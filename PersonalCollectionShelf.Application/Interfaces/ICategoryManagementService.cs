using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface ICategoryManagementService
{
    Task<IReadOnlyList<MediaCategoryDetailsDto>> GetAllAsync(string userId, CancellationToken cancellationToken = default);

    Task<MediaCategoryDetailsDto> SaveAsync(SaveMediaCategoryRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string userId, Guid id, CancellationToken cancellationToken = default);
}
