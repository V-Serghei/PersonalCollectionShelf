using System.Text;
using System.Text.Json;

namespace PersonalCollectionShelf.Infrastructure.Services.Firebase;

public sealed class FirebaseAuthClient(HttpClient httpClient, FirebaseOptions options) : IFirebaseAuthClient
{
    private const string IdentityToolkitBaseUrl = "https://identitytoolkit.googleapis.com/v1/accounts";
    private const string SecureTokenUrl = "https://securetoken.googleapis.com/v1/token";

    public Task<AuthTokens> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        return SendCredentialsRequestAsync("signInWithPassword", email, password, cancellationToken);
    }

    public Task<AuthTokens> SignUpAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        return SendCredentialsRequestAsync("signUp", email, password, cancellationToken);
    }

    public async Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var requestUrl = $"{SecureTokenUrl}?key={Uri.EscapeDataString(options.ApiKey)}";
        using var response = await httpClient.PostAsync(requestUrl, content, cancellationToken);
        var payload = await ReadPayloadAsync(response, cancellationToken);

        return new AuthTokens(
            UserId: GetRequiredString(payload, "user_id"),
            Email: null,
            IdToken: GetRequiredString(payload, "id_token"),
            RefreshToken: GetRequiredString(payload, "refresh_token"),
            ExpiresAtUtc: DateTime.UtcNow.AddSeconds(ParseExpiresIn(payload, "expires_in")));
    }

    private async Task<AuthTokens> SendCredentialsRequestAsync(
        string action,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            email,
            password,
            returnSecureToken = true
        });

        var requestUrl = $"{IdentityToolkitBaseUrl}:{action}?key={Uri.EscapeDataString(options.ApiKey)}";
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(requestUrl, content, cancellationToken);
        var payload = await ReadPayloadAsync(response, cancellationToken);

        return new AuthTokens(
            UserId: GetRequiredString(payload, "localId"),
            Email: payload.TryGetProperty("email", out var emailProperty) ? emailProperty.GetString() : email,
            IdToken: GetRequiredString(payload, "idToken"),
            RefreshToken: GetRequiredString(payload, "refreshToken"),
            ExpiresAtUtc: DateTime.UtcNow.AddSeconds(ParseExpiresIn(payload, "expiresIn")));
    }

    private static async Task<JsonElement> ReadPayloadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new FirebaseAuthException("INVALID_RESPONSE");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new FirebaseAuthException(ExtractErrorCode(root));
        }

        return root;
    }

    private static string ExtractErrorCode(JsonElement root)
    {
        if (root.TryGetProperty("error", out var error) &&
            error.TryGetProperty("message", out var message) &&
            message.GetString() is { Length: > 0 } rawMessage)
        {
            // Firebase appends details after the code, e.g. "WEAK_PASSWORD : Password should be at least 6 characters".
            return rawMessage.Split(' ', ':')[0];
        }

        return "UNKNOWN";
    }

    private static string GetRequiredString(JsonElement payload, string propertyName)
    {
        if (payload.TryGetProperty(propertyName, out var property) && property.GetString() is { Length: > 0 } value)
        {
            return value;
        }

        throw new FirebaseAuthException("INVALID_RESPONSE");
    }

    private static double ParseExpiresIn(JsonElement payload, string propertyName)
    {
        if (payload.TryGetProperty(propertyName, out var property) &&
            double.TryParse(property.GetString(), out var seconds))
        {
            return seconds;
        }

        throw new FirebaseAuthException("INVALID_RESPONSE");
    }
}
