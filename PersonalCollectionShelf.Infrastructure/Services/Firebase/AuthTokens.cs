namespace PersonalCollectionShelf.Infrastructure.Services.Firebase;

public sealed record AuthTokens(
    string UserId,
    string? Email,
    string IdToken,
    string RefreshToken,
    DateTime ExpiresAtUtc)
{
    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAtUtc;
}
