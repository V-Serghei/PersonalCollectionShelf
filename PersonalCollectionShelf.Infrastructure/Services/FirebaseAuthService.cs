using PersonalCollectionShelf.Application.Interfaces;

namespace PersonalCollectionShelf.Infrastructure.Services;

public sealed class FirebaseAuthService : IAuthService
{
    public Task<string?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>("local-user");
    }

    public Task<bool> IsSignedInAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
