using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Services;
using PersonalCollectionShelf.Domain.Enums;
using PersonalCollectionShelf.Infrastructure.Persistence;
using PersonalCollectionShelf.Infrastructure.Repositories;

if (args.Length < 4)
{
    Console.Error.WriteLine("Usage: SeedTool <seed.json> <database.db3> <user-id> <cover-directory> [stored-cover-root]");
    return 2;
}

var seedPath = Path.GetFullPath(args[0]);
var databasePath = Path.GetFullPath(args[1]);
var userId = args[2];
var coverDirectory = Path.GetFullPath(args[3]);
var storedCoverRoot = args.Length >= 5 ? args[4].TrimEnd('/', '\\') : null;
Directory.CreateDirectory(coverDirectory);

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var seed = JsonSerializer.Deserialize<SeedDocument>(await File.ReadAllTextAsync(seedPath), options)
           ?? throw new InvalidDataException("Seed is invalid.");

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalCollectionShelf-SeedImporter/1.0");
var coverPaths = new ConcurrentDictionary<Guid, string?>();
using (var gate = new SemaphoreSlim(8))
{
    var completed = 0;
    await Task.WhenAll(seed.Items.Select(async item =>
    {
        await gate.WaitAsync();
        try
        {
            coverPaths[item.Id] = await DownloadCoverAsync(http, item, coverDirectory, storedCoverRoot);
        }
        finally
        {
            gate.Release();
            var current = Interlocked.Increment(ref completed);
            if (current % 100 == 0 || current == seed.Items.Count)
                Console.WriteLine($"covers {current}/{seed.Items.Count}");
        }
    }));
}

var database = new LocalDatabaseService(databasePath);
await database.InitializeAsync();
var people = new PersonService(new PersonRepository(database));
var studios = new StudioService(new StudioRepository(database));
var service = new MediaItemService(
    new MediaItemRepository(database),
    people,
    studios,
    new TagRepository(database),
    new MediaContributionRepository(database),
    new BookDetailsRepository(database),
    new MediaCollectionRepository(database),
    new MediaRelationRepository(database),
    new MediaCategoryRepository(database),
    new LocalDatabaseTransactionRunner(database),
    new MovieDetailsRepository(database),
    new MediaStudioCreditRepository(database),
    new MediaTypeDetailsRepository(database));

var existingItems = await service.GetLibraryAsync(userId);
var existingIds = existingItems.Select(value => value.Id).ToHashSet();
var existingKinopoiskIds = existingItems.Where(value => value.KinopoiskId.HasValue)
    .Select(value => value.KinopoiskId!.Value).ToHashSet();
var imported = 0;
var skipped = 0;
foreach (var item in seed.Items)
{
    if (existingIds.Contains(item.Id) || existingKinopoiskIds.Contains(item.KinopoiskId))
    {
        skipped++;
        continue;
    }

    var mediaType = Enum.TryParse<MediaType>(item.MediaType, true, out var parsedType)
        ? parsedType
        : item.SourceKind.Equals("series", StringComparison.OrdinalIgnoreCase) ? MediaType.Series : MediaType.Movie;
    var contributions = item.People.Select((value, index) => new PersonCreditInput
    {
        Name = value.Name,
        Role = Enum.TryParse<ContributionRole>(value.Role, true, out var role) ? role : ContributionRole.Actor,
        SortOrder = index,
        Details = value.Details
    }).ToList();
    var studioCredits = item.Studios.Select((value, index) => new StudioCreditInput
    {
        Name = value,
        Role = StudioRole.ProductionCompany,
        SortOrder = index
    }).ToList();
    var isEpisodic = mediaType is MediaType.Series or MediaType.AnimatedSeries or MediaType.Anime;
    var isMovie = mediaType is MediaType.Movie or MediaType.Cartoon;
    await service.CreateMediaItemAsync(new CreateMediaItemRequest
    {
        Id = item.Id,
        UserId = userId,
        Title = item.Title,
        OriginalTitle = item.OriginalTitle,
        Description = item.Description,
        MediaType = mediaType,
        Status = MediaStatus.Completed,
        Rating = item.Rating,
        FinishDate = ParseDate(item.FinishDate),
        ReleaseYear = item.ReleaseYear,
        CoverUrl = coverPaths.GetValueOrDefault(item.Id) ?? item.PosterUrl,
        TmdbId = item.TmdbId,
        ImdbId = item.ImdbId,
        KinopoiskId = item.KinopoiskId,
        TmdbRating = item.TmdbRating,
        TmdbVoteCount = item.TmdbVoteCount,
        ImdbRating = item.ImdbRating,
        ImdbVoteCount = item.ImdbVoteCount,
        KinopoiskRating = item.KinopoiskRating,
        KinopoiskVoteCount = item.KinopoiskVoteCount,
        ExternalRatingsUpdatedAt = seed.GeneratedAtUtc,
        Genres = item.Genres,
        Contributions = contributions,
        StudioCredits = studioCredits,
        MovieDetails = isMovie ? new MovieDetailsInput
        {
            RuntimeMinutes = item.RuntimeMinutes,
            OriginalLanguage = item.OriginalLanguage,
            CountryOfOrigin = item.Country,
            AgeRating = item.AgeRating
        } : null,
        EpisodicDetails = isEpisodic ? new EpisodicDetailsInput
        {
            SeasonCount = item.SeasonCount,
            EpisodeCount = item.EpisodeCount,
            EpisodeRuntimeMinutes = item.RuntimeMinutes,
            Network = item.Network,
            AiringStatus = item.AiringStatus,
            OriginalLanguage = item.OriginalLanguage
        } : null,
        Collection = string.IsNullOrWhiteSpace(item.CollectionName) ? null : new CollectionMembershipInput
        {
            Name = item.CollectionName,
            Kind = MediaCollectionKind.Series
        },
        Notes = $"Imported from Kinopoisk ratings (ID {item.KinopoiskId})."
    });
    imported++;
    if (imported % 50 == 0) Console.WriteLine($"imported {imported}/{seed.Items.Count}");
}

await database.Connection.CloseAsync();
Console.WriteLine($"completed imported={imported} skipped={skipped} total={seed.Items.Count}");
return 0;

static DateTime? ParseDate(string? value) =>
    DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
        ? parsed
        : null;

static async Task<string?> DownloadCoverAsync(
    HttpClient http,
    SeedItem item,
    string coverDirectory,
    string? storedCoverRoot)
{
    if (string.IsNullOrWhiteSpace(item.PosterUrl)) return null;
    var fileName = $"{item.Id:N}.jpg";
    var path = Path.Combine(coverDirectory, fileName);
    if (!File.Exists(path))
    {
        try
        {
            using var response = await http.GetAsync(item.PosterUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync();
            await using var target = File.Create(path);
            await source.CopyToAsync(target);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    return string.IsNullOrWhiteSpace(storedCoverRoot)
        ? new Uri(path).AbsoluteUri
        : new Uri($"{storedCoverRoot.Replace('\\', '/')}/{fileName}", UriKind.Absolute).AbsoluteUri;
}

internal sealed record SeedDocument
{
    public DateTime GeneratedAtUtc { get; init; }
    public IReadOnlyList<SeedItem> Items { get; init; } = [];
}

internal sealed record SeedItem
{
    public Guid Id { get; init; }
    public int KinopoiskId { get; init; }
    public string SourceKind { get; init; } = "film";
    public string Title { get; init; } = string.Empty;
    public string? OriginalTitle { get; init; }
    public string? Description { get; init; }
    public string? MediaType { get; init; }
    public decimal Rating { get; init; }
    public string? FinishDate { get; init; }
    public int? ReleaseYear { get; init; }
    public string? PosterUrl { get; init; }
    public int? TmdbId { get; init; }
    public string? ImdbId { get; init; }
    public decimal? TmdbRating { get; init; }
    public int? TmdbVoteCount { get; init; }
    public decimal? ImdbRating { get; init; }
    public int? ImdbVoteCount { get; init; }
    public decimal? KinopoiskRating { get; init; }
    public int? KinopoiskVoteCount { get; init; }
    public IReadOnlyList<string> Genres { get; init; } = [];
    public IReadOnlyList<SeedPerson> People { get; init; } = [];
    public IReadOnlyList<string> Studios { get; init; } = [];
    public int? RuntimeMinutes { get; init; }
    public string? OriginalLanguage { get; init; }
    public string? Country { get; init; }
    public string? AgeRating { get; init; }
    public int? SeasonCount { get; init; }
    public int? EpisodeCount { get; init; }
    public string? Network { get; init; }
    public string? AiringStatus { get; init; }
    public string? CollectionName { get; init; }
}

internal sealed record SeedPerson
{
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = "Actor";
    public string? Details { get; init; }
}
