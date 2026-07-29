using PersonalCollectionShelf.Infrastructure.Services;
using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.Tests.Infrastructure;

public sealed class FirebaseAuthServiceTests
{
    private static readonly FirebaseOptions ConfiguredOptions = new() { ApiKey = "key", ProjectId = "project" };

    [Fact]
    public async Task SignInAsync_fails_with_not_configured_key_when_firebase_is_disabled()
    {
        var service = new FirebaseAuthService(FirebaseOptions.Disabled, new StubAuthClient(), new InMemoryAuthTokenStore());

        var result = await service.SignInAsync("user@example.com", "secret");

        Assert.False(result.Succeeded);
        Assert.Equal("Auth.Error.NotConfigured", result.ErrorKey);
    }

    [Fact]
    public async Task SignInAsync_fails_when_email_or_password_is_missing()
    {
        var service = new FirebaseAuthService(ConfiguredOptions, new StubAuthClient(), new InMemoryAuthTokenStore());

        var result = await service.SignInAsync(" ", "secret");

        Assert.False(result.Succeeded);
        Assert.Equal("Auth.Error.MissingCredentials", result.ErrorKey);
    }

    [Fact]
    public async Task SignInAsync_saves_tokens_and_reports_signed_in_state()
    {
        var store = new InMemoryAuthTokenStore();
        var client = new StubAuthClient
        {
            SignInResult = MakeTokens("uid-1", "user@example.com", DateTime.UtcNow.AddHours(1))
        };
        var service = new FirebaseAuthService(ConfiguredOptions, client, store);

        var result = await service.SignInAsync("user@example.com", "secret");

        Assert.True(result.Succeeded);
        Assert.Equal("uid-1", result.UserId);
        Assert.True(await service.IsSignedInAsync());
        Assert.Equal("user@example.com", await service.GetSignedInEmailAsync());
    }

    [Fact]
    public async Task SignInAsync_maps_firebase_error_codes_to_localization_keys()
    {
        var client = new StubAuthClient { SignInError = new FirebaseAuthException("INVALID_LOGIN_CREDENTIALS") };
        var service = new FirebaseAuthService(ConfiguredOptions, client, new InMemoryAuthTokenStore());

        var result = await service.SignInAsync("user@example.com", "wrong");

        Assert.False(result.Succeeded);
        Assert.Equal("Auth.Error.InvalidCredentials", result.ErrorKey);
    }

    [Fact]
    public async Task GetCurrentUserIdAsync_keeps_local_user_id_even_when_signed_in()
    {
        var store = new InMemoryAuthTokenStore();
        await store.SaveAsync(MakeTokens("uid-1", "user@example.com", DateTime.UtcNow.AddHours(1)));
        var service = new FirebaseAuthService(ConfiguredOptions, new StubAuthClient(), store);

        Assert.Equal("local-user", await service.GetCurrentUserIdAsync());
    }

    [Fact]
    public async Task SignOutAsync_clears_stored_tokens()
    {
        var store = new InMemoryAuthTokenStore();
        await store.SaveAsync(MakeTokens("uid-1", "user@example.com", DateTime.UtcNow.AddHours(1)));
        var service = new FirebaseAuthService(ConfiguredOptions, new StubAuthClient(), store);

        await service.SignOutAsync();

        Assert.False(await service.IsSignedInAsync());
    }

    [Fact]
    public async Task GetValidIdTokenAsync_returns_current_token_when_not_expired()
    {
        var store = new InMemoryAuthTokenStore();
        await store.SaveAsync(MakeTokens("uid-1", "user@example.com", DateTime.UtcNow.AddHours(1)));
        var service = new FirebaseAuthService(ConfiguredOptions, new StubAuthClient(), store);

        Assert.Equal("id-token", await service.GetValidIdTokenAsync());
    }

    [Fact]
    public async Task GetValidIdTokenAsync_refreshes_expired_token_and_keeps_email()
    {
        var store = new InMemoryAuthTokenStore();
        await store.SaveAsync(MakeTokens("uid-1", "user@example.com", DateTime.UtcNow.AddMinutes(-5)));
        var client = new StubAuthClient
        {
            RefreshResult = new AuthTokens("uid-1", null, "refreshed-token", "new-refresh", DateTime.UtcNow.AddHours(1))
        };
        var service = new FirebaseAuthService(ConfiguredOptions, client, store);

        var idToken = await service.GetValidIdTokenAsync();

        Assert.Equal("refreshed-token", idToken);
        Assert.Equal("user@example.com", await service.GetSignedInEmailAsync());
    }

    [Fact]
    public async Task GetValidIdTokenAsync_clears_session_when_refresh_token_is_rejected()
    {
        var store = new InMemoryAuthTokenStore();
        await store.SaveAsync(MakeTokens("uid-1", "user@example.com", DateTime.UtcNow.AddMinutes(-5)));
        var client = new StubAuthClient { RefreshError = new FirebaseAuthException("TOKEN_EXPIRED") };
        var service = new FirebaseAuthService(ConfiguredOptions, client, store);

        var idToken = await service.GetValidIdTokenAsync();

        Assert.Null(idToken);
        Assert.False(await service.IsSignedInAsync());
    }

    private static AuthTokens MakeTokens(string userId, string? email, DateTime expiresAtUtc)
    {
        return new AuthTokens(userId, email, "id-token", "refresh-token", expiresAtUtc);
    }

    private sealed class StubAuthClient : IFirebaseAuthClient
    {
        public AuthTokens? SignInResult { get; init; }

        public FirebaseAuthException? SignInError { get; init; }

        public AuthTokens? RefreshResult { get; init; }

        public FirebaseAuthException? RefreshError { get; init; }

        public Task<AuthTokens> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (SignInError is not null)
            {
                throw SignInError;
            }

            return Task.FromResult(SignInResult ?? throw new InvalidOperationException("SignInResult is not set."));
        }

        public Task<AuthTokens> SignUpAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            return SignInAsync(email, password, cancellationToken);
        }

        public Task<AuthTokens> SignInWithGoogleAsync(
            string idToken,
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            return SignInAsync("google@example.com", accessToken, cancellationToken);
        }

        public Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (RefreshError is not null)
            {
                throw RefreshError;
            }

            return Task.FromResult(RefreshResult ?? throw new InvalidOperationException("RefreshResult is not set."));
        }
    }
}
