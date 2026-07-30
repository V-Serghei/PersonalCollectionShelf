using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public enum CollectionSortOption
{
    NameAscending,
    NameDescending,
    MostItems,
    FewestItems,
    Category,
    Kind
}

public sealed class CollectionCardViewModel : ObservableObject
{
    private string _firstCover;
    private string _secondCover;
    private string _thirdCover;
    private string _fourthCover;
    private string _fifthCover;

    public CollectionCardViewModel(Guid id, string name, string kindLabel, int itemCount, string itemCountText, string categorySummary, IReadOnlyList<string> covers)
    {
        Id = id;
        Name = name;
        KindLabel = kindLabel;
        ItemCount = itemCount;
        ItemCountText = itemCountText;
        CategorySummary = categorySummary;
        _firstCover = covers.ElementAtOrDefault(0) ?? string.Empty;
        _secondCover = covers.ElementAtOrDefault(1) ?? string.Empty;
        _thirdCover = covers.ElementAtOrDefault(2) ?? string.Empty;
        _fourthCover = covers.ElementAtOrDefault(3) ?? string.Empty;
        _fifthCover = covers.ElementAtOrDefault(4) ?? string.Empty;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string KindLabel { get; }
    public int ItemCount { get; }
    public string ItemCountText { get; }
    public string CategorySummary { get; }
    public string FirstCover => _firstCover;
    public string SecondCover => _secondCover;
    public string ThirdCover => _thirdCover;
    public string FourthCover => _fourthCover;
    public string FifthCover => _fifthCover;
    public bool HasFirstCover => _firstCover.Length > 0;
    public bool HasSecondCover => _secondCover.Length > 0;
    public bool HasThirdCover => _thirdCover.Length > 0;
    public bool HasFourthCover => _fourthCover.Length > 0;
    public bool HasFifthCover => _fifthCover.Length > 0;
    public bool HasNoCovers => !HasFirstCover;

    public void SetCovers(IReadOnlyList<string> covers)
    {
        _firstCover = covers.ElementAtOrDefault(0) ?? string.Empty;
        _secondCover = covers.ElementAtOrDefault(1) ?? string.Empty;
        _thirdCover = covers.ElementAtOrDefault(2) ?? string.Empty;
        _fourthCover = covers.ElementAtOrDefault(3) ?? string.Empty;
        _fifthCover = covers.ElementAtOrDefault(4) ?? string.Empty;
        OnPropertyChanged(nameof(FirstCover));
        OnPropertyChanged(nameof(SecondCover));
        OnPropertyChanged(nameof(ThirdCover));
        OnPropertyChanged(nameof(FourthCover));
        OnPropertyChanged(nameof(FifthCover));
        OnPropertyChanged(nameof(HasFirstCover));
        OnPropertyChanged(nameof(HasSecondCover));
        OnPropertyChanged(nameof(HasThirdCover));
        OnPropertyChanged(nameof(HasFourthCover));
        OnPropertyChanged(nameof(HasFifthCover));
        OnPropertyChanged(nameof(HasNoCovers));
    }
}

public partial class CollectionsViewModel : BaseViewModel
{
    private readonly ICollectionExplorerService _explorer;
    private readonly IAuthService _auth;
    private readonly UiThumbnailCache _thumbnailCache;
    private IReadOnlyList<CollectionExplorerDto> _allCollections = [];
    private string _searchText = string.Empty;
    private bool _isOptionsVisible;
    private bool _hasLoaded;
    private bool _suppressFiltering;
    private Task? _loadingTask;
    private LocalizedOption<MediaType?>? _selectedMediaTypeFilter;
    private LocalizedOption<CollectionSortOption>? _selectedSortOption;

    public CollectionsViewModel(
        ICollectionExplorerService explorer,
        IAuthService auth,
        UiThumbnailCache thumbnailCache,
        ILocalizationService localization)
        : base(localization)
    {
        _explorer = explorer;
        _auth = auth;
        _thumbnailCache = thumbnailCache;
        IsBusy = true;
        ReloadOptions();
    }

    private ObservableCollection<CollectionCardViewModel> _collections = [];
    public ObservableCollection<CollectionCardViewModel> Collections
    {
        get => _collections;
        private set => SetProperty(ref _collections, value);
    }
    public ObservableCollection<LocalizedOption<MediaType?>> MediaTypeFilters { get; } = [];
    public ObservableCollection<LocalizedOption<CollectionSortOption>> SortOptions { get; } = [];

    public string PageTitle => T("Collections.Title");
    public string EmptyText => T("Collections.Empty");
    public string SearchPlaceholder => T("Collections.SearchPlaceholder");
    public string LoadingText => T("Collections.Loading");
    public string OptionsText => T("Collections.Options");
    public string CategoryFilterLabel => T("Collections.CategoryFilter");
    public string SortLabel => T("Collections.SortLabel");
    public string ResultsText => string.Format(T("Collections.ResultsFormat"), Collections.Count);

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value)) ApplyFilter();
        }
    }

    public bool IsOptionsVisible
    {
        get => _isOptionsVisible;
        set => SetProperty(ref _isOptionsVisible, value);
    }

    public LocalizedOption<MediaType?>? SelectedMediaTypeFilter
    {
        get => _selectedMediaTypeFilter;
        set
        {
            if (!SetProperty(ref _selectedMediaTypeFilter, value)) return;
            foreach (var option in MediaTypeFilters)
            {
                option.IsSelected = ReferenceEquals(option, value);
            }
            if (!_suppressFiltering) ApplyFilter();
        }
    }

    public LocalizedOption<CollectionSortOption>? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value) && !_suppressFiltering) ApplyFilter();
        }
    }

    public Task LoadAsync()
    {
        if (_hasLoaded) return Task.CompletedTask;
        return _loadingTask ??= LoadCoreAsync();
    }

    private async Task LoadCoreAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _auth.GetCurrentUserIdAsync() ?? "local-user";
            _allCollections = await _explorer.GetAllAsync(userId);
            _hasLoaded = true;
            ApplyFilter();
            _ = WarmCoverStacksAsync();
        }
        finally
        {
            IsBusy = false;
            _loadingTask = null;
        }
    }

    [RelayCommand]
    private void ToggleOptions() => IsOptionsVisible = !IsOptionsVisible;

    [RelayCommand]
    private void SelectMediaTypeFilter(LocalizedOption<MediaType?> option) => SelectedMediaTypeFilter = option;

    [RelayCommand]
    private Task OpenCollectionAsync(CollectionCardViewModel collection) => AppNavigation.OpenCollectionAsync(collection.Id);

    private void ApplyFilter()
    {
        if (_suppressFiltering) return;

        var term = SearchText.Trim();
        var mediaType = SelectedMediaTypeFilter?.Value;
        IEnumerable<CollectionExplorerDto> filtered = _allCollections;

        if (mediaType.HasValue)
        {
            filtered = filtered.Where(collection => collection.Items.Any(item => item.MediaType == mediaType.Value));
        }

        if (term.Length > 0)
        {
            filtered = filtered.Where(collection =>
                collection.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                collection.Items.Any(item => item.Title.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        filtered = SelectedSortOption?.Value switch
        {
            CollectionSortOption.NameDescending => filtered.OrderByDescending(collection => collection.Name, StringComparer.OrdinalIgnoreCase),
            CollectionSortOption.MostItems => filtered.OrderByDescending(collection => VisibleItemCount(collection, mediaType)).ThenBy(collection => collection.Name, StringComparer.OrdinalIgnoreCase),
            CollectionSortOption.FewestItems => filtered.OrderBy(collection => VisibleItemCount(collection, mediaType)).ThenBy(collection => collection.Name, StringComparer.OrdinalIgnoreCase),
            CollectionSortOption.Category => filtered.OrderBy(collection => CategorySortKey(collection, mediaType)).ThenBy(collection => collection.Name, StringComparer.OrdinalIgnoreCase),
            CollectionSortOption.Kind => filtered.OrderBy(collection => collection.Kind).ThenBy(collection => collection.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(collection => collection.Name, StringComparer.OrdinalIgnoreCase)
        };

        Collections = new ObservableCollection<CollectionCardViewModel>(filtered.Select(collection => CreateCard(collection, mediaType)));

        OnPropertyChanged(nameof(ResultsText));
    }

    private CollectionCardViewModel CreateCard(CollectionExplorerDto collection, MediaType? mediaType)
    {
        var visibleItems = VisibleItems(collection, mediaType);
        var covers = visibleItems
            .Where(item => MediaPresentation.HasValidCoverUrl(item.CoverUrl))
            .Select(item => ResolveCoverSource(item.CoverUrl!))
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToList();
        var categories = string.Join("  •  ", visibleItems
            .GroupBy(item => item.MediaType)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => CategoryOrder(group.Key))
            .Take(3)
            .Select(group => $"{T($"MediaType.{group.Key}")} {group.Count()}"));

        return new CollectionCardViewModel(
            collection.Id,
            collection.Name,
            T($"CollectionKind.{collection.Kind}"),
            visibleItems.Count,
            string.Format(T("Collections.ItemCountFormat"), visibleItems.Count),
            categories,
            covers);
    }

    private async Task WarmCoverStacksAsync()
    {
        var sources = _allCollections
            .SelectMany(collection => collection.Items
                .Where(item => MediaPresentation.HasValidCoverUrl(item.CoverUrl))
                .Take(5)
                .Select(item => item.CoverUrl));
        await _thumbnailCache.WarmBatchAsync(sources, 120, degreeOfParallelism: 3);
        MainThread.BeginInvokeOnMainThread(RefreshVisibleCoverStacks);
    }

    private void RefreshVisibleCoverStacks()
    {
        var mediaType = SelectedMediaTypeFilter?.Value;
        var sourceById = _allCollections.ToDictionary(collection => collection.Id);
        foreach (var card in Collections)
        {
            if (!sourceById.TryGetValue(card.Id, out var collection)) continue;
            var covers = VisibleItems(collection, mediaType)
                .Where(item => MediaPresentation.HasValidCoverUrl(item.CoverUrl))
                .Select(item => ResolveCoverSource(item.CoverUrl!))
                .Where(source => !string.IsNullOrWhiteSpace(source))
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToList();
            card.SetCovers(covers);
        }
        OnPropertyChanged(nameof(Collections));
    }

    private string ResolveCoverSource(string originalSource)
    {
        var thumbnail = _thumbnailCache.GetDisplaySource(originalSource);
        // A local thumbnail is created asynchronously. Keep the original file
        // visible until it is ready instead of rendering an empty cover stack.
        return string.IsNullOrWhiteSpace(thumbnail) ? originalSource : thumbnail;
    }

    private static IReadOnlyList<CollectionItemDto> VisibleItems(CollectionExplorerDto collection, MediaType? mediaType) =>
        mediaType.HasValue
            ? collection.Items.Where(item => item.MediaType == mediaType.Value).ToList()
            : collection.Items;

    private static int VisibleItemCount(CollectionExplorerDto collection, MediaType? mediaType) =>
        mediaType.HasValue ? collection.Items.Count(item => item.MediaType == mediaType.Value) : collection.Items.Count;

    private static int CategorySortKey(CollectionExplorerDto collection, MediaType? mediaType) =>
        VisibleItems(collection, mediaType).Select(item => CategoryOrder(item.MediaType)).DefaultIfEmpty(int.MaxValue).Min();

    private static int CategoryOrder(MediaType mediaType)
    {
        for (var index = 0; index < MediaPresentation.OrderedMediaTypes.Count; index++)
        {
            if (MediaPresentation.OrderedMediaTypes[index] == mediaType) return index;
        }
        return int.MaxValue;
    }

    private void ReloadOptions()
    {
        var selectedMediaType = SelectedMediaTypeFilter?.Value;
        var selectedSort = SelectedSortOption?.Value ?? CollectionSortOption.NameAscending;
        _suppressFiltering = true;
        try
        {
            MediaTypeFilters.Clear();
            MediaTypeFilters.Add(new LocalizedOption<MediaType?>(null, T("Common.All")));
            foreach (var mediaType in MediaPresentation.OrderedMediaTypes)
            {
                MediaTypeFilters.Add(new LocalizedOption<MediaType?>(mediaType, T($"MediaType.{mediaType}")));
            }

            SortOptions.Clear();
            foreach (var sortOption in Enum.GetValues<CollectionSortOption>())
            {
                SortOptions.Add(new LocalizedOption<CollectionSortOption>(sortOption, T($"Collections.Sort.{sortOption}")));
            }

            _selectedMediaTypeFilter = null;
            _selectedSortOption = null;
            OnPropertyChanged(nameof(SelectedMediaTypeFilter));
            OnPropertyChanged(nameof(SelectedSortOption));
            SelectedMediaTypeFilter = MediaTypeFilters.FirstOrDefault(option => option.Value == selectedMediaType) ?? MediaTypeFilters[0];
            SelectedSortOption = SortOptions.First(option => option.Value == selectedSort);
        }
        finally
        {
            _suppressFiltering = false;
        }
    }

    protected override void RefreshLocalizedProperties()
    {
        ReloadOptions();
        ApplyFilter();
        base.RefreshLocalizedProperties();
    }
}
