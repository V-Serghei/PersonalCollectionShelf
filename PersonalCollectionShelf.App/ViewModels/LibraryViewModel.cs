using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Pages;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class LibraryViewModel : BaseViewModel
{
    private readonly IMediaItemService _mediaItemService;
    private readonly IAuthService _authService;
    private bool _suppressFilterReload;

    public LibraryViewModel(
        IMediaItemService mediaItemService,
        IAuthService authService,
        ILocalizationService localizationService)
        : base(localizationService)
    {
        _mediaItemService = mediaItemService;
        _authService = authService;
        ReloadFilterOptions();
    }

    public ObservableCollection<MediaItemListItemViewModel> MediaItems { get; } = [];

    public ObservableCollection<LocalizedOption<MediaType?>> MediaTypeFilters { get; } = [];

    public ObservableCollection<LocalizedOption<MediaStatus?>> StatusFilters { get; } = [];

    public ObservableCollection<LocalizedOption<string?>> CategoryFilters { get; } = [];

    public ObservableCollection<LocalizedOption<string?>> TagFilters { get; } = [];

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private LocalizedOption<MediaType?>? selectedMediaTypeFilter;

    [ObservableProperty]
    private LocalizedOption<MediaStatus?>? selectedStatusFilter;

    [ObservableProperty]
    private LocalizedOption<string?>? selectedCategoryFilter;

    [ObservableProperty]
    private LocalizedOption<string?>? selectedTagFilter;

    [ObservableProperty]
    private LibraryQuickFilter activeQuickFilter;

    public string PageTitle => T("Library.Title");

    public string SearchPlaceholder => T("Library.SearchPlaceholder");

    public string MediaTypeFilterPlaceholder => T("Library.MediaTypeFilterPlaceholder");

    public string StatusFilterPlaceholder => T("Library.StatusFilterPlaceholder");

    public string CategoryFilterPlaceholder => T("Library.CategoryFilterPlaceholder");

    public string TagFilterPlaceholder => T("Library.TagFilterPlaceholder");

    public string AddButtonText => T("Library.AddButton");

    public string EmptyLibraryText => T("Library.Empty");

    public string QuickFilterAllText => GetQuickFilterText(LibraryQuickFilter.All, "Library.QuickFilter.All");

    public string QuickFilterFavoritesText => GetQuickFilterText(LibraryQuickFilter.Favorites, "Library.QuickFilter.Favorites");

    public string QuickFilterMissingCategoryText => GetQuickFilterText(LibraryQuickFilter.MissingCategory, "Library.QuickFilter.MissingCategory");

    public string QuickFilterMissingCoverText => GetQuickFilterText(LibraryQuickFilter.MissingCover, "Library.QuickFilter.MissingCover");

    public string QuickFilterCompletedText => GetQuickFilterText(LibraryQuickFilter.Completed, "Library.QuickFilter.Completed");

    public string QuickFilterInProgressText => GetQuickFilterText(LibraryQuickFilter.InProgress, "Library.QuickFilter.InProgress");

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        ReloadFilterOptions();
        _ = LoadAsync();
    }

    partial void OnSelectedMediaTypeFilterChanged(LocalizedOption<MediaType?>? value)
    {
        if (!_suppressFilterReload)
        {
            _ = LoadAsync();
        }
    }

    partial void OnSelectedStatusFilterChanged(LocalizedOption<MediaStatus?>? value)
    {
        if (!_suppressFilterReload)
        {
            _ = LoadAsync();
        }
    }

    partial void OnSelectedCategoryFilterChanged(LocalizedOption<string?>? value)
    {
        if (!_suppressFilterReload)
        {
            _ = LoadAsync();
        }
    }

    partial void OnSelectedTagFilterChanged(LocalizedOption<string?>? value)
    {
        if (!_suppressFilterReload)
        {
            _ = LoadAsync();
        }
    }

    partial void OnSearchTermChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _ = LoadAsync();
        }
    }

    partial void OnActiveQuickFilterChanged(LibraryQuickFilter value)
    {
        _ = LoadAsync();
        OnPropertyChanged(nameof(QuickFilterAllText));
        OnPropertyChanged(nameof(QuickFilterFavoritesText));
        OnPropertyChanged(nameof(QuickFilterMissingCategoryText));
        OnPropertyChanged(nameof(QuickFilterMissingCoverText));
        OnPropertyChanged(nameof(QuickFilterCompletedText));
        OnPropertyChanged(nameof(QuickFilterInProgressText));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var userId = await GetCurrentUserIdAsync();
            var library = await _mediaItemService.GetLibraryAsync(userId);
            ReloadDynamicFilterOptions(library);

            var items = await _mediaItemService.SearchMediaItemsAsync(new MediaItemSearchCriteria
            {
                UserId = userId,
                SearchTerm = SearchTerm,
                MediaType = SelectedMediaTypeFilter?.Value,
                Status = SelectedStatusFilter?.Value,
                Category = SelectedCategoryFilter?.Value,
                Tag = SelectedTagFilter?.Value
            });

            MediaItems.Clear();
            foreach (var item in ApplyQuickFilter(items))
            {
                MediaItems.Add(ToListItem(item));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateMediaItemAsync()
    {
        await Shell.Current.GoToAsync(nameof(EditMediaItemPage));
    }

    [RelayCommand]
    private async Task OpenMediaItemAsync(MediaItemListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"{nameof(MediaDetailsPage)}?id={item.Id}");
    }

    [RelayCommand]
    private void ShowAll()
    {
        ActiveQuickFilter = LibraryQuickFilter.All;
    }

    [RelayCommand]
    private void ShowFavorites()
    {
        ActiveQuickFilter = LibraryQuickFilter.Favorites;
    }

    [RelayCommand]
    private void ShowMissingCategory()
    {
        ActiveQuickFilter = LibraryQuickFilter.MissingCategory;
    }

    [RelayCommand]
    private void ShowMissingCover()
    {
        ActiveQuickFilter = LibraryQuickFilter.MissingCover;
    }

    [RelayCommand]
    private void ShowCompleted()
    {
        ActiveQuickFilter = LibraryQuickFilter.Completed;
    }

    [RelayCommand]
    private void ShowInProgress()
    {
        ActiveQuickFilter = LibraryQuickFilter.InProgress;
    }

    private void ReloadFilterOptions()
    {
        var selectedMediaType = SelectedMediaTypeFilter?.Value;
        var selectedStatus = SelectedStatusFilter?.Value;

        _suppressFilterReload = true;
        try
        {
            MediaTypeFilters.Clear();
            MediaTypeFilters.Add(new LocalizedOption<MediaType?>(null, T("Common.All")));
            foreach (var mediaType in Enum.GetValues<MediaType>())
            {
                MediaTypeFilters.Add(new LocalizedOption<MediaType?>(mediaType, T($"MediaType.{mediaType}")));
            }

            StatusFilters.Clear();
            StatusFilters.Add(new LocalizedOption<MediaStatus?>(null, T("Common.All")));
            foreach (var status in Enum.GetValues<MediaStatus>())
            {
                StatusFilters.Add(new LocalizedOption<MediaStatus?>(status, T($"MediaStatus.{status}")));
            }

            SelectedMediaTypeFilter = MediaTypeFilters.First(option => EqualityComparer<MediaType?>.Default.Equals(option.Value, selectedMediaType));
            SelectedStatusFilter = StatusFilters.First(option => EqualityComparer<MediaStatus?>.Default.Equals(option.Value, selectedStatus));
        }
        finally
        {
            _suppressFilterReload = false;
        }
    }

    private void ReloadDynamicFilterOptions(IReadOnlyList<MediaItemDto> library)
    {
        var selectedCategory = SelectedCategoryFilter?.Value;
        var selectedTag = SelectedTagFilter?.Value;

        _suppressFilterReload = true;
        try
        {
            CategoryFilters.Clear();
            CategoryFilters.Add(new LocalizedOption<string?>(null, T("Common.All")));
            foreach (var category in library
                         .Select(item => item.Category)
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Select(value => value!)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(value => value))
            {
                CategoryFilters.Add(new LocalizedOption<string?>(category, category));
            }

            TagFilters.Clear();
            TagFilters.Add(new LocalizedOption<string?>(null, T("Common.All")));
            foreach (var tag in library
                         .SelectMany(item => SplitTags(item.Tags))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(value => value))
            {
                TagFilters.Add(new LocalizedOption<string?>(tag, tag));
            }

            SelectedCategoryFilter = CategoryFilters.FirstOrDefault(option => string.Equals(option.Value, selectedCategory, StringComparison.OrdinalIgnoreCase))
                ?? CategoryFilters.First();
            SelectedTagFilter = TagFilters.FirstOrDefault(option => string.Equals(option.Value, selectedTag, StringComparison.OrdinalIgnoreCase))
                ?? TagFilters.First();
        }
        finally
        {
            _suppressFilterReload = false;
        }
    }

    private MediaItemListItemViewModel ToListItem(MediaItemDto item)
    {
        var type = T($"MediaType.{item.MediaType}");
        var status = T($"MediaStatus.{item.Status}");
        var progress = item.ProgressTotal.HasValue
            ? string.Format(T("Library.ProgressWithTotalFormat"), item.ProgressCurrent, item.ProgressTotal.Value)
            : string.Format(T("Library.ProgressFormat"), item.ProgressCurrent);
        var rating = item.Rating.HasValue
            ? string.Format(T("Library.RatingFormat"), item.Rating.Value)
            : T("Library.NoRating");
        var categoryLine = string.IsNullOrWhiteSpace(item.Category)
            ? T("Library.NoCategory")
            : string.Format(T("Library.CategoryFormat"), item.Category);
        var tagsLine = string.IsNullOrWhiteSpace(item.Tags)
            ? T("Library.NoTags")
            : string.Format(T("Library.TagsFormat"), item.Tags);

        return new MediaItemListItemViewModel(
            item.Id,
            item.Title,
            $"{type} - {status}",
            categoryLine,
            tagsLine,
            progress,
            rating,
            item.CoverUrl ?? string.Empty,
            !string.IsNullOrWhiteSpace(item.CoverUrl),
            string.IsNullOrWhiteSpace(item.CoverUrl),
            GetInitial(item.Title),
            item.IsFavorite,
            item.IsFavorite ? T("Library.FavoriteMarker") : string.Empty,
            T("Library.OpenButton"));
    }

    private IEnumerable<MediaItemDto> ApplyQuickFilter(IEnumerable<MediaItemDto> items)
    {
        return ActiveQuickFilter switch
        {
            LibraryQuickFilter.Favorites => items.Where(item => item.IsFavorite),
            LibraryQuickFilter.MissingCategory => items.Where(item => string.IsNullOrWhiteSpace(item.Category)),
            LibraryQuickFilter.MissingCover => items.Where(item => string.IsNullOrWhiteSpace(item.CoverUrl)),
            LibraryQuickFilter.Completed => items.Where(item => item.Status == MediaStatus.Completed),
            LibraryQuickFilter.InProgress => items.Where(item => item.Status is MediaStatus.InProgress or MediaStatus.Rewatching or MediaStatus.Rereading),
            _ => items
        };
    }

    private string GetQuickFilterText(LibraryQuickFilter filter, string key)
    {
        var label = T(key);
        return ActiveQuickFilter == filter ? $"[{label}]" : label;
    }

    private static IEnumerable<string> SplitTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return [];
        }

        return tags.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string GetInitial(string title)
    {
        return string.IsNullOrWhiteSpace(title)
            ? "?"
            : title.Trim()[0].ToString().ToUpperInvariant();
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        return await _authService.GetCurrentUserIdAsync() ?? "local-user";
    }
}
