using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record TagCardViewModel(
    string Name,
    int ItemCount,
    string ItemCountText,
    string CategorySummary,
    string Icon);

public partial class TagsViewModel : BaseViewModel
{
    private readonly IMediaItemService _mediaItems;
    private readonly IAuthService _auth;
    private IReadOnlyList<TagSourceGroup> _allTags = [];
    private string _searchText = string.Empty;
    private bool _isOptionsVisible;
    private bool _hasLoaded;
    private bool _suppressFiltering;
    private Task? _loadingTask;
    private LocalizedOption<MediaType?>? _selectedMediaTypeFilter;
    private LocalizedOption<TagSortOption>? _selectedSortOption;
    private readonly bool _isGenreMode;

    public TagsViewModel(
        IMediaItemService mediaItems,
        IAuthService auth,
        ILocalizationService localization)
        : this(mediaItems, auth, localization, isGenreMode: false)
    {
    }

    protected TagsViewModel(
        IMediaItemService mediaItems,
        IAuthService auth,
        ILocalizationService localization,
        bool isGenreMode)
        : base(localization)
    {
        _mediaItems = mediaItems;
        _auth = auth;
        _isGenreMode = isGenreMode;
        IsBusy = true;
        ReloadFilterOptions();
    }

    private ObservableCollection<TagCardViewModel> _tags = [];
    public ObservableCollection<TagCardViewModel> Tags
    {
        get => _tags;
        private set => SetProperty(ref _tags, value);
    }
    public ObservableCollection<LocalizedOption<MediaType?>> MediaTypeFilters { get; } = [];
    public ObservableCollection<LocalizedOption<TagSortOption>> SortOptions { get; } = [];

    private string LocalizationPrefix => _isGenreMode ? "Genres" : "Tags";
    public string PageTitle => T($"{LocalizationPrefix}.Title");
    public string Subtitle => T($"{LocalizationPrefix}.Subtitle");
    public string EmptyText => T($"{LocalizationPrefix}.Empty");
    public string SearchPlaceholder => T($"{LocalizationPrefix}.SearchPlaceholder");
    public string SortPlaceholder => T($"{LocalizationPrefix}.SortPlaceholder");
    public string ResultsText => string.Format(T($"{LocalizationPrefix}.ResultsFormat"), Tags.Count);
    public string OptionsText => T($"{LocalizationPrefix}.Options");
    public string CategoryFilterLabel => T($"{LocalizationPrefix}.CategoryFilter");
    public string LoadingText => T($"{LocalizationPrefix}.Loading");

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
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

    public LocalizedOption<TagSortOption>? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value) && !_suppressFiltering)
            {
                ApplyFilter();
            }
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
            var library = await _mediaItems.GetLibraryAsync(userId);
            _allTags = library
                .SelectMany(item => (_isGenreMode ? item.Genres : item.TagNames)
                    .Select(value => (Name: value.Trim(), Item: item)))
                .Where(value => value.Name.Length > 0)
                .GroupBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TagSourceGroup(
                    group.First().Name,
                    group.Select(value => value.Item).DistinctBy(item => item.Id).ToList()))
                .ToList();
            _hasLoaded = true;
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
            _loadingTask = null;
        }
    }

    [RelayCommand]
    private void ToggleOptions()
    {
        IsOptionsVisible = !IsOptionsVisible;
    }

    [RelayCommand]
    private void SelectMediaTypeFilter(LocalizedOption<MediaType?> option)
    {
        SelectedMediaTypeFilter = option;
    }

    [RelayCommand]
    private Task OpenTagAsync(TagCardViewModel tag) => _isGenreMode
        ? AppNavigation.OpenGenreAsync(tag.Name)
        : AppNavigation.OpenTagAsync(tag.Name);

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var mediaType = SelectedMediaTypeFilter?.Value;
        IEnumerable<TagSourceGroup> filtered = _allTags;

        if (mediaType.HasValue)
        {
            filtered = filtered.Where(group => group.Items.Any(item => item.MediaType == mediaType.Value));
        }

        if (term.Length > 0)
        {
            filtered = filtered.Where(group =>
                group.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                group.Items.Any(item =>
                    item.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(item.Category) && item.Category.Contains(term, StringComparison.OrdinalIgnoreCase))));
        }

        filtered = SelectedSortOption?.Value switch
        {
            TagSortOption.NameDescending => filtered.OrderByDescending(group => group.Name, StringComparer.OrdinalIgnoreCase),
            TagSortOption.MostUsed => filtered.OrderByDescending(group => CountVisibleItems(group, mediaType)).ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase),
            TagSortOption.LeastUsed => filtered.OrderBy(group => CountVisibleItems(group, mediaType)).ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
        };

        var cards = new List<TagCardViewModel>();
        foreach (var group in filtered)
        {
            var visibleItems = mediaType.HasValue
                ? group.Items.Where(item => item.MediaType == mediaType.Value).ToList()
                : group.Items;
            var categorySummary = string.Join("  •  ", visibleItems
                .GroupBy(item => item.MediaType)
                .OrderByDescending(category => category.Count())
                .ThenBy(category => category.Key)
                .Take(4)
                .Select(category => $"{T($"MediaType.{category.Key}")} {category.Count()}"));

            cards.Add(new TagCardViewModel(
                group.Name,
                visibleItems.Count,
                string.Format(T($"{LocalizationPrefix}.ItemCountFormat"), visibleItems.Count),
                categorySummary,
                _isGenreMode ? "◈" : "#"));
        }

        Tags = new ObservableCollection<TagCardViewModel>(cards);

        OnPropertyChanged(nameof(ResultsText));
    }

    private static int CountVisibleItems(TagSourceGroup group, MediaType? mediaType) =>
        mediaType.HasValue ? group.Items.Count(item => item.MediaType == mediaType.Value) : group.Items.Count;

    private void ReloadFilterOptions()
    {
        var selectedMediaType = SelectedMediaTypeFilter?.Value;
        var selectedSort = SelectedSortOption?.Value ?? TagSortOption.NameAscending;
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
            foreach (var sortOption in Enum.GetValues<TagSortOption>())
            {
                SortOptions.Add(new LocalizedOption<TagSortOption>(sortOption, T($"{LocalizationPrefix}.Sort.{sortOption}")));
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
        ReloadFilterOptions();
        ApplyFilter();
        base.RefreshLocalizedProperties();
    }

    private sealed record TagSourceGroup(string Name, IReadOnlyList<MediaItemDto> Items);
}

public sealed class GenresViewModel : TagsViewModel
{
    public GenresViewModel(
        IMediaItemService mediaItems,
        IAuthService auth,
        ILocalizationService localization)
        : base(mediaItems, auth, localization, isGenreMode: true)
    {
    }
}
