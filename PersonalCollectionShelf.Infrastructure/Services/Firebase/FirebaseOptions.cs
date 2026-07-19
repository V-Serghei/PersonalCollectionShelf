using System.Text.Json;

namespace PersonalCollectionShelf.Infrastructure.Services.Firebase;

public sealed class FirebaseOptions
{
    public string ApiKey { get; init; } = string.Empty;

    public string ProjectId { get; init; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ProjectId);

    public static FirebaseOptions Disabled { get; } = new();

    public static FirebaseOptions Load(string appDataDirectory)
    {
        var configPath = Path.Combine(appDataDirectory, "firebase.json");

        if (!File.Exists(configPath))
        {
            return Disabled;
        }

        try
        {
            using var stream = File.OpenRead(configPath);
            var document = JsonSerializer.Deserialize<ConfigDocument>(stream, SerializerOptions);

            return document is null
                ? Disabled
                : new FirebaseOptions
                {
                    ApiKey = document.ApiKey ?? string.Empty,
                    ProjectId = document.ProjectId ?? string.Empty
                };
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return Disabled;
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class ConfigDocument
    {
        public string? ApiKey { get; set; }

        public string? ProjectId { get; set; }
    }
}
