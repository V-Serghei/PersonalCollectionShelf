using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.Infrastructure.Services;

public sealed class FirebaseAuthService(
    FirebaseOptions options,
    IFirebaseAuthClient authClient,
    IAuthTokenStore tokenStore) : IAuthService
{
    // Local rows are owned by this id. The Firebase uid is mapped to it during sync,
    // not here, so signing in never hides the existing local library.
    private const string LocalUserId = "local-user";

    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(1);

    public Task<string?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(LocalUserId);
    }

    public async Task<bool> IsSignedInAsync(CancellationToken cancellationToken = default)
    {
        var tokens = await tokenStore.GetAsync(cancellationToken);
        return tokens is not null;
    }

    public async Task<string?> GetSignedInEmailAsync(CancellationToken cancellationToken = default)
    {
        var tokens = await tokenStore.GetAsync(cancellationToken);
        return tokens?.Email;
    }

    public Task<AuthResultDto> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        return ExecuteCredentialsFlowAsync(email, password, authClient.SignInAsync, cancellationToken);
    }

    public Task<AuthResultDto> SignUpAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        return ExecuteCredentialsFlowAsync(email, password, authClient.SignUpAsync, cancellationToken);
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        return tokenStore.ClearAsync(cancellationToken);
    }

    public async Task<string?> GetValidIdTokenAsync(CancellationToken cancellationToken = default)
    {
        var tokens = await tokenStore.GetAsync(cancellationToken);

        if (tokens is null)
        {
            return null;
        }

        if (!tokens.IsExpired(DateTime.UtcNow + ExpiryBuffer))
        {
            return tokens.IdToken;
        }

        try
        {
            var refreshed = await authClient.RefreshAsync(tokens.RefreshToken, cancellationToken);
            var merged = refreshed with { Email = refreshed.Email ?? tokens.Email };
            await tokenStore.SaveAsync(merged, cancellationToken);
            return merged.IdToken;
        }
        catch (FirebaseAuthException)
        {
            // The refresh token was rejected, so the stored session is no longer usable.
            await tokenStore.ClearAsync(cancellationToken);
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<AuthResultDto> ExecuteCredentialsFlowAsync(
        string email,
        string password,
        Func<string, string, CancellationToken, Task<AuthTokens>> sendRequest,
        CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
        {
            return AuthResultDto.Failure("Auth.Error.NotConfigured");
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return AuthResultDto.Failure("Auth.Error.MissingCredentials");
        }

        try
        {
            var tokens = await sendRequest(email.Trim(), password, cancellationToken);
            await tokenStore.SaveAsync(tokens, cancellationToken);
            return AuthResultDto.Success(tokens.UserId, tokens.Email);
        }
        catch (FirebaseAuthException exception)
        {
            return AuthResultDto.Failure(MapErrorKey(exception.ErrorCode));
        }
        catch (HttpRequestException)
        {
            return AuthResultDto.Failure("Auth.Error.Network");
        }
    }

    private static string MapErrorKey(string errorCode)
    {
        return errorCode switch
        {
            "EMAIL_NOT_FOUND" or "INVALID_PASSWORD" or "INVALID_LOGIN_CREDENTIALS" => "Auth.Error.InvalidCredentials",
            "EMAIL_EXISTS" => "Auth.Error.EmailExists",
            "INVALID_EMAIL" or "MISSING_EMAIL" => "Auth.Error.InvalidEmail",
            "WEAK_PASSWORD" or "MISSING_PASSWORD" => "Auth.Error.WeakPassword",
            "TOO_MANY_ATTEMPTS_TRY_LATER" => "Auth.Error.TooManyAttempts",
            "USER_DISABLED" => "Auth.Error.UserDisabled",
            _ => "Auth.Error.Unknown"
        };
    }
}
