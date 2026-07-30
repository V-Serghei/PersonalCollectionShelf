using System.Text.Json;

namespace PersonalCollectionShelf.App.Services;

public sealed class MediaMetadataOptions
{
    public string TmdbReadAccessToken { get; init; } = string.Empty;
    public string KinopoiskApiKey { get; init; } = string.Empty;
    public string GoogleBooksApiKey { get; init; } = string.Empty;
    public string OpenLibraryContactEmail { get; init; } = string.Empty;
    public string ComicVineApiKey { get; init; } = string.Empty;
    public string RawgApiKey { get; init; } = string.Empty;

    public bool IsTmdbConfigured => !string.IsNullOrWhiteSpace(TmdbReadAccessToken);
    public bool IsKinopoiskConfigured => !string.IsNullOrWhiteSpace(KinopoiskApiKey);
    public bool IsGoogleBooksConfigured => !string.IsNullOrWhiteSpace(GoogleBooksApiKey);
    public bool IsComicVineConfigured => !string.IsNullOrWhiteSpace(ComicVineApiKey);
    public bool IsRawgConfigured => !string.IsNullOrWhiteSpace(RawgApiKey);

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
