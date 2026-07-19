namespace PersonalCollectionShelf.Infrastructure.Services.Firebase;

public interface IFirebaseAuthClient
{
    Task<AuthTokens> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AuthTokens> SignUpAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}
