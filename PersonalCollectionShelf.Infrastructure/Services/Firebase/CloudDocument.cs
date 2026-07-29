namespace PersonalCollectionShelf.Infrastructure.Services.Firebase;

public sealed record CloudDocument(
    string EntityType,
    string EntityKey,
    string? Payload,
    string ContentHash,
    DateTime ChangedAtUtc,
    bool IsDeleted);
