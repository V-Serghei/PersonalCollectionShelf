namespace PersonalCollectionShelf.Infrastructure.Services.Firebase;

public sealed class FirebaseAuthException(string errorCode) : Exception($"Firebase auth request failed: {errorCode}")
{
    public string ErrorCode { get; } = errorCode;
}
