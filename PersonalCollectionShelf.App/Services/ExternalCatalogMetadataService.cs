using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Maui.Storage;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.Services;

public sealed class ExternalCatalogMetadataService(HttpClient httpClient, MediaMetadataOptions options)
    : IExternalCatalogMetadataService
{
    private const string GoogleBooksBase = "https://www.googleapis.com/books/v1";
    private const string OpenLibraryBase = "https://openlibrary.org";
    private const string ComicVineBase = "https://comicvine.gamespot.com/api";
    private const string RawgBase = "https://api.rawg.io/api";

    public bool Supports(MediaType mediaType) => mediaType is MediaType.Book or MediaType.Manga or MediaType.Comic or MediaType.Game;

    public bool IsConfigured(MediaType mediaType) => mediaType switch
    {
        MediaType.Book => true,
        MediaType.Manga => true,
        MediaType.Comic => options.IsComicVineConfigured || options.IsGoogleBooksConfigured,
        MediaType.Game => options.IsRawgConfigured,
        _ => false
    };

    public async Task<IReadOnlyList<ExternalCatalogCandidate>> SearchAsync(
        string query,
        MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (query.Length < 2 || !Supports(mediaType)) return [];

        var tasks = new List<Task<IReadOnlyList<ExternalCatalogCandidate>>>();
        if (mediaType is MediaType.Book or MediaType.Manga or MediaType.Comic)
        {
            tasks.Add(SearchOpenLibraryAsync(query, mediaType, cancellationToken));
            if (options.IsGoogleBooksConfigured) tasks.Add(SearchGoogleBooksAsync(query, mediaType, cancellationToken));
            if (mediaType is MediaType.Manga or MediaType.Comic && options.IsComicVineConfigured)
                tasks.Add(SearchComicVineAsync(query, mediaType, cancellationToken));
        }
        else if (mediaType == MediaType.Game && options.IsRawgConfigured)
        {
            tasks.Add(SearchRawgAsync(query, cancellationToken));
        }

        var results = (await Task.WhenAll(tasks)).SelectMany(value => value).ToList();
        return results
            .GroupBy(value => value.Isbn ?? $"{value.Provider}:{value.ExternalId}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(16)
            .ToList();
    }

    public async Task<ExternalCatalogDetails> GetDetailsAsync(
        ExternalCatalogCandidate candidate,
        CancellationToken cancellationToken = default) => candidate.Provider switch
    {
        ExternalCatalogProvider.GoogleBooks => await GetGoogleBooksDetailsAsync(candidate, cancellationToken),
        ExternalCatalogProvider.OpenLibrary => await GetOpenLibraryDetailsAsync(candidate, cancellationToken),
        ExternalCatalogProvider.ComicVine => await GetComicVineDetailsAsync(candidate, cancellationToken),
        ExternalCatalogProvider.Rawg => await GetRawgDetailsAsync(candidate, cancellationToken),
        _ => throw new NotSupportedException()
    };

    public async Task<ExternalCatalogCandidate> GetCandidateAsync(
        ExternalCatalogProvider provider,
        string externalId,
        MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        return provider switch
        {
            ExternalCatalogProvider.GoogleBooks => ParseGoogleCandidate(
                (await GetJsonAsync($"{GoogleBooksBase}/volumes/{Uri.EscapeDataString(externalId)}?key={Uri.EscapeDataString(options.GoogleBooksApiKey)}", null, cancellationToken)).RootElement,
                mediaType),
            ExternalCatalogProvider.OpenLibrary => await GetOpenLibraryCandidateAsync(externalId, mediaType, cancellationToken),
            ExternalCatalogProvider.ComicVine => await GetComicVineCandidateAsync(externalId, mediaType, cancellationToken),
            ExternalCatalogProvider.Rawg => await GetRawgCandidateAsync(externalId, cancellationToken),
            _ => throw new NotSupportedException()
        };
    }

    public async Task<string?> DownloadImageAsync(ExternalCatalogCandidate candidate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(candidate.ImageUrl)) return null;
        var directory = Path.Combine(FileSystem.AppDataDirectory, "covers");
        Directory.CreateDirectory(directory);
        var safeId = Regex.Replace(candidate.ExternalId, "[^A-Za-z0-9_-]", "-");
        var path = Path.Combine(directory, $"{candidate.Provider.ToString().ToLowerInvariant()}-{safeId}.jpg");
        if (File.Exists(path)) return path;
        using var request = new HttpRequestMessage(HttpMethod.Get, candidate.ImageUrl);
        AddUserAgent(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(path);
        await source.CopyToAsync(target, cancellationToken);
        return path;
    }

    private async Task<IReadOnlyList<ExternalCatalogCandidate>> SearchGoogleBooksAsync(string query, MediaType type, CancellationToken token)
    {
        using var json = await GetJsonAsync($"{GoogleBooksBase}/volumes?q={Uri.EscapeDataString(query)}&printType=books&maxResults=8&key={Uri.EscapeDataString(options.GoogleBooksApiKey)}", null, token);
        return json.RootElement.TryGetProperty("items", out var items)
            ? items.EnumerateArray().Select(value => ParseGoogleCandidate(value, type)).ToList()
            : [];
    }

    private static ExternalCatalogCandidate ParseGoogleCandidate(JsonElement item, MediaType type)
    {
        var info = item.GetProperty("volumeInfo");
        var id = ReadString(item, "id") ?? throw new InvalidDataException("Google Books ID is missing.");
        var identifiers = info.TryGetProperty("industryIdentifiers", out var ids) ? ids.EnumerateArray().ToList() : [];
        var isbn = identifiers.Select(value => ReadString(value, "identifier")).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var image = ReadBestImage(info);
        return new ExternalCatalogCandidate(
            ExternalCatalogProvider.GoogleBooks, id, type,
            ReadString(info, "title") ?? id,
            ReadString(info, "subtitle") ?? string.Empty,
            ReadYear(ReadString(info, "publishedDate")),
            image,
            JoinStrings(info, "authors"),
            string.Join(" · ", new[] { ReadString(info, "publisher"), isbn }.Where(value => !string.IsNullOrWhiteSpace(value))),
            isbn,
            ReadString(info, "canonicalVolumeLink") ?? ReadString(info, "infoLink"),
            ReadDecimal(info, "averageRating"),
            ReadInt(info, "ratingsCount"));
    }

    private async Task<ExternalCatalogDetails> GetGoogleBooksDetailsAsync(ExternalCatalogCandidate candidate, CancellationToken token)
    {
        using var json = await GetJsonAsync($"{GoogleBooksBase}/volumes/{Uri.EscapeDataString(candidate.ExternalId)}?key={Uri.EscapeDataString(options.GoogleBooksApiKey)}", null, token);
        var info = json.RootElement.GetProperty("volumeInfo");
        var ids = info.TryGetProperty("industryIdentifiers", out var values) ? values.EnumerateArray().ToList() : [];
        var isbn10 = ids.FirstOrDefault(value => ReadString(value, "type") == "ISBN_10");
        var isbn13 = ids.FirstOrDefault(value => ReadString(value, "type") == "ISBN_13");
        var secondary = await TryGetOpenLibraryRatingAsync(ReadString(isbn13, "identifier") ?? ReadString(isbn10, "identifier"), token);
        return new ExternalCatalogDetails
        {
            Candidate = candidate,
            Description = StripHtml(ReadString(info, "description")),
            Authors = ReadStringArray(info, "authors"),
            Publisher = ReadString(info, "publisher"),
            Isbn10 = ReadString(isbn10, "identifier"), Isbn13 = ReadString(isbn13, "identifier"),
            PageCount = ReadInt(info, "pageCount"), Language = ReadString(info, "language"),
            Genres = ReadStringArray(info, "categories"),
            PrimaryRatingSource = "Google Books", PrimaryRating = ReadDecimal(info, "averageRating"), PrimaryRatingCount = ReadInt(info, "ratingsCount"),
            SecondaryRatingSource = secondary?.Source, SecondaryRating = secondary?.Rating, SecondaryRatingCount = secondary?.Count
        };
    }

    private async Task<IReadOnlyList<ExternalCatalogCandidate>> SearchOpenLibraryAsync(string query, MediaType type, CancellationToken token)
    {
        var fields = "key,title,author_name,first_publish_year,cover_i,isbn,publisher,language,number_of_pages_median,ratings_average,ratings_count,subject";
        using var json = await GetJsonAsync($"{OpenLibraryBase}/search.json?q={Uri.EscapeDataString(query)}&limit=8&fields={Uri.EscapeDataString(fields)}", "openlibrary", token);
        return json.RootElement.GetProperty("docs").EnumerateArray().Select(value => ParseOpenLibraryCandidate(value, type)).ToList();
    }

    private static ExternalCatalogCandidate ParseOpenLibraryCandidate(JsonElement item, MediaType type)
    {
        var key = ReadString(item, "key") ?? throw new InvalidDataException("Open Library key is missing.");
        var cover = ReadInt(item, "cover_i");
        var isbn = ReadStringArray(item, "isbn").FirstOrDefault();
        return new ExternalCatalogCandidate(
            ExternalCatalogProvider.OpenLibrary, key, type,
            ReadString(item, "title") ?? key, string.Empty, ReadInt(item, "first_publish_year"),
            cover.HasValue ? $"https://covers.openlibrary.org/b/id/{cover.Value}-L.jpg" : null,
            JoinStrings(item, "author_name"),
            string.Join(" · ", new[] { ReadStringArray(item, "publisher").FirstOrDefault(), isbn }.Where(value => !string.IsNullOrWhiteSpace(value))),
            isbn, $"https://openlibrary.org{key}", ReadDecimal(item, "ratings_average"), ReadInt(item, "ratings_count"));
    }

    private async Task<ExternalCatalogCandidate> GetOpenLibraryCandidateAsync(string id, MediaType type, CancellationToken token)
    {
        using var json = await GetJsonAsync($"{OpenLibraryBase}/search.json?q={Uri.EscapeDataString("key:" + id)}&limit=1", "openlibrary", token);
        var item = json.RootElement.GetProperty("docs").EnumerateArray().FirstOrDefault();
        return item.ValueKind == JsonValueKind.Object ? ParseOpenLibraryCandidate(item, type) : throw new InvalidOperationException("Open Library item not found.");
    }

    private async Task<ExternalCatalogDetails> GetOpenLibraryDetailsAsync(ExternalCatalogCandidate candidate, CancellationToken token)
    {
        using var work = await GetJsonAsync($"{OpenLibraryBase}{candidate.ExternalId}.json", "openlibrary", token);
        var root = work.RootElement;
        var rating = await TryGetOpenLibraryRatingByWorkAsync(candidate.ExternalId, token);
        var google = options.IsGoogleBooksConfigured ? await TryGetGoogleRatingAsync(candidate.Isbn, token) : null;
        return new ExternalCatalogDetails
        {
            Candidate = candidate,
            Description = ReadDescription(root),
            Authors = candidate.CreatorSummary.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            Isbn13 = candidate.Isbn?.Length == 13 ? candidate.Isbn : null,
            Isbn10 = candidate.Isbn?.Length == 10 ? candidate.Isbn : null,
            Genres = ReadStringArray(root, "subjects"),
            PrimaryRatingSource = "Open Library", PrimaryRating = rating?.Rating, PrimaryRatingCount = rating?.Count,
            SecondaryRatingSource = google?.Source, SecondaryRating = google?.Rating, SecondaryRatingCount = google?.Count
        };
    }

    private async Task<IReadOnlyList<ExternalCatalogCandidate>> SearchComicVineAsync(string query, MediaType type, CancellationToken token)
    {
        var url = $"{ComicVineBase}/search/?api_key={Uri.EscapeDataString(options.ComicVineApiKey)}&format=json&resources=issue&limit=8&query={Uri.EscapeDataString(query)}&field_list=id,name,issue_number,cover_date,image,volume,api_detail_url,site_detail_url";
        using var json = await GetJsonAsync(url, "comicvine", token);
        return json.RootElement.GetProperty("results").EnumerateArray().Select(value => ParseComicVineCandidate(value, type)).ToList();
    }

    private static ExternalCatalogCandidate ParseComicVineCandidate(JsonElement item, MediaType type)
    {
        var id = ReadInt(item, "id")?.ToString(CultureInfo.InvariantCulture) ?? throw new InvalidDataException();
        var volume = item.TryGetProperty("volume", out var volumeObject) ? ReadString(volumeObject, "name") : null;
        var issue = ReadString(item, "issue_number");
        var image = item.TryGetProperty("image", out var imageObject) ? ReadString(imageObject, "original_url") : null;
        var name = ReadString(item, "name");
        var title = string.IsNullOrWhiteSpace(name) ? volume ?? id : name;
        return new ExternalCatalogCandidate(
            ExternalCatalogProvider.ComicVine, id, type, title!, volume ?? string.Empty,
            ReadYear(ReadString(item, "cover_date")), image, volume ?? string.Empty,
            string.IsNullOrWhiteSpace(issue) ? string.Empty : $"#{issue}", null,
            ReadString(item, "site_detail_url"), null, null);
    }

    private async Task<ExternalCatalogCandidate> GetComicVineCandidateAsync(string id, MediaType type, CancellationToken token)
    {
        var url = $"{ComicVineBase}/issue/4000-{Uri.EscapeDataString(id)}/?api_key={Uri.EscapeDataString(options.ComicVineApiKey)}&format=json";
        using var json = await GetJsonAsync(url, "comicvine", token);
        return ParseComicVineCandidate(json.RootElement.GetProperty("results"), type);
    }

    private async Task<ExternalCatalogDetails> GetComicVineDetailsAsync(ExternalCatalogCandidate candidate, CancellationToken token)
    {
        var url = $"{ComicVineBase}/issue/4000-{Uri.EscapeDataString(candidate.ExternalId)}/?api_key={Uri.EscapeDataString(options.ComicVineApiKey)}&format=json";
        using var json = await GetJsonAsync(url, "comicvine", token);
        var root = json.RootElement.GetProperty("results");
        var people = root.TryGetProperty("person_credits", out var credits)
            ? credits.EnumerateArray().Select(value => ReadString(value, "name")).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToList()
            : [];
        return new ExternalCatalogDetails
        {
            Candidate = candidate, Description = StripHtml(ReadString(root, "description") ?? ReadString(root, "deck")),
            Authors = people, Publisher = root.TryGetProperty("volume", out var volume) ? ReadString(volume, "name") : null,
            IssueCount = int.TryParse(ReadString(root, "issue_number"), out var issue) ? issue : null
        };
    }

    private async Task<IReadOnlyList<ExternalCatalogCandidate>> SearchRawgAsync(string query, CancellationToken token)
    {
        var url = $"{RawgBase}/games?key={Uri.EscapeDataString(options.RawgApiKey)}&search={Uri.EscapeDataString(query)}&search_precise=true&page_size=8";
        using var json = await GetJsonAsync(url, "rawg", token);
        return json.RootElement.GetProperty("results").EnumerateArray().Select(ParseRawgCandidate).ToList();
    }

    private static ExternalCatalogCandidate ParseRawgCandidate(JsonElement item)
    {
        var id = item.GetProperty("id").GetInt32().ToString(CultureInfo.InvariantCulture);
        var slug = ReadString(item, "slug");
        return new ExternalCatalogCandidate(
            ExternalCatalogProvider.Rawg, id, MediaType.Game, ReadString(item, "name") ?? id, string.Empty,
            ReadYear(ReadString(item, "released")), ReadString(item, "background_image"),
            JoinStrings(item, "developers", "name"), JoinRawgPlatforms(item), null,
            string.IsNullOrWhiteSpace(slug) ? "https://rawg.io/games" : $"https://rawg.io/games/{slug}",
            ReadDecimal(item, "rating"), ReadInt(item, "ratings_count"));
    }

    private async Task<ExternalCatalogCandidate> GetRawgCandidateAsync(string id, CancellationToken token)
    {
        using var json = await GetJsonAsync($"{RawgBase}/games/{Uri.EscapeDataString(id)}?key={Uri.EscapeDataString(options.RawgApiKey)}", "rawg", token);
        return ParseRawgCandidate(json.RootElement);
    }

    private async Task<ExternalCatalogDetails> GetRawgDetailsAsync(ExternalCatalogCandidate candidate, CancellationToken token)
    {
        using var json = await GetJsonAsync($"{RawgBase}/games/{Uri.EscapeDataString(candidate.ExternalId)}?key={Uri.EscapeDataString(options.RawgApiKey)}", "rawg", token);
        var root = json.RootElement;
        var publishers = ReadObjectNames(root, "publishers");
        return new ExternalCatalogDetails
        {
            Candidate = candidate,
            Description = ReadString(root, "description_raw") ?? StripHtml(ReadString(root, "description")),
            Developers = ReadObjectNames(root, "developers"),
            Publisher = publishers.FirstOrDefault(),
            Genres = ReadObjectNames(root, "genres"),
            Platform = JoinRawgPlatforms(root),
            PrimaryRatingSource = "RAWG",
            PrimaryRating = ReadDecimal(root, "rating"),
            PrimaryRatingCount = ReadInt(root, "ratings_count"),
            SecondaryRatingSource = ReadInt(root, "metacritic").HasValue ? "Metacritic" : null,
            SecondaryRating = ReadInt(root, "metacritic")
        };
    }

    private async Task<RatingResult?> TryGetOpenLibraryRatingAsync(string? isbn, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return null;
        using var search = await GetJsonAsync($"{OpenLibraryBase}/search.json?q=isbn:{Uri.EscapeDataString(isbn)}&limit=1&fields=key", "openlibrary", token);
        var work = search.RootElement.GetProperty("docs").EnumerateArray().FirstOrDefault();
        var key = work.ValueKind == JsonValueKind.Object ? ReadString(work, "key") : null;
        return key is null ? null : await TryGetOpenLibraryRatingByWorkAsync(key, token);
    }

    private async Task<RatingResult?> TryGetOpenLibraryRatingByWorkAsync(string workKey, CancellationToken token)
    {
        try
        {
            using var json = await GetJsonAsync($"{OpenLibraryBase}{workKey}/ratings.json", "openlibrary", token);
            var summary = json.RootElement.GetProperty("summary");
            return new RatingResult("Open Library", ReadDecimal(summary, "average"), ReadInt(summary, "count"));
        }
        catch (HttpRequestException) { return null; }
    }

    private async Task<RatingResult?> TryGetGoogleRatingAsync(string? isbn, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return null;
        using var json = await GetJsonAsync($"{GoogleBooksBase}/volumes?q=isbn:{Uri.EscapeDataString(isbn)}&maxResults=1&key={Uri.EscapeDataString(options.GoogleBooksApiKey)}", null, token);
        if (!json.RootElement.TryGetProperty("items", out var items)) return null;
        var info = items.EnumerateArray().First().GetProperty("volumeInfo");
        return new RatingResult("Google Books", ReadDecimal(info, "averageRating"), ReadInt(info, "ratingsCount"));
    }

    private async Task<JsonDocument> GetJsonAsync(string url, string? provider, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddUserAgent(request, provider);
        using var response = await httpClient.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
    }

    private void AddUserAgent(HttpRequestMessage request, string? provider = null)
    {
        var contact = string.IsNullOrWhiteSpace(options.OpenLibraryContactEmail) ? "personal-use" : options.OpenLibraryContactEmail.Trim();
        request.Headers.UserAgent.ParseAdd($"PersonalCollectionShelf/1.0 ({contact})");
    }

    private static string JoinRawgPlatforms(JsonElement root) =>
        root.TryGetProperty("platforms", out var values) && values.ValueKind == JsonValueKind.Array
            ? string.Join(", ", values.EnumerateArray()
                .Select(value => value.TryGetProperty("platform", out var platform) ? ReadString(platform, "name") : null)
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            : string.Empty;
    private static string JoinStrings(JsonElement root, string property) => string.Join(", ", ReadStringArray(root, property));
    private static string JoinStrings(JsonElement root, string property, string child) => string.Join(", ", ReadObjectNames(root, property, child));
    private static IReadOnlyList<string> ReadObjectNames(JsonElement root, string property, string child = "name") =>
        root.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(value => ReadString(value, child)).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToList()
            : [];
    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string property) =>
        root.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(value => value.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToList()
            : [];
    private static string? ReadBestImage(JsonElement info)
    {
        if (!info.TryGetProperty("imageLinks", out var links)) return null;
        foreach (var name in new[] { "extraLarge", "large", "medium", "small", "thumbnail" })
            if (ReadString(links, name) is { } value) return value.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase);
        return null;
    }
    private static string? ReadDescription(JsonElement root)
    {
        if (!root.TryGetProperty("description", out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ValueKind == JsonValueKind.Object ? ReadString(value, "value") : null;
    }
    private static string? ReadString(JsonElement root, string property) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int? ReadInt(JsonElement root, string property) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : null;
    private static decimal? ReadDecimal(JsonElement root, string property) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var result) ? result : null;
    private static int? ReadYear(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length >= 4 && int.TryParse(value[..4], out var year) ? year : null;
    private static string? StripHtml(string? value) => string.IsNullOrWhiteSpace(value) ? value : Regex.Replace(System.Net.WebUtility.HtmlDecode(value), "<[^>]+>", " ").Replace("  ", " ").Trim();
    private sealed record RatingResult(string Source, decimal? Rating, int? Count);
}
