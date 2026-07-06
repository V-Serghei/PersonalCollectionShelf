using System.Collections.ObjectModel;
using System.Globalization;
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
    private IReadOnlyList<MediaItemDto> _visibleItems = [];
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

    public ObservableCollection<MediaItemListItemViewModel> InProgressItems { get; } = [];

    public ObservableCollection<MediaItemListItemViewModel> RecentItems { get; } = [];

    public ObservableCollection<MediaItemListItemViewModel> FavoriteItems { get; } = [];

    public ObservableCollection<CategorySummaryViewModel> CategorySummaries { get; } = [];

    public ObservableCollection<StatusSummaryViewModel> StatusSummaries { get; } = [];

    public ObservableCollection<LocalizedOption<MediaType?>> MediaTypeFilters { get; } = [];

    public ObservableCollection<LocalizedOption<MediaStatus?>> StatusFilters { get; } = [];

    public ObservableCollection<LocalizedOption<string?>> CategoryFilters { get; } = [];

    public ObservableCollection<LocalizedOption<string?>> TagFilters { get; } = [];

    private string _searchTerm = string.Empty;
    private LocalizedOption<MediaType?>? _selectedMediaTypeFilter;
    private LocalizedOption<MediaStatus?>? _selectedStatusFilter;
    private LocalizedOption<string?>? _selectedCategoryFilter;
    private LocalizedOption<string?>? _selectedTagFilter;
    private LibraryQuickFilter _activeQuickFilter;

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetProperty(ref _searchTerm, value))
            {
                OnSearchTermChanged(value);
            }
        }
    }

    public LocalizedOption<MediaType?>? SelectedMediaTypeFilter
    {
        get => _selectedMediaTypeFilter;
        set
        {
            if (SetProperty(ref _selectedMediaTypeFilter, value))
            {
                OnSelectedMediaTypeFilterChanged(value);
            }
        }
    }

    public LocalizedOption<MediaStatus?>? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                OnSelectedStatusFilterChanged(value);
            }
        }
    }

    public LocalizedOption<string?>? SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                OnSelectedCategoryFilterChanged(value);
            }
        }
    }

    public LocalizedOption<string?>? SelectedTagFilter
    {
        get => _selectedTagFilter;
        set
        {
            if (SetProperty(ref _selectedTagFilter, value))
            {
                OnSelectedTagFilterChanged(value);
            }
        }
    }

    public LibraryQuickFilter ActiveQuickFilter
    {
        get => _activeQuickFilter;
        set
        {
            if (SetProperty(ref _activeQuickFilter, value))
            {
                OnActiveQuickFilterChanged(value);
            }
        }
    }

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

    public int TotalItemCount => _visibleItems.Count;

    public int CompletedItemCount => _visibleItems.Count(item => item.Status == MediaStatus.Completed);

    public int InProgressItemCount => _visibleItems.Count(item => item.Status is MediaStatus.InProgress or MediaStatus.Rewatching or MediaStatus.Rereading);

    public int FavoritesItemCount => _visibleItems.Count(item => item.IsFavorite);

    public string TotalItemsLabel => T("Common.All");

    public string CompletedItemsLabel => T("Library.QuickFilter.Completed");

    public string InProgressItemsLabel => T("Library.QuickFilter.InProgress");

    public string FavoritesItemsLabel => T("Library.QuickFilter.Favorites");

    public int WishlistItemCount => _visibleItems.Count(item => item.Status is MediaStatus.Planned or MediaStatus.OnHold);

    public string WishlistItemsLabel => T("MediaStatus.Planned");

    public string DashboardTitle => $"{GetGreeting()}, Alex";

    public string DashboardSubtitle => $"You have {InProgressItemCount} items in progress - {WishlistItemCount} in wishlist";

    public string ContinueSectionTitle => "Continue";

    public string RecentlyAddedSectionTitle => "Recently Added";

    public string FavoritesSectionTitle => T("Library.QuickFilter.Favorites");

    public string CategoryOverviewTitle => "Category Overview";

    public string SeeAllText => "See all";

    public string FullStatsText => "Full Stats";

    public string LibraryCountText => $"{TotalItemCount} items";

    public string AverageRatingText
    {
        get
        {
            var ratedItems = _visibleItems.Where(item => item.Rating.HasValue).ToList();
            if (ratedItems.Count == 0)
            {
                return "--";
            }

            return (ratedItems.Sum(item => item.Rating!.Value) / (double)ratedItems.Count).ToString("0.0", CultureInfo.InvariantCulture);
        }
    }

    public string CompletionPercentText
    {
        get
        {
            if (_visibleItems.Count == 0)
            {
                return "0%";
            }

            var percent = CompletedItemCount / (double)_visibleItems.Count;
            return percent.ToString("P0", CultureInfo.InvariantCulture);
        }
    }

    public string CompletionSubtitle => $"{CompletedItemCount} of {TotalItemCount} items";

    public string CollectionSubtitle => $"across {CategorySummaries.Count(summary => summary.Count > 0)} categories";

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        ReloadFilterOptions();
        PopulateMediaItems(_visibleItems);
    }

    private void OnSelectedMediaTypeFilterChanged(LocalizedOption<MediaType?>? value)
    {
        if (!_suppressFilterReload)
        {
            _ = LoadAsync();
        }
    }

    private void OnSelectedStatusFilterChanged(LocalizedOption<MediaStatus?>? value)
    {
        if (!_suppressFilterReload)
        {
            _ = LoadAsync();
        }
    }

    private void OnSelectedCategoryFilterChanged(LocalizedOption<string?>? value)
    {
        if (!_suppressFilterReload)
        {
            _ = LoadAsync();
        }
    }

    private void OnSelectedTagFilterChanged(LocalizedOption<string?>? value)
    {
        if (!_suppressFilterReload)
        {
            _ = LoadAsync();
        }
    }

    private void OnSearchTermChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _ = LoadAsync();
        }
    }

    private void OnActiveQuickFilterChanged(LibraryQuickFilter value)
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
    private async Task OpenLibraryAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("//Library");
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, "LibraryViewModel.OpenLibraryAsync");
        }
    }

    [RelayCommand]
    private async Task OpenStatisticsAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("//Statistics");
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, "LibraryViewModel.OpenStatisticsAsync");
        }
    }

    [RelayCommand]
    private void SelectMediaTypeFilter(LocalizedOption<MediaType?>? option)
    {
        if (option is not null)
        {
            SelectedMediaTypeFilter = option;
        }
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

            _visibleItems = ApplyQuickFilter(items).ToList();
            PopulateMediaItems(_visibleItems);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateMediaItemAsync()
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(EditMediaItemPage));
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, "LibraryViewModel.CreateMediaItemAsync");
        }
    }

    [RelayCommand]
    private async Task OpenMediaItemAsync(MediaItemListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await Shell.Current.GoToAsync($"{nameof(MediaDetailsPage)}?id={item.Id}");
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, $"LibraryViewModel.OpenMediaItemAsync id={item.Id}");
        }
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
        var hasCoverUrl = MediaPresentation.HasValidCoverUrl(item.CoverUrl);

        return new MediaItemListItemViewModel(
            item.Id,
            item.Title,
            type,
            MediaPresentation.GetMediaTypeColor(item.MediaType),
            status,
            MediaPresentation.GetStatusForegroundColor(item.Status),
            MediaPresentation.GetStatusBackgroundColor(item.Status),
            $"{type} - {status}",
            categoryLine,
            tagsLine,
            progress,
            MediaPresentation.GetProgressPercent(item.ProgressCurrent, item.ProgressTotal, item.Status),
            MediaPresentation.HasProgressBar(item.Status),
            rating,
            item.Rating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            item.Rating.HasValue,
            item.ReleaseYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            hasCoverUrl ? item.CoverUrl ?? string.Empty : string.Empty,
            hasCoverUrl,
            !hasCoverUrl,
            GetInitial(item.Title),
            item.IsFavorite,
            item.IsFavorite ? T("Library.FavoriteMarker") : string.Empty,
            T("Library.OpenButton"));
    }

    private void PopulateMediaItems(IEnumerable<MediaItemDto> items)
    {
        var itemList = items.ToList();

        MediaItems.Clear();
        foreach (var item in itemList)
        {
            MediaItems.Add(ToListItem(item));
        }

        PopulateDashboardCollections(itemList);
        RefreshCollectionSummary();
    }

    private void PopulateDashboardCollections(IReadOnlyList<MediaItemDto> items)
    {
        InProgressItems.Clear();
        foreach (var item in items
                     .Where(item => item.Status is MediaStatus.InProgress or MediaStatus.Rewatching or MediaStatus.Rereading)
                     .Take(6))
        {
            InProgressItems.Add(ToListItem(item));
        }

        RecentItems.Clear();
        foreach (var item in items
                     .OrderByDescending(item => item.CreatedAt)
                     .Take(6))
        {
            RecentItems.Add(ToListItem(item));
        }

        FavoriteItems.Clear();
        foreach (var item in items
                     .Where(item => item.IsFavorite)
                     .Take(6))
        {
            FavoriteItems.Add(ToListItem(item));
        }

        CategorySummaries.Clear();
        foreach (var mediaType in Enum.GetValues<MediaType>())
        {
            var matchingItems = items.Where(item => item.MediaType == mediaType).ToList();
            CategorySummaries.Add(new CategorySummaryViewModel(
                mediaType,
                T($"MediaType.{mediaType}"),
                matchingItems.Count,
                matchingItems.Count(item => item.Status == MediaStatus.Completed),
                MediaPresentation.GetMediaTypeColor(mediaType)));
        }

        StatusSummaries.Clear();
        foreach (var status in Enum.GetValues<MediaStatus>())
        {
            StatusSummaries.Add(new StatusSummaryViewModel(
                status,
                T($"MediaStatus.{status}"),
                items.Count(item => item.Status == status),
                MediaPresentation.GetStatusForegroundColor(status)));
        }
    }

    private void RefreshCollectionSummary()
    {
        OnPropertyChanged(nameof(TotalItemCount));
        OnPropertyChanged(nameof(CompletedItemCount));
        OnPropertyChanged(nameof(InProgressItemCount));
        OnPropertyChanged(nameof(FavoritesItemCount));
        OnPropertyChanged(nameof(WishlistItemCount));
        OnPropertyChanged(nameof(DashboardTitle));
        OnPropertyChanged(nameof(DashboardSubtitle));
        OnPropertyChanged(nameof(LibraryCountText));
        OnPropertyChanged(nameof(AverageRatingText));
        OnPropertyChanged(nameof(CompletionPercentText));
        OnPropertyChanged(nameof(CompletionSubtitle));
        OnPropertyChanged(nameof(CollectionSubtitle));
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

    private static string GetGreeting()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            < 12 => "Good morning",
            < 18 => "Good afternoon",
            _ => "Good evening"
        };
    }
}
