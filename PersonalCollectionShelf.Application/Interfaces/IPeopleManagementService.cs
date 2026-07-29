using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface IPeopleManagementService
{
    Task<IReadOnlyList<PersonCatalogDto>> GetCatalogAsync(string userId, CancellationToken cancellationToken = default);

    Task<PersonDetailsDto?> GetAsync(string userId, Guid id, CancellationToken cancellationToken = default);

    Task<PersonDetailsDto> SaveAsync(SavePersonRequest request, CancellationToken cancellationToken = default);

    Task<PersonRelationDto> SaveRelationAsync(SavePersonRelationRequest request, CancellationToken cancellationToken = default);

    Task RemoveRelationAsync(string userId, Guid relationId, CancellationToken cancellationToken = default);

    Task<PersonPhotoDto> AddPhotoAsync(AddPersonPhotoRequest request, CancellationToken cancellationToken = default);

    Task RemovePhotoAsync(string userId, Guid personId, Guid photoId, CancellationToken cancellationToken = default);

    Task SetPrimaryPhotoAsync(string userId, Guid personId, Guid photoId, CancellationToken cancellationToken = default);
}
