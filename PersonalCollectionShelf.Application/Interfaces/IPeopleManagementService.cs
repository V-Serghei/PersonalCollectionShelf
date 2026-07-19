using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface IPeopleManagementService
{
    Task<PersonDetailsDto?> GetAsync(string userId, Guid id, CancellationToken cancellationToken = default);

    Task<PersonDetailsDto> SaveAsync(SavePersonRequest request, CancellationToken cancellationToken = default);

    Task<PersonRelationDto> SaveRelationAsync(SavePersonRelationRequest request, CancellationToken cancellationToken = default);

    Task RemoveRelationAsync(string userId, Guid relationId, CancellationToken cancellationToken = default);
}
