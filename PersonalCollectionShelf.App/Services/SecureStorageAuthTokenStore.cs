using System.Text.Json;
using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.App.Services;

public sealed class SecureStorageAuthTokenStore : IAuthTokenStore
{
    private const string StorageKey = "firebase_auth_tokens";

    public async Task<AuthTokens?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync(StorageKey);

            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<AuthTokens>(json);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            SecureStorage.Default.Remove(StorageKey);
            return null;
        }
    }

    public Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(tokens);
        return SecureStorage.Default.SetAsync(StorageKey, json);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
