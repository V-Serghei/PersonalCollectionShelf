using PersonalCollectionShelf.Application.DTOs;

namespace PersonalCollectionShelf.Application.Interfaces;

public interface IAuthService
{
    Task<string?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);

    Task<bool> IsSignedInAsync(CancellationToken cancellationToken = default);

    Task<string?> GetSignedInEmailAsync(CancellationToken cancellationToken = default);

    Task<AuthResultDto> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AuthResultDto> SignUpAsync(string email, string password, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
