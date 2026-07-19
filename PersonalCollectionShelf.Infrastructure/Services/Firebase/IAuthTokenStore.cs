namespace PersonalCollectionShelf.Infrastructure.Services.Firebase;

public interface IAuthTokenStore
{
    Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
