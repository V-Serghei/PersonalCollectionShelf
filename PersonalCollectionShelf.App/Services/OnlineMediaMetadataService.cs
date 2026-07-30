using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Maui.Storage;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.Services;

public sealed class OnlineMediaMetadataService(HttpClient httpClient, MediaMetadataOptions options)
    : IMediaMetadataService
{
    private const string TmdbBaseUrl = "https://api.themoviedb.org/3";
    private const string TmdbImageBaseUrl = "https://image.tmdb.org/t/p/original";
    private const string ImdbRatingsUrl = "https://datasets.imdbws.com/title.ratings.tsv.gz";
    private const string KinopoiskBaseUrl = "https://kinopoiskapiunofficial.tech/api";
    private readonly string _cacheDirectory = Path.Combine(FileSystem.AppDataDirectory, "metadata-cache");

    public bool IsConfigured => options.IsTmdbConfigured;

    public async Task<IReadOnlyList<MediaMetadataCandidate>> SearchAsync(
        string query,
        MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("TMDB is not configured.");
        }

        query = query.Trim();
        if (query.Length < 2)
        {
            return [];
        }

        var isTv = mediaType is MediaType.Series or MediaType.AnimatedSeries or MediaType.Anime;
        var kind = isTv ? "tv" : "movie";
        using var search = await GetTmdbJsonAsync(
            $"{TmdbBaseUrl}/search/{kind}?query={Uri.EscapeDataString(query)}&language=ru-RU&include_adult=false&page=1",
            cancellationToken);
        var ids = search.RootElement.GetProperty("results").EnumerateArray()
            .Take(6)
            .Select(value => value.GetProperty("id").GetInt32())
            .ToArray();

        var candidates = await Task.WhenAll(ids.Select(id => BuildCandidateAsync(id, isTv, cancellationToken)));
        return candidates.Where(value => value is not null).Cast<MediaMetadataCandidate>().ToList();
    }

    public async Task<MediaMetadataCandidate> GetCandidateAsync(
        int tmdbId,
        MediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("TMDB is not configured.");
        }

        var isTv = mediaType is MediaType.Series or MediaType.AnimatedSeries or MediaType.Anime;
        return await BuildCandidateAsync(tmdbId, isTv, cancellationToken) ??
               throw new InvalidOperationException("TMDB title was not found.");
    }

    public async Task<MediaMetadataDetails> GetDetailsAsync(
        MediaMetadataCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        var kind = candidate.IsTv ? "tv" : "movie";
        var append = candidate.IsTv
            ? "credits,external_ids,content_ratings"
            : "credits,external_ids,release_dates";
        using var document = await GetTmdbJsonAsync(
            $"{TmdbBaseUrl}/{kind}/{candidate.TmdbId}?language=ru-RU&append_to_response={append}",
            cancellationToken);
        var root = document.RootElement;
        var imdbId = ReadString(root.GetProperty("external_ids"), "imdb_id");
        var imdb = await TryGetImdbRatingAsync(imdbId, cancellationToken);
        var kinopoisk = await TryGetKinopoiskRatingAsync(candidate, imdbId, cancellationToken);

        return new MediaMetadataDetails
        {
            Candidate = candidate,
            Description = ReadString(root, "overview"),
            ImdbId = imdbId,
            KinopoiskId = kinopoisk?.Id,
            ImdbRating = imdb?.Rating,
            ImdbVoteCount = imdb?.VoteCount,
            KinopoiskRating = kinopoisk?.Rating,
            KinopoiskVoteCount = kinopoisk?.VoteCount,
            RuntimeMinutes = ReadRuntime(root, candidate.IsTv),
            SeasonCount = ReadNullableInt(root, "number_of_seasons"),
            EpisodeCount = ReadNullableInt(root, "number_of_episodes"),
            OriginalLanguage = ReadString(root, "original_language"),
            Country = ReadCountry(root, candidate.IsTv),
            AgeRating = ReadAgeRating(root, candidate.IsTv),
            Network = ReadFirstName(root, "networks"),
            AiringStatus = ReadString(root, "status"),
            CollectionName = root.TryGetProperty("belongs_to_collection", out var collection) &&
                             collection.ValueKind == JsonValueKind.Object
                ? ReadString(collection, "name")
                : null,
            Genres = ReadNames(root, "genres"),
            People = ReadPeople(root),
            Studios = ReadNames(root, "production_companies")
                .Select(name => new MetadataStudio(name, StudioRole.ProductionCompany))
                .ToList(),
            RatingsUpdatedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<string?> DownloadPosterAsync(
        MediaMetadataCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(candidate.PosterUrl))
        {
            return null;
        }

        Directory.CreateDirectory(Path.Combine(FileSystem.AppDataDirectory, "covers"));
        var path = Path.Combine(FileSystem.AppDataDirectory, "covers", $"tmdb-{candidate.TmdbId}.jpg");
        if (File.Exists(path))
        {
            return path;
        }

        using var response = await httpClient.GetAsync(candidate.PosterUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(path);
        await source.CopyToAsync(target, cancellationToken);
        return path;
    }

    private async Task<MediaMetadataCandidate?> BuildCandidateAsync(
        int id,
        bool isTv,
        CancellationToken cancellationToken)
    {
        var kind = isTv ? "tv" : "movie";
        using var russian = await GetTmdbJsonAsync(
            $"{TmdbBaseUrl}/{kind}/{id}?language=ru-RU&append_to_response=credits",
            cancellationToken);
        using var english = await GetTmdbJsonAsync(
            $"{TmdbBaseUrl}/{kind}/{id}?language=en-US",
            cancellationToken);
        var root = russian.RootElement;
        var titleProperty = isTv ? "name" : "title";
        var originalProperty = isTv ? "original_name" : "original_title";
        var dateProperty = isTv ? "first_air_date" : "release_date";
        var posterPath = ReadString(root, "poster_path");
        var cast = root.GetProperty("credits").GetProperty("cast").EnumerateArray()
            .Take(3)
            .Select(value => ReadString(value, "name"))
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return new MediaMetadataCandidate(
            id,
            isTv,
            ReadString(root, titleProperty) ?? ReadString(root, originalProperty) ?? id.ToString(),
            ReadString(english.RootElement, titleProperty) ?? string.Empty,
            ReadString(root, originalProperty) ?? string.Empty,
            ReadYear(root, dateProperty),
            posterPath is null ? null : TmdbImageBaseUrl + posterPath,
            string.Join(", ", cast),
            ReadDecimal(root, "vote_average"),
            ReadNullableInt(root, "vote_count"));
    }

    private async Task<JsonDocument> GetTmdbJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.TmdbReadAccessToken.Trim());
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private async Task<RatingValue?> TryGetImdbRatingAsync(string? imdbId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imdbId))
        {
            return null;
        }

        Directory.CreateDirectory(_cacheDirectory);
        var path = Path.Combine(_cacheDirectory, "title.ratings.tsv.gz");
        if (!File.Exists(path) || DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromHours(24))
        {
            try
            {
                using var response = await httpClient.GetAsync(ImdbRatingsUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var target = File.Create(path);
                await source.CopyToAsync(target, cancellationToken);
            }
            catch when (File.Exists(path))
            {
                // A stale official snapshot is preferable to losing the last known rating offline.
            }
        }

        await using var file = File.OpenRead(path);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        _ = await reader.ReadLineAsync(cancellationToken);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var firstTab = line.IndexOf('\t');
            if (firstTab <= 0)
            {
                continue;
            }

            var id = line[..firstTab];
            var comparison = string.CompareOrdinal(id, imdbId);
            if (comparison > 0)
            {
                break;
            }

            if (comparison != 0)
            {
                continue;
            }

            var values = line.Split('\t');
            return values.Length >= 3 &&
                   decimal.TryParse(values[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var rating) &&
                   int.TryParse(values[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var votes)
                ? new RatingValue(rating, votes)
                : null;
        }

        return null;
    }

    private async Task<KinopoiskValue?> TryGetKinopoiskRatingAsync(
        MediaMetadataCandidate candidate,
        string? imdbId,
        CancellationToken cancellationToken)
    {
        if (!options.IsKinopoiskConfigured)
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{KinopoiskBaseUrl}/v2.1/films/search-by-keyword?keyword={Uri.EscapeDataString(candidate.Title)}&page=1");
        request.Headers.Add("X-API-KEY", options.KinopoiskApiKey.Trim());
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var search = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!search.RootElement.TryGetProperty("films", out var films))
        {
            return null;
        }

        var match = films.EnumerateArray()
            .Select(value => new
            {
                Id = ReadNullableInt(value, "filmId") ?? ReadNullableInt(value, "kinopoiskId"),
                Year = int.TryParse(ReadString(value, "year"), out var year) ? year : (int?)null,
                NameRu = ReadString(value, "nameRu"),
                NameEn = ReadString(value, "nameEn")
            })
            .Where(value => value.Id.HasValue)
            .OrderByDescending(value => value.Year == candidate.Year)
            .ThenByDescending(value => NameMatches(value.NameRu, candidate.Title) ||
                                       NameMatches(value.NameEn, candidate.EnglishTitle))
            .FirstOrDefault();
        if (match is null)
        {
            return null;
        }

        using var detailsRequest = new HttpRequestMessage(HttpMethod.Get, $"{KinopoiskBaseUrl}/v2.2/films/{match.Id}");
        detailsRequest.Headers.Add("X-API-KEY", options.KinopoiskApiKey.Trim());
        using var detailsResponse = await httpClient.SendAsync(detailsRequest, cancellationToken);
        if (!detailsResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var details = JsonDocument.Parse(await detailsResponse.Content.ReadAsStringAsync(cancellationToken));
        var returnedImdbId = ReadString(details.RootElement, "imdbId");
        if (!string.IsNullOrWhiteSpace(imdbId) && !string.IsNullOrWhiteSpace(returnedImdbId) &&
            !string.Equals(imdbId, returnedImdbId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new KinopoiskValue(
            match.Id!.Value,
            ReadDecimal(details.RootElement, "ratingKinopoisk"),
            ReadNullableInt(details.RootElement, "ratingKinopoiskVoteCount"));
    }

    private static IReadOnlyList<MetadataPerson> ReadPeople(JsonElement root)
    {
        var people = new List<MetadataPerson>();

        // TMDB models a TV show's creators separately from its crew. Most
        // series therefore have no useful "Director" row in /tv/{id}/credits,
        // even though created_by is populated. The application currently
        // presents the primary creative credit for screen productions as
        // "Director", so include these people in that slot.
        if (root.TryGetProperty("created_by", out var creators) &&
            creators.ValueKind == JsonValueKind.Array)
        {
            foreach (var creator in creators.EnumerateArray())
            {
                AddPerson(people, ReadString(creator, "name"), ContributionRole.Director);
            }
        }

        if (!root.TryGetProperty("credits", out var credits))
        {
            return people;
        }

        if (credits.TryGetProperty("crew", out var crew) && crew.ValueKind == JsonValueKind.Array)
        {
            foreach (var member in crew.EnumerateArray())
            {
                var role = ReadString(member, "job") switch
                {
                    "Director" => ContributionRole.Director,
                    "Screenplay" or "Writer" => ContributionRole.Screenwriter,
                    "Producer" or "Executive Producer" => ContributionRole.Producer,
                    "Director of Photography" => ContributionRole.Cinematographer,
                    "Original Music Composer" => ContributionRole.Composer,
                    "Casting" => ContributionRole.CastingDirector,
                    "Production Design" => ContributionRole.ProductionDesigner,
                    _ => (ContributionRole?)null
                };
                if (role.HasValue)
                {
                    AddPerson(people, ReadString(member, "name"), role.Value);
                }
            }
        }

        if (credits.TryGetProperty("cast", out var cast) && cast.ValueKind == JsonValueKind.Array)
        {
            foreach (var member in cast.EnumerateArray().Take(12))
            {
                var name = ReadString(member, "name");
                if (string.IsNullOrWhiteSpace(name) ||
                    people.Any(value => value.Role == ContributionRole.Actor &&
                                        string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                people.Add(new MetadataPerson(name, ContributionRole.Actor, ReadString(member, "character")));
            }
        }

        return people;
    }

    private static void AddPerson(
        ICollection<MetadataPerson> people,
        string? name,
        ContributionRole role)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            people.Any(value => value.Role == role &&
                                string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        people.Add(new MetadataPerson(name.Trim(), role));
    }

    private static IReadOnlyList<string> ReadNames(JsonElement root, string property) =>
        root.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(value => ReadString(value, "name"))
                .Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToList()
            : [];

    private static string? ReadFirstName(JsonElement root, string property) => ReadNames(root, property).FirstOrDefault();

    private static int? ReadRuntime(JsonElement root, bool isTv)
    {
        if (!isTv)
        {
            return ReadNullableInt(root, "runtime");
        }

        return root.TryGetProperty("episode_run_time", out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(value => value.GetInt32()).FirstOrDefault()
            : null;
    }

    private static string? ReadCountry(JsonElement root, bool isTv)
    {
        if (!isTv)
        {
            return ReadFirstName(root, "production_countries");
        }

        return root.TryGetProperty("origin_country", out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(value => value.GetString()).FirstOrDefault()
            : null;
    }

    private static string? ReadAgeRating(JsonElement root, bool isTv)
    {
        var property = isTv ? "content_ratings" : "release_dates";
        if (!root.TryGetProperty(property, out var container) ||
            !container.TryGetProperty("results", out var results))
        {
            return null;
        }

        foreach (var countryCode in new[] { "RU", "US" })
        {
            var country = results.EnumerateArray()
                .FirstOrDefault(value => ReadString(value, "iso_3166_1") == countryCode);
            if (country.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (isTv)
            {
                var rating = ReadString(country, "rating");
                if (!string.IsNullOrWhiteSpace(rating)) return rating;
            }
            else if (country.TryGetProperty("release_dates", out var dates))
            {
                var rating = dates.EnumerateArray().Select(value => ReadString(value, "certification"))
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                if (!string.IsNullOrWhiteSpace(rating)) return rating;
            }
        }

        return null;
    }

    private static int? ReadYear(JsonElement root, string property) =>
        DateTime.TryParse(ReadString(root, property), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.Year
            : null;

    private static string? ReadString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadNullableInt(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;

    private static decimal? ReadDecimal(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var result)
            ? result
            : null;

    private static bool NameMatches(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private sealed record RatingValue(decimal Rating, int VoteCount);
    private sealed record KinopoiskValue(int Id, decimal? Rating, int? VoteCount);
}
