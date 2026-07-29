using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PersonalCollectionShelf.Infrastructure.Services.Firebase;

public sealed class FirestoreRestClient(
    HttpClient httpClient,
    FirebaseOptions options,
    IFirebaseSessionProvider sessionProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool IsConfigured => options.IsConfigured;

    public async Task<IReadOnlyList<CloudDocument>> GetChangesAsync(
        DateTime? changedAfterUtc,
        CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var url = $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(options.ProjectId)}/databases/(default)/documents/users/{Uri.EscapeDataString(session.UserId)}:runQuery";

        object query = changedAfterUtc is null
            ? new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = "libraryData" } },
                    orderBy = new[] { new { field = new { fieldPath = "changedAt" }, direction = "ASCENDING" } }
                }
            }
            : new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = "libraryData" } },
                    where = new
                    {
                        fieldFilter = new
                        {
                            field = new { fieldPath = "changedAt" },
                            op = "GREATER_THAN",
                            value = new { timestampValue = ToTimestamp(changedAfterUtc.Value) }
                        }
                    },
                    orderBy = new[] { new { field = new { fieldPath = "changedAt" }, direction = "ASCENDING" } }
                }
            };

        using var request = CreateRequest(HttpMethod.Post, url, session.IdToken);
        request.Content = new StringContent(JsonSerializer.Serialize(query, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(json);
        var changes = new List<CloudDocument>();
        foreach (var result in document.RootElement.EnumerateArray())
        {
            if (!result.TryGetProperty("document", out var firestoreDocument) ||
                !firestoreDocument.TryGetProperty("fields", out var fields))
            {
                continue;
            }

            changes.Add(new CloudDocument(
                GetString(fields, "entityType"),
                GetString(fields, "entityKey"),
                GetNullableString(fields, "payload"),
                GetString(fields, "contentHash"),
                GetTimestamp(fields, "changedAt"),
                GetBoolean(fields, "isDeleted")));
        }

        return changes;
    }

    public async Task PutAsync(CloudDocument document, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var documentId = CloudKey.CreateDocumentId(document.EntityType, document.EntityKey);
        var url = $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(options.ProjectId)}/databases/(default)/documents/users/{Uri.EscapeDataString(session.UserId)}/libraryData/{documentId}";
        var fields = new Dictionary<string, object>
        {
            ["entityType"] = new { stringValue = document.EntityType },
            ["entityKey"] = new { stringValue = document.EntityKey },
            ["contentHash"] = new { stringValue = document.ContentHash },
            ["changedAt"] = new { timestampValue = ToTimestamp(document.ChangedAtUtc) },
            ["isDeleted"] = new { booleanValue = document.IsDeleted },
            ["schemaVersion"] = new { integerValue = "1" }
        };

        if (document.Payload is not null)
        {
            fields["payload"] = new { stringValue = document.Payload };
        }

        using var request = CreateRequest(HttpMethod.Patch, url, session.IdToken);
        request.Content = new StringContent(JsonSerializer.Serialize(new { fields }, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<FirebaseSession> RequireSessionAsync(CancellationToken cancellationToken)
    {
        return await sessionProvider.GetSessionAsync(cancellationToken)
            ?? throw new InvalidOperationException("A Firebase session is required for cloud synchronization.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string idToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
        return request;
    }

    private static string ToTimestamp(DateTime value) =>
        value.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static string GetString(JsonElement fields, string name) =>
        GetNullableString(fields, name) ?? throw new JsonException($"Missing Firestore field '{name}'.");

    private static string? GetNullableString(JsonElement fields, string name) =>
        fields.TryGetProperty(name, out var field) && field.TryGetProperty("stringValue", out var value)
            ? value.GetString()
            : null;

    private static bool GetBoolean(JsonElement fields, string name) =>
        fields.TryGetProperty(name, out var field) &&
        field.TryGetProperty("booleanValue", out var value) &&
        value.GetBoolean();

    private static DateTime GetTimestamp(JsonElement fields, string name)
    {
        var raw = fields.GetProperty(name).GetProperty("timestampValue").GetString();
        return DateTime.Parse(raw!, null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
    }
}
