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

public readonly record struct LibraryScrollRestorePoint(int Index, int Offset);

public partial class LibraryViewModel : BaseViewModel, IQueryAttributable
{
    private const string ViewModePreferenceKey = "library.viewMode";
    private const int LibraryPageSize = 240;
    private const string GridColumnCountPreferenceKey = "library.gridColumnCount";
    private const int ThumbnailBatchSize = 500;
    private const int ThumbnailBatchLead = 400;

    private readonly IMediaItemService _mediaItemService;
    private readonly IAuthService _authService;
    private readonly UiThumbnailCache _thumbnailCache;
    private readonly Dictionary<Guid, string> _coverSources = [];
    private int _thumbnailViewportVersion;
    private IReadOnlyList<MediaItemDto> _allItems = [];
    private IReadOnlyList<MediaItemDto> _visibleItems = [];
    private IReadOnlyList<MediaItemListItemViewModel> _preparedMediaItems = [];
    private CancellationTokenSource? _searchDelayCancellation;
    private bool _suppressFilterReload;
    private bool _isGridView = Microsoft.Maui.Storage.Preferences.Get(ViewModePreferenceKey, "grid") != "list";
    private bool _isSearchVisible;
    private bool _isFilterPanelVisible = true;
    private bool _initialThumbnailBatchPrepared;
    private readonly object _thumbnailBatchLock = new();
    private int _nextThumbnailBatchStart;
    private int _thumbnailOrderingVersion;
    private static readonly LibrarySessionState SessionState = new();

    private sealed class LibrarySessionState
    {
        public bool HasMediaTypeFilter { get; set; }
        public MediaType? MediaTypeFilter { get; set; }
        public Guid? ScrollAnchorId { get; set; }
        public int ScrollIndex { get; set; }
        public int ScrollOffset { get; set; }
        public bool HasScrollPosition { get; set; }
    }

    public LibraryViewModel(
        IMediaItemService mediaItemService,
        IAuthService authService,
        UiThumbnailCache thumbnailCache,
        ILocalizationService localizationService)
        : base(localizationService)
    {
        _mediaItemService = mediaItemService;
        _authService = authService;
        _thumbnailCache = thumbnailCache;
        ReloadFilterOptions();
        RestoreSessionFilter();
    }

    private ObservableCollection<MediaItemListItemViewModel> _mediaItems = [];

    public ObservableCollection<MediaItemListItemViewModel> MediaItems
    {
        get => _mediaItems;
        private set => SetProperty(ref _mediaItems, value);
    }

    public ObservableCollection<MediaItemListItemViewModel> InProgressItems { get; } = [];

    public ObservableCollection<MediaItemListItemViewModel> RecentItems { get; } = [];

    public ObservableCollection<MediaItemListItemViewModel> FavoriteItems { get; } = [];

    public ObservableCollection<CategorySummaryViewModel> CategorySummaries { get; } = [];

    public ObservableCollection<StatusSummaryViewModel> StatusSummaries { get; } = [];

    public ObservableCollection<MonthlyActivityPoint> MonthlyActivity { get; } = [];

    public ObservableCollection<StatisticPoint> ReleaseYearStatistics { get; } = [];
    public ObservableCollection<StatisticPoint> AddedYearStatistics { get; } = [];
    public ObservableCollection<StatisticPoint> DecadeStatistics { get; } = [];
    public ObservableCollection<StatisticPoint> GenreStatistics { get; } = [];
    public ObservableCollection<StatisticPoint> CountryStatistics { get; } = [];
    public ObservableCollection<StatisticPoint> RatingStatistics { get; } = [];
    public ObservableCollection<StatisticPoint> AverageRatingByTypeStatistics { get; } = [];
    public ObservableCollection<StatisticPoint> CompletionByTypeStatistics { get; } = [];
    public ObservableCollection<StatisticPoint> FavoritesByTypeStatistics { get; } = [];

    public ObservableCollection<LocalizedOption<MediaType?>> MediaTypeFilters { get; } = [];

    public ObservableCollection<LocalizedOption<MediaStatus?>> StatusFilters { get; } = [];

    public ObservableCollection<LocalizedOption<string?>> CategoryFilters { get; } = [];

    public ObservableCollection<LocalizedOption<string?>> TagFilters { get; } = [];

    public ObservableCollection<LocalizedOption<LibrarySortOption>> SortOptions { get; } = [];

    public bool IsGridView
    {
        get => _isGridView;
        private set
        {
            if (SetProperty(ref _isGridView, value))
            {
                OnPropertyChanged(nameof(IsListView));
                OnPropertyChanged(nameof(ViewModeIcon));
                Microsoft.Maui.Storage.Preferences.Set(ViewModePreferenceKey, value ? "grid" : "list");
            }
        }
    }

    public bool IsListView => !_isGridView;

    public string ViewModeIcon => IsGridView ? "≡" : "▦";

    public int GridColumnCount => Math.Clamp(
        Microsoft.Maui.Storage.Preferences.Get(GridColumnCountPreferenceKey, 2), 1, 4);

    public double GridPosterHeight => GridColumnCount switch
    {
        1 => 420,
        2 => 230,
        3 => 155,
        _ => 118
    };

    public bool UseCompactGridCard => GridColumnCount >= 3;

    public bool UseFullGridCard => !UseCompactGridCard;

    public bool IsFilterPanelVisible
    {
        get => _isFilterPanelVisible;
        set => SetProperty(ref _isFilterPanelVisible, value);
    }

    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set => SetProperty(ref _isSearchVisible, value);
    }


    [RelayCommand]
    private void SetGridView()
    {
        IsGridView = true;
    }

    [RelayCommand]
    private void SetListView()
    {
        IsGridView = false;
    }

    [RelayCommand]
    private void ToggleView() => IsGridView = !IsGridView;

    [RelayCommand]
    private void ToggleFilters() => IsFilterPanelVisible = !IsFilterPanelVisible;

    public void RefreshDisplayPreferences()
    {
        OnPropertyChanged(nameof(GridColumnCount));
        OnPropertyChanged(nameof(GridPosterHeight));
        OnPropertyChanged(nameof(UseCompactGridCard));
        OnPropertyChanged(nameof(UseFullGridCard));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("reset", out var reset) && string.Equals(reset?.ToString(), "true", StringComparison.OrdinalIgnoreCase))
        {
            SessionState.HasMediaTypeFilter = true;
            SessionState.MediaTypeFilter = null;
            ClearSavedScrollPosition();
            SelectedMediaTypeFilter = MediaTypeFilters.FirstOrDefault(option => option.Value is null);
            SelectedStatusFilter = StatusFilters.FirstOrDefault(option => option.Value is null);
        }

        if (query.TryGetValue("mediaType", out var raw) &&
            Enum.TryParse<MediaType>(raw?.ToString(), true, out var mediaType))
        {
            var option = MediaTypeFilters.FirstOrDefault(candidate => candidate.Value == mediaType);
            if (option is not null)
            {
                if (SessionState.MediaTypeFilter != mediaType) ClearSavedScrollPosition();
                SessionState.HasMediaTypeFilter = true;
                SessionState.MediaTypeFilter = mediaType;
                SelectedMediaTypeFilter = option;
            }
        }

        if (query.TryGetValue("status", out var rawStatus) &&
            Enum.TryParse<MediaStatus>(rawStatus?.ToString(), true, out var status))
        {
            var option = StatusFilters.FirstOrDefault(candidate => candidate.Value == status);
            if (option is not null)
            {
                SelectedStatusFilter = option;
            }
        }
    }

    private string _searchTerm = string.Empty;
    private LocalizedOption<MediaType?>? _selectedMediaTypeFilter;
    private LocalizedOption<MediaStatus?>? _selectedStatusFilter;
    private LocalizedOption<string?>? _selectedCategoryFilter;
    private LocalizedOption<string?>? _selectedTagFilter;
    private LocalizedOption<LibrarySortOption>? _selectedSortOption;
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
                foreach (var option in MediaTypeFilters) option.IsSelected = ReferenceEquals(option, value);
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

    public LocalizedOption<LibrarySortOption>? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value) && !_suppressFilterReload)
            {
                PopulateMediaItems(_visibleItems);
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

    public string PageTitle => SelectedStatusFilter?.Value is null
        ? T("Library.Title")
        : SelectedStatusFilter.DisplayName;

    public string SearchPlaceholder => T("Library.SearchPlaceholder");

    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        if (!IsSearchVisible) SearchTerm = string.Empty;
    }

    public string MediaTypeFilterPlaceholder => T("Library.MediaTypeFilterPlaceholder");

    public string StatusFilterPlaceholder => T("Library.StatusFilterPlaceholder");

    public string CategoryFilterPlaceholder => T("Library.CategoryFilterPlaceholder");

    public string TagFilterPlaceholder => T("Library.TagFilterPlaceholder");

    public string SortByPlaceholder => T("Library.SortByPlaceholder");

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

    public string DashboardTitle => GetGreeting();

    public string DashboardSubtitle => string.Format(T("Dashboard.SubtitleFormat"), InProgressItemCount, WishlistItemCount);

    public string ContinueSectionTitle => T("Dashboard.Continue");

    public string RecentlyAddedSectionTitle => T("Dashboard.RecentlyAdded");

    public string FavoritesSectionTitle => T("Library.QuickFilter.Favorites");

    public string CategoryOverviewTitle => T("Dashboard.CategoryOverview");

    public string SeeAllText => T("Dashboard.SeeAll");

    public string FullStatsText => T("Dashboard.FullStatistics");

    public string LibraryCountText => string.Format(T("Dashboard.ItemCountFormat"), TotalItemCount);

    public string StatisticsTitle => T("Shell.Statistics");

    public string AverageRatingLabel => T("Statistics.AverageRating");

    public string CompletionLabel => T("Statistics.Completion");

    public string CollectionLabel => T("Statistics.Collection");

    public string MonthlyActivityTitle => T("Statistics.MonthlyActivity");

    public string ByCategoryTitle => T("Statistics.ByCategory");

    public string ByStatusTitle => T("Statistics.ByStatus");

    public string StatisticsItemsLabel => T("Statistics.Items");

    public string StatisticsOverviewTab => T("Statistics.Tab.Overview");
    public string StatisticsTimeTab => T("Statistics.Tab.Time");
    public string StatisticsGenresTab => T("Statistics.Tab.Genres");
    public string StatisticsRatingsTab => T("Statistics.Tab.Ratings");
    public string ReleasesByYearTitle => T("Statistics.ReleasesByYear");
    public string AddedByYearTitle => T("Statistics.AddedByYear");
    public string ByDecadeTitle => T("Statistics.ByDecade");
    public string ByGenreTitle => T("Statistics.ByGenre");
    public string ByCountryTitle => T("Statistics.ByCountry");
    public string RatingDistributionTitle => T("Statistics.RatingDistribution");
    public string AverageRatingByTypeTitle => T("Statistics.AverageRatingByType");
    public string CompletionByTypeTitle => T("Statistics.CompletionByType");
    public string FavoritesByTypeTitle => T("Statistics.FavoritesByType");

    public string AverageRatingText
    {
        get
        {
            var ratedItems = _visibleItems.Where(item => item.Rating.HasValue).ToList();
            if (ratedItems.Count == 0)
            {
                return "--";
            }

            return (ratedItems.Sum(item => item.Rating!.Value) / ratedItems.Count).ToString("0.0", CultureInfo.InvariantCulture);
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

    public string CompletionSubtitle => string.Format(T("Statistics.CompletionFormat"), CompletedItemCount, TotalItemCount);

    public string CollectionSubtitle => string.Format(T("Statistics.CategoriesFormat"), CategorySummaries.Count(summary => summary.Count > 0));

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        ReloadFilterOptions();
        PopulateMediaItems(_visibleItems);
        PopulateDashboardCollections(_visibleItems);
    }

    private void OnSelectedMediaTypeFilterChanged(LocalizedOption<MediaType?>? value)
    {
        if (Shell.Current is AppShell shell)
        {
            shell.SetLibraryCategoryFilter(value?.Value);
        }

        if (!_suppressFilterReload)
        {
            ApplyCurrentFilters();
        }
    }

    private void OnSelectedStatusFilterChanged(LocalizedOption<MediaStatus?>? value)
    {
        OnPropertyChanged(nameof(PageTitle));
        if (!_suppressFilterReload)
        {
            ApplyCurrentFilters();
        }
    }

    private void OnSelectedCategoryFilterChanged(LocalizedOption<string?>? value)
    {
        if (!_suppressFilterReload)
        {
            ApplyCurrentFilters();
        }
    }

    private void OnSelectedTagFilterChanged(LocalizedOption<string?>? value)
    {
        if (!_suppressFilterReload)
        {
            ApplyCurrentFilters();
        }
    }

    private void OnSearchTermChanged(string value)
    {
        ScheduleSearchRefresh(string.IsNullOrWhiteSpace(value) ? TimeSpan.Zero : TimeSpan.FromMilliseconds(250));
    }

    private void OnActiveQuickFilterChanged(LibraryQuickFilter value)
    {
        ApplyCurrentFilters();
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
    private async Task OpenCompletedAsync()
    {
        await OpenLibraryFilterAsync("status=Completed", "LibraryViewModel.OpenCompletedAsync");
    }

    [RelayCommand]
    private async Task OpenInProgressAsync()
    {
        await OpenLibraryFilterAsync("status=InProgress", "LibraryViewModel.OpenInProgressAsync");
    }

    [RelayCommand]
    private async Task OpenPlannedAsync()
    {
        await OpenLibraryFilterAsync("status=Planned", "LibraryViewModel.OpenPlannedAsync");
    }

    [RelayCommand]
    private async Task OpenCategoryAsync(CategorySummaryViewModel? category)
    {
        if (category is null)
        {
            return;
        }

        try
        {
            await Shell.Current.GoToAsync($"//Library?mediaType={category.MediaType}");
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, $"LibraryViewModel.OpenCategoryAsync type={category.MediaType}");
        }
    }

    private static async Task OpenLibraryFilterAsync(string query, string context)
    {
        try
        {
            await Shell.Current.GoToAsync($"//Library?{query}");
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, context);
        }
    }

    [RelayCommand]
    private void SelectMediaTypeFilter(LocalizedOption<MediaType?>? option)
    {
        if (option is not null)
        {
            if (!EqualityComparer<MediaType?>.Default.Equals(SessionState.MediaTypeFilter, option.Value))
            {
                ClearSavedScrollPosition();
            }

            SessionState.HasMediaTypeFilter = true;
            SessionState.MediaTypeFilter = option.Value;
            SelectedMediaTypeFilter = option;
        }
    }

    public void RememberScrollPosition(int firstVisibleIndex, int offset = 0)
    {
        if (firstVisibleIndex < 0 || MediaItems.Count == 0) return;
        var index = Math.Clamp(firstVisibleIndex, 0, MediaItems.Count - 1);
        SessionState.ScrollAnchorId = MediaItems[index].Id;
        SessionState.ScrollIndex = index;
        SessionState.ScrollOffset = offset;
        SessionState.HasScrollPosition = true;
    }

    public LibraryScrollRestorePoint? GetSavedScrollPosition()
    {
        if (!SessionState.HasScrollPosition || MediaItems.Count == 0) return null;

        var index = -1;
        if (SessionState.ScrollAnchorId.HasValue)
        {
            for (var candidate = 0; candidate < MediaItems.Count; candidate++)
            {
                if (MediaItems[candidate].Id != SessionState.ScrollAnchorId.Value) continue;
                index = candidate;
                break;
            }
        }

        if (index < 0) index = Math.Clamp(SessionState.ScrollIndex, 0, MediaItems.Count - 1);
        return new LibraryScrollRestorePoint(index, SessionState.ScrollOffset);
    }

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
            _allItems = library;
            ReloadDynamicFilterOptions(library);
            ApplyCurrentFilters();
            StartInitialThumbnailPipeline();
            _ = _thumbnailCache.PrimeDisplaySourcesAsync(library.Select(item => item.CoverUrl));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void LoadMore()
    {
        if (MediaItems.Count >= _preparedMediaItems.Count)
        {
            return;
        }

        var targetCount = Math.Min(MediaItems.Count + LibraryPageSize, _preparedMediaItems.Count);
        for (var index = MediaItems.Count; index < targetCount; index++)
        {
            MediaItems.Add(_preparedMediaItems[index]);
        }
    }

    [RelayCommand]
    private async Task CreateMediaItemAsync()
    {
        try
        {
            await AppNavigation.OpenEditMediaItemAsync();
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
            await AppNavigation.OpenMediaDetailsAsync(item.Id);
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
        var selectedSort = SelectedSortOption?.Value ?? LibrarySortOption.DateAddedNewest;

        _suppressFilterReload = true;
        try
        {
            MediaTypeFilters.Clear();
            MediaTypeFilters.Add(new LocalizedOption<MediaType?>(null, T("Common.All")));
            foreach (var mediaType in MediaPresentation.OrderedMediaTypes)
            {
                MediaTypeFilters.Add(new LocalizedOption<MediaType?>(mediaType, T($"MediaType.{mediaType}")));
            }

            StatusFilters.Clear();
            StatusFilters.Add(new LocalizedOption<MediaStatus?>(null, T("Common.All")));
            foreach (var status in Enum.GetValues<MediaStatus>())
            {
                StatusFilters.Add(new LocalizedOption<MediaStatus?>(status, T($"MediaStatus.{status}")));
            }

            SortOptions.Clear();
            foreach (var sortOption in Enum.GetValues<LibrarySortOption>())
            {
                SortOptions.Add(new LocalizedOption<LibrarySortOption>(sortOption, T($"Library.SortOption.{sortOption}")));
            }

            SelectedMediaTypeFilter = MediaTypeFilters.First(option => EqualityComparer<MediaType?>.Default.Equals(option.Value, selectedMediaType));
            foreach (var option in MediaTypeFilters) option.IsSelected = ReferenceEquals(option, SelectedMediaTypeFilter);
            SelectedStatusFilter = StatusFilters.First(option => EqualityComparer<MediaStatus?>.Default.Equals(option.Value, selectedStatus));
            SelectedSortOption = SortOptions.First(option => option.Value == selectedSort);
        }
        finally
        {
            _suppressFilterReload = false;
        }
    }

    private void RestoreSessionFilter()
    {
        if (!SessionState.HasMediaTypeFilter) return;
        var option = MediaTypeFilters.FirstOrDefault(candidate =>
            EqualityComparer<MediaType?>.Default.Equals(candidate.Value, SessionState.MediaTypeFilter));
        if (option is null) return;

        _suppressFilterReload = true;
        try
        {
            SelectedMediaTypeFilter = option;
        }
        finally
        {
            _suppressFilterReload = false;
        }
    }

    private static void ClearSavedScrollPosition()
    {
        SessionState.ScrollAnchorId = null;
        SessionState.ScrollIndex = 0;
        SessionState.ScrollOffset = 0;
        SessionState.HasScrollPosition = false;
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
        var displayCoverUrl = _thumbnailCache.GetDisplaySource(item.CoverUrl);
        var hasCoverUrl = MediaPresentation.HasValidCoverUrl(displayCoverUrl);

        return new MediaItemListItemViewModel(
            item.Id,
            item.Title,
            type,
            MediaPresentation.GetMediaTypeColor(item.MediaType),
            status,
            GetStatusIcon(item.Status),
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
            item.ReleaseYear.HasValue,
            item.CoverUrl ?? string.Empty,
            hasCoverUrl ? displayCoverUrl : string.Empty,
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
        _coverSources.Clear();
        foreach (var item in itemList.Where(item => MediaPresentation.HasValidCoverUrl(item.CoverUrl)))
        {
            _coverSources[item.Id] = item.CoverUrl!;
        }

        var preparedItems = SortItems(itemList).Select(ToListItem).ToList();
        var canUpdateInPlace = MediaItems.Count <= preparedItems.Count &&
                               MediaItems.Select(item => item.Id)
                                   .SequenceEqual(preparedItems.Take(MediaItems.Count).Select(item => item.Id));

        _preparedMediaItems = preparedItems;
        Interlocked.Increment(ref _thumbnailOrderingVersion);
        lock (_thumbnailBatchLock)
        {
            _nextThumbnailBatchStart = _initialThumbnailBatchPrepared
                ? Math.Min(ThumbnailBatchSize, preparedItems.Count)
                : 0;
        }
        if (canUpdateInPlace && MediaItems.Count == preparedItems.Count)
        {
            for (var index = 0; index < MediaItems.Count; index++)
            {
                if (!MediaItems[index].HasSameContent(preparedItems[index]))
                {
                    MediaItems[index] = preparedItems[index];
                }
            }

        }
        else
        {
            MediaItems = new ObservableCollection<MediaItemListItemViewModel>(preparedItems);
        }

        PopulateDashboardCollections(itemList);
        RefreshCollectionSummary();
        PreloadThumbnails(0, Math.Min(30, MediaItems.Count - 1));
    }

    public void PreloadThumbnails(int firstVisibleIndex, int lastVisibleIndex)
    {
        if (MediaItems.Count == 0 || lastVisibleIndex < 0) return;
        var version = Interlocked.Increment(ref _thumbnailViewportVersion);
        _ = PreloadThumbnailRangeAsync(firstVisibleIndex, lastVisibleIndex, version);
    }

    public void EnsureThumbnailLookAhead(int lastVisibleIndex)
    {
        if (!_initialThumbnailBatchPrepared || lastVisibleIndex < 0) return;

        while (true)
        {
            (Guid Id, string Source)[] batch;
            int version;
            lock (_thumbnailBatchLock)
            {
                if (_nextThumbnailBatchStart >= _preparedMediaItems.Count ||
                    lastVisibleIndex < Math.Max(0, _nextThumbnailBatchStart - ThumbnailBatchLead))
                {
                    return;
                }

                var start = _nextThumbnailBatchStart;
                var count = Math.Min(ThumbnailBatchSize, _preparedMediaItems.Count - start);
                _nextThumbnailBatchStart += count;
                version = Volatile.Read(ref _thumbnailOrderingVersion);
                batch = _preparedMediaItems
                    .Skip(start)
                    .Take(count)
                    .Select(item => (item.Id, _coverSources.GetValueOrDefault(item.Id) ?? string.Empty))
                    .Where(item => !string.IsNullOrWhiteSpace(item.Item2))
                    .ToArray();
            }

            _ = WarmThumbnailBatchAsync(batch, version);
        }
    }

    private void StartInitialThumbnailPipeline()
    {
        if (_initialThumbnailBatchPrepared || _preparedMediaItems.Count == 0) return;
        _initialThumbnailBatchPrepared = true;
        var version = Volatile.Read(ref _thumbnailOrderingVersion);
        var firstBatch = _preparedMediaItems
            .Take(ThumbnailBatchSize)
            .Select(item => (item.Id, _coverSources.GetValueOrDefault(item.Id) ?? string.Empty))
            .Where(item => !string.IsNullOrWhiteSpace(item.Item2))
            .ToArray();
        lock (_thumbnailBatchLock)
        {
            _nextThumbnailBatchStart = Math.Min(ThumbnailBatchSize, _preparedMediaItems.Count);
        }
        _ = WarmThumbnailBatchAsync(firstBatch, version);
    }

    private async Task WarmThumbnailBatchAsync((Guid Id, string Source)[] batch, int version)
    {
        if (batch.Length == 0) return;
        await _thumbnailCache.WarmBatchAsync(
            batch.Select(item => item.Source),
            batch.Length,
            degreeOfParallelism: 1).ConfigureAwait(false);
        if (version != Volatile.Read(ref _thumbnailOrderingVersion)) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (version != Volatile.Read(ref _thumbnailOrderingVersion)) return;
            var cards = MediaItems.ToDictionary(item => item.Id);
            foreach (var (id, source) in batch)
            {
                if (!cards.TryGetValue(id, out var card)) continue;
                var displaySource = _thumbnailCache.GetDisplaySource(source);
                if (!string.IsNullOrWhiteSpace(displaySource)) card.SetCoverSource(displaySource, notify: false);
            }
        });
    }

    private async Task PreloadThumbnailRangeAsync(int firstVisibleIndex, int lastVisibleIndex, int version)
    {
        var start = Math.Max(0, firstVisibleIndex - 30);
        var end = Math.Min(MediaItems.Count - 1, lastVisibleIndex + 30);
        var center = Math.Clamp((firstVisibleIndex + lastVisibleIndex) / 2, start, end);
        var indices = Enumerable.Range(start, end - start + 1)
            .OrderBy(index => Math.Abs(index - center));
        var ready = new List<(int Index, Guid Id, string Source)>();

        foreach (var index in indices)
        {
            if (version != Volatile.Read(ref _thumbnailViewportVersion)) return;
            if (index >= MediaItems.Count) return;
            var itemId = MediaItems[index].Id;
            if (!_coverSources.TryGetValue(itemId, out var source)) continue;
            var thumbnail = await _thumbnailCache.GetOrCreateAsync(source).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(thumbnail)) continue;
            ready.Add((index, itemId, thumbnail));
        }

        if (version != Volatile.Read(ref _thumbnailViewportVersion) || ready.Count == 0) return;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (version != Volatile.Read(ref _thumbnailViewportVersion)) return;
            foreach (var item in ready)
            {
                if (item.Index >= MediaItems.Count || MediaItems[item.Index].Id != item.Id) continue;
                var isVisible = item.Index >= firstVisibleIndex && item.Index <= lastVisibleIndex;
                MediaItems[item.Index].SetCoverSource(item.Source, notify: isVisible);
            }
        });
    }

    private static string GetStatusIcon(MediaStatus status) => status switch
    {
        MediaStatus.Completed => "✓",
        MediaStatus.InProgress => "▶",
        MediaStatus.Planned => "○",
        MediaStatus.OnHold => "Ⅱ",
        MediaStatus.Dropped => "×",
        MediaStatus.Rewatching => "↻",
        MediaStatus.Rereading => "↻",
        _ => "•"
    };

    private IEnumerable<MediaItemDto> SortItems(IEnumerable<MediaItemDto> items)
    {
        return SelectedSortOption?.Value switch
        {
            LibrarySortOption.TitleAsc => items.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            LibrarySortOption.TitleDesc => items.OrderByDescending(item => item.Title, StringComparer.OrdinalIgnoreCase),
            LibrarySortOption.DateAddedOldest => items.OrderBy(item => item.CreatedAt),
            LibrarySortOption.RatingHighest => items.OrderByDescending(item => item.Rating ?? -1),
            LibrarySortOption.RatingLowest => items.OrderBy(item => item.Rating ?? int.MaxValue),
            LibrarySortOption.ReleaseYearNewest => items.OrderByDescending(item => item.ReleaseYear ?? int.MinValue),
            LibrarySortOption.ReleaseYearOldest => items.OrderBy(item => item.ReleaseYear ?? int.MaxValue),
            _ => items.OrderByDescending(item => item.CreatedAt)
        };
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
        foreach (var mediaType in MediaPresentation.OrderedMediaTypes)
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

        MonthlyActivity.Clear();
        var now = DateTime.Now;
        for (var offset = 8; offset >= 0; offset--)
        {
            var month = new DateTime(now.Year, now.Month, 1).AddMonths(-offset);
            var count = items.Count(item => item.CreatedAt.Year == month.Year && item.CreatedAt.Month == month.Month);
            var culture = CultureInfo.GetCultureInfo(LocalizationService.CurrentLanguage);
            MonthlyActivity.Add(new MonthlyActivityPoint(month.ToString("MMM", culture), count));
        }

        PopulateStatisticPoints(ReleaseYearStatistics, items
            .Where(item => item.ReleaseYear.HasValue)
            .GroupBy(item => item.ReleaseYear!.Value)
            .OrderBy(group => group.Key)
            .TakeLast(24)
            .Select((group, index) => new StatisticPoint(group.Key.ToString(CultureInfo.InvariantCulture), group.Count(), ChartColor(index))));

        PopulateStatisticPoints(AddedYearStatistics, items
            .GroupBy(item => item.CreatedAt.Year)
            .OrderBy(group => group.Key)
            .Select((group, index) => new StatisticPoint(group.Key.ToString(CultureInfo.InvariantCulture), group.Count(), ChartColor(index))));

        PopulateStatisticPoints(DecadeStatistics, items
            .Where(item => item.ReleaseYear.HasValue)
            .GroupBy(item => item.ReleaseYear!.Value / 10 * 10)
            .OrderBy(group => group.Key)
            .Select((group, index) => new StatisticPoint($"{group.Key}s", group.Count(), ChartColor(index))));

        PopulateStatisticPoints(GenreStatistics, items
            .SelectMany(item => item.Genres ?? [])
            .Where(genre => !string.IsNullOrWhiteSpace(genre))
            .GroupBy(genre => genre.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(18)
            .Select((group, index) => new StatisticPoint(group.Key, group.Count(), ChartColor(index))));

        PopulateStatisticPoints(CountryStatistics, items
            .Select(item => item.MovieDetails?.CountryOfOrigin ?? item.BookDetails?.CountryOfOrigin)
            .Where(country => !string.IsNullOrWhiteSpace(country))
            .SelectMany(country => country!.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .GroupBy(country => country, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Take(15)
            .Select((group, index) => new StatisticPoint(group.Key, group.Count(), ChartColor(index))));

        PopulateStatisticPoints(RatingStatistics, Enumerable.Range(1, 10)
            .Select((rating, index) => new StatisticPoint(
                rating.ToString(CultureInfo.InvariantCulture),
                items.Count(item => item.Rating.HasValue && Math.Clamp((int)Math.Round(item.Rating.Value), 1, 10) == rating),
                ChartColor(index))));

        PopulateStatisticPoints(AverageRatingByTypeStatistics, MediaPresentation.OrderedMediaTypes
            .Select(type =>
            {
                var ratings = items.Where(item => item.MediaType == type && item.Rating.HasValue).Select(item => item.Rating!.Value).ToList();
                return new StatisticPoint(T($"MediaType.{type}"), ratings.Count == 0 ? 0 : (double)ratings.Average(), MediaPresentation.GetMediaTypeColor(type));
            }).Where(point => point.Value > 0));

        PopulateStatisticPoints(CompletionByTypeStatistics, MediaPresentation.OrderedMediaTypes
            .Select(type =>
            {
                var values = items.Where(item => item.MediaType == type).ToList();
                var percent = values.Count == 0 ? 0 : values.Count(item => item.Status == MediaStatus.Completed) * 100d / values.Count;
                return new StatisticPoint(T($"MediaType.{type}"), percent, MediaPresentation.GetMediaTypeColor(type), $"{percent:0}%");
            }).Where(point => point.Value > 0));

        PopulateStatisticPoints(FavoritesByTypeStatistics, MediaPresentation.OrderedMediaTypes
            .Select(type => new StatisticPoint(
                T($"MediaType.{type}"),
                items.Count(item => item.MediaType == type && item.IsFavorite),
                MediaPresentation.GetMediaTypeColor(type)))
            .Where(point => point.Value > 0));
    }

    private static void PopulateStatisticPoints(ObservableCollection<StatisticPoint> target, IEnumerable<StatisticPoint> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private static Color ChartColor(int index)
    {
        string[] colors = ["#9D7FF4", "#60A5FA", "#4ADE80", "#FBBF24", "#F07CB8", "#E07C54", "#4DD0E1", "#C47CF0"];
        return Color.FromArgb(colors[Math.Abs(index) % colors.Length]);
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

    private void ApplyCurrentFilters()
    {
        IEnumerable<MediaItemDto> items = _allItems;
        var searchTerm = SearchTerm.Trim();

        if (searchTerm.Length > 0)
        {
            items = items.Where(item =>
                Contains(item.Title, searchTerm) ||
                Contains(item.OriginalTitle, searchTerm) ||
                Contains(item.Description, searchTerm) ||
                Contains(item.Category, searchTerm) ||
                Contains(item.Tags, searchTerm) ||
                Contains(item.SerialNumber, searchTerm) ||
                Contains(item.Notes, searchTerm));
        }

        if (SelectedMediaTypeFilter?.Value is { } mediaType)
        {
            items = items.Where(item => item.MediaType == mediaType);
        }

        if (SelectedStatusFilter?.Value is { } status)
        {
            items = items.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(SelectedCategoryFilter?.Value))
        {
            items = items.Where(item =>
                string.Equals(item.Category, SelectedCategoryFilter.Value, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedTagFilter?.Value))
        {
            items = items.Where(item => SplitTags(item.Tags)
                .Any(tag => string.Equals(tag, SelectedTagFilter.Value, StringComparison.OrdinalIgnoreCase)));
        }

        _visibleItems = ApplyQuickFilter(items).ToList();
        PopulateMediaItems(_visibleItems);
    }

    private async void ScheduleSearchRefresh(TimeSpan delay)
    {
        _searchDelayCancellation?.Cancel();
        _searchDelayCancellation?.Dispose();
        _searchDelayCancellation = new CancellationTokenSource();
        var cancellationToken = _searchDelayCancellation.Token;

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            await MainThread.InvokeOnMainThreadAsync(ApplyCurrentFilters);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static bool Contains(string? source, string value) =>
        source?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;

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

    private string GetGreeting()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            < 12 => T("Dashboard.Greeting.Morning"),
            < 18 => T("Dashboard.Greeting.Afternoon"),
            _ => T("Dashboard.Greeting.Evening")
        };
    }
}
