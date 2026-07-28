using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Application.Services;
using PersonalCollectionShelf.Application.Validation;
using PersonalCollectionShelf.Domain.Abstractions;
using PersonalCollectionShelf.Domain.Entities;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.Tests.Application;

public sealed class MediaItemServiceTests
{
    [Fact]
    public async Task CreateMediaItemAsync_creates_valid_item()
    {
        var repository = new InMemoryMediaItemRepository();
        var service = new MediaItemService(repository, new InMemoryPersonService(), new InMemoryStudioService());

        var created = await service.CreateMediaItemAsync(new CreateMediaItemRequest
        {
            UserId = "user-1",
            Title = "Dune",
            MediaType = MediaType.Book,
            Status = MediaStatus.InProgress,
            ProgressCurrent = 100,
            ProgressTotal = 500,
            Rating = 9
        });

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Dune", created.Title);
        Assert.Equal(MediaType.Book, created.MediaType);
        Assert.Single(await repository.GetAllAsync("user-1"));
    }

    [Fact]
    public async Task CreateMediaItemAsync_resolves_creator_studio_and_cast_to_entities()
    {
        var repository = new InMemoryMediaItemRepository();
        var personService = new InMemoryPersonService();
        var studioService = new InMemoryStudioService();
        var service = new MediaItemService(repository, personService, studioService);

        var created = await service.CreateMediaItemAsync(new CreateMediaItemRequest
        {
            UserId = "user-1",
            Title = "Dune: Part Two",
            MediaType = MediaType.Movie,
            Creator = "Denis Villeneuve",
            Publisher = "Legendary Pictures",
            Cast = ["Timothée Chalamet", "Zendaya", "timothée chalamet"]
        });

        Assert.NotNull(created.CreatorId);
        Assert.Equal("Denis Villeneuve", created.Creator);
        Assert.NotNull(created.StudioId);
        Assert.Equal("Legendary Pictures", created.Publisher);
        Assert.Equal(2, created.Cast.Count);
        Assert.Contains("Timothée Chalamet", created.Cast);
        Assert.Contains("Zendaya", created.Cast);

        var reloaded = await service.GetMediaItemAsync(created.Id, "user-1");
        Assert.NotNull(reloaded);
        Assert.Equal("Denis Villeneuve", reloaded!.Creator);
        Assert.Equal(2, reloaded.Cast.Count);

        var sameCreatorAgain = await service.CreateMediaItemAsync(new CreateMediaItemRequest
        {
            UserId = "user-1",
            Title = "Blade Runner 2049",
            MediaType = MediaType.Movie,
            Creator = "Denis Villeneuve"
        });

        Assert.Equal(created.CreatorId, sameCreatorAgain.CreatorId);
    }

    [Fact]
    public async Task CreateMediaItemAsync_rejects_missing_title()
    {
        var repository = new InMemoryMediaItemRepository();
        var service = new MediaItemService(repository, new InMemoryPersonService(), new InMemoryStudioService());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateMediaItemAsync(new CreateMediaItemRequest
            {
                UserId = "user-1",
                Title = " ",
                MediaType = MediaType.Book
            }));

        Assert.Contains(exception.Errors, error => error.Code == "Validation.TitleRequired");
    }

    [Fact]
    public async Task SearchMediaItemsAsync_filters_by_text_and_type()
    {
        var repository = new InMemoryMediaItemRepository();
        var service = new MediaItemService(repository, new InMemoryPersonService(), new InMemoryStudioService());

        await service.CreateMediaItemAsync(new CreateMediaItemRequest
        {
            UserId = "user-1",
            Title = "The Hobbit",
            MediaType = MediaType.Book
        });

        await service.CreateMediaItemAsync(new CreateMediaItemRequest
        {
            UserId = "user-1",
            Title = "The Last of Us",
            MediaType = MediaType.Game
        });

        var result = await service.SearchMediaItemsAsync(new MediaItemSearchCriteria
        {
            UserId = "user-1",
            SearchTerm = "the",
            MediaType = MediaType.Game
        });

        Assert.Single(result);
        Assert.Equal("The Last of Us", result[0].Title);
    }

    [Fact]
    public async Task SearchMediaItemsAsync_filters_by_category_and_tag()
    {
        var repository = new InMemoryMediaItemRepository();
        var service = new MediaItemService(repository, new InMemoryPersonService(), new InMemoryStudioService());

        await service.CreateMediaItemAsync(new CreateMediaItemRequest
        {
            UserId = "user-1",
            Title = "Dune",
            MediaType = MediaType.Book,
            Category = "Sci-Fi",
            Tags = "classic, desert, classic"
        });

        await service.CreateMediaItemAsync(new CreateMediaItemRequest
        {
            UserId = "user-1",
            Title = "Stardew Valley",
            MediaType = MediaType.Game,
            Category = "Games",
            Tags = "cozy, backlog"
        });

        var result = await service.SearchMediaItemsAsync(new MediaItemSearchCriteria
        {
            UserId = "user-1",
            Category = "sci-fi",
            Tag = "desert"
        });

        Assert.Single(result);
        Assert.Equal("Dune", result[0].Title);
        Assert.Equal("classic, desert", result[0].Tags);
    }

    [Fact]
    public async Task UpdateMediaItemAsync_updates_existing_item()
    {
        var repository = new InMemoryMediaItemRepository();
        var service = new MediaItemService(repository, new InMemoryPersonService(), new InMemoryStudioService());

        var created = await service.CreateMediaItemAsync(new CreateMediaItemRequest
        {
            UserId = "user-1",
            Title = "Old title",
            MediaType = MediaType.Book,
            Status = MediaStatus.Planned
        });

        var updated = await service.UpdateMediaItemAsync(new UpdateMediaItemRequest
        {
            Id = created.Id,
            UserId = "user-1",
            Title = "New title",
            MediaType = MediaType.Game,
            Status = MediaStatus.Completed,
            ProgressCurrent = 12,
            ProgressTotal = 12,
            Rating = 8,
            IsFavorite = true
        });

        Assert.Equal("New title", updated.Title);
        Assert.Equal(MediaType.Game, updated.MediaType);
        Assert.Equal(MediaStatus.Completed, updated.Status);
        Assert.Equal(8, updated.Rating);
        Assert.True(updated.IsFavorite);
    }

    [Fact]
    public async Task DeleteMediaItemAsync_hides_item_from_library()
    {
        var repository = new InMemoryMediaItemRepository();
        var service = new MediaItemService(repository, new InMemoryPersonService(), new InMemoryStudioService());

        var created = await service.CreateMediaItemAsync(new CreateMediaItemRequest
        {
            UserId = "user-1",
            Title = "Temporary",
            MediaType = MediaType.Other
        });

        await service.DeleteMediaItemAsync(created.Id, "user-1");

        Assert.Empty(await service.GetLibraryAsync("user-1"));
        Assert.Null(await service.GetMediaItemAsync(created.Id, "user-1"));
    }

    private sealed class InMemoryPersonService : IPersonService
    {
        private readonly List<PersonDto> _people = [];

        public Task<PersonDto?> GetByIdAsync(string userId, Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_people.FirstOrDefault(person => person.Id == id));
        }

        public Task<IReadOnlyList<PersonDto>> GetByIdsAsync(string userId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PersonDto>>(_people.Where(person => ids.Contains(person.Id)).ToList());
        }

        public Task<PersonDto> GetOrCreateAsync(string userId, string name, CancellationToken cancellationToken = default)
        {
            var existing = _people.FirstOrDefault(person => string.Equals(person.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                return Task.FromResult(existing);
            }

            var created = new PersonDto(Guid.NewGuid(), name);
            _people.Add(created);
            return Task.FromResult(created);
        }

        public Task<IReadOnlyList<PersonDto>> SearchAsync(string userId, string? searchTerm, int limit = 20, CancellationToken cancellationToken = default)
        {
            var results = _people
                .Where(person => string.IsNullOrWhiteSpace(searchTerm) || person.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<PersonDto>>(results);
        }

        public Task<PersonDto> CreateAsync(string userId, string displayName, CancellationToken cancellationToken = default)
        {
            return GetOrCreateAsync(userId, displayName, cancellationToken);
        }
    }

    private sealed class InMemoryStudioService : IStudioService
    {
        private readonly List<StudioDto> _studios = [];

        public Task<StudioDto?> GetByIdAsync(string userId, Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_studios.FirstOrDefault(studio => studio.Id == id));
        }

        public Task<StudioDto> GetOrCreateAsync(string userId, string name, CancellationToken cancellationToken = default)
        {
            var existing = _studios.FirstOrDefault(studio => string.Equals(studio.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                return Task.FromResult(existing);
            }

            var created = new StudioDto(Guid.NewGuid(), name);
            _studios.Add(created);
            return Task.FromResult(created);
        }
    }

    private sealed class InMemoryMediaItemRepository : IMediaItemRepository
    {
        private readonly List<MediaItem> _items = [];
        private readonly Dictionary<Guid, List<Guid>> _cast = [];

        public Task<IReadOnlyList<MediaItem>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MediaItem>>(_items.Where(item => item.UserId == userId && !item.IsDeleted).ToList());
        }

        public Task<int> CountAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Count(item => item.UserId == userId && !item.IsDeleted));
        }

        public Task<MediaItem?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.Id == id && item.UserId == userId && !item.IsDeleted));
        }

        public Task<MediaItem> AddAsync(MediaItem mediaItem, CancellationToken cancellationToken = default)
        {
            _items.Add(mediaItem);
            return Task.FromResult(mediaItem);
        }

        public Task<MediaItem> UpdateAsync(MediaItem mediaItem, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(mediaItem);
        }

        public Task DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
        {
            var item = _items.FirstOrDefault(candidate => candidate.Id == id && candidate.UserId == userId);
            item?.MarkDeleted();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MediaItem>> SearchAsync(
            string userId,
            string? searchTerm,
            MediaType? mediaType,
            MediaStatus? status,
            string? category,
            string? tag,
            CancellationToken cancellationToken = default)
        {
            var query = _items.Where(item => item.UserId == userId && !item.IsDeleted);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(item =>
                    Contains(item.Title, searchTerm) ||
                    Contains(item.OriginalTitle, searchTerm) ||
                    Contains(item.Description, searchTerm) ||
                    Contains(item.Category, searchTerm) ||
                    Contains(item.Tags, searchTerm) ||
                    Contains(item.Notes, searchTerm));
            }

            if (mediaType.HasValue)
            {
                query = query.Where(item => item.MediaType == mediaType.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(item => item.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(item => string.Equals(item.Category, category.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                query = query.Where(item => HasTag(item.Tags, tag));
            }

            return Task.FromResult<IReadOnlyList<MediaItem>>(query.ToList());
        }

        public Task<IReadOnlyList<Guid>> GetCastPersonIdsAsync(Guid mediaItemId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Guid>>(_cast.TryGetValue(mediaItemId, out var ids) ? ids.ToList() : []);
        }

        public Task ReplaceCastAsync(Guid mediaItemId, IReadOnlyList<Guid> personIds, CancellationToken cancellationToken = default)
        {
            _cast[mediaItemId] = personIds.ToList();
            return Task.CompletedTask;
        }

        private static bool Contains(string? source, string searchTerm)
        {
            return source?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool HasTag(string? tags, string tag)
        {
            if (string.IsNullOrWhiteSpace(tags))
            {
                return false;
            }

            return tags
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(candidate => string.Equals(candidate, tag.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
