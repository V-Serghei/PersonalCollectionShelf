using System.Text.Json;

namespace PersonalCollectionShelf.App.Services;

public sealed class MediaMetadataOptions
{
    public string TmdbReadAccessToken { get; init; } = string.Empty;
    public string KinopoiskApiKey { get; init; } = string.Empty;

    public bool IsTmdbConfigured => !string.IsNullOrWhiteSpace(TmdbReadAccessToken);
    public bool IsKinopoiskConfigured => !string.IsNullOrWhiteSpace(KinopoiskApiKey);

    public static MediaMetadataOptions Load(string appDataDirectory)
    {
        var path = Path.Combine(appDataDirectory, "metadata.json");
        if (!File.Exists(path))
        {
            return new MediaMetadataOptions();
        }

        try
        {
            return JsonSerializer.Deserialize<MediaMetadataOptions>(
                       File.ReadAllText(path),
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new MediaMetadataOptions();
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return new MediaMetadataOptions();
        }
    }
}
