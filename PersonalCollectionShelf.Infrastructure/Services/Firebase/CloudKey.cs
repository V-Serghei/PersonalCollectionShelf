using System.Security.Cryptography;
using System.Text;

namespace PersonalCollectionShelf.Infrastructure.Services.Firebase;

internal static class CloudKey
{
    public static string CreateStateId(string entityType, string entityKey) => $"{entityType}:{entityKey}";

    public static string CreateDocumentId(string entityType, string entityKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(CreateStateId(entityType, entityKey)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Hash(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
