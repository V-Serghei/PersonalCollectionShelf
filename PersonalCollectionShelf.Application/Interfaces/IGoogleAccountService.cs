using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface IGoogleAccountService
{
    bool IsConfigured { get; }

    Task<AuthResultDto> SignInAsync(CancellationToken cancellationToken = default);

    Task<string?> GetDriveAccessTokenAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
