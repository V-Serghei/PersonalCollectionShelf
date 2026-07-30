namespace PersonalCollectionShelf.Infrastructure.Services.Firebase;

public interface IFirebaseSessionProvider
{
    Task<FirebaseSession?> GetSessionAsync(CancellationToken cancellationToken = default);
}

public sealed record FirebaseSession(string UserId, string IdToken, string? Email);
