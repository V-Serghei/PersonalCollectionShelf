namespace PersonalCollectionShelf.Application.Interfaces;

public interface IAuthService
{
    Task<string?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);

    Task<bool> IsSignedInAsync(CancellationToken cancellationToken = default);
}
