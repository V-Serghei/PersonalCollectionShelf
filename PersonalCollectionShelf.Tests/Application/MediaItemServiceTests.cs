using PersonalCollectionShelf.Application.DTOs;
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
        var service = new MediaItemService(repository);

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
    public async Task CreateMediaItemAsync_rejects_missing_title()
    {
        var repository = new InMemoryMediaItemRepository();
        var service = new MediaItemService(repository);

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
        var service = new MediaItemService(repository);

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

    private sealed class InMemoryMediaItemRepository : IMediaItemRepository
    {
        private readonly List<MediaItem> _items = [];

        public Task<IReadOnlyList<MediaItem>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MediaItem>>(_items.Where(item => item.UserId == userId && !item.IsDeleted).ToList());
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
            CancellationToken cancellationToken = default)
        {
            var query = _items.Where(item => item.UserId == userId && !item.IsDeleted);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(item => item.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
            }

            if (mediaType.HasValue)
            {
                query = query.Where(item => item.MediaType == mediaType.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(item => item.Status == status.Value);
            }

            return Task.FromResult<IReadOnlyList<MediaItem>>(query.ToList());
        }
    }
}
