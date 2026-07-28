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
    string CategorySummary);

public partial class TagsViewModel : BaseViewModel
{
    private readonly IMediaItemService _mediaItems;
    private readonly IAuthService _auth;
    private IReadOnlyList<TagSourceGroup> _allTags = [];
    private string _searchText = string.Empty;
    private bool _isSearchVisible;
    private LocalizedOption<MediaType?>? _selectedMediaTypeFilter;
    private LocalizedOption<TagSortOption>? _selectedSortOption;

    public TagsViewModel(
        IMediaItemService mediaItems,
        IAuthService auth,
        ILocalizationService localization)
        : base(localization)
    {
        _mediaItems = mediaItems;
        _auth = auth;
        ReloadFilterOptions();
    }

    public ObservableCollection<TagCardViewModel> Tags { get; } = [];
    public ObservableCollection<LocalizedOption<MediaType?>> MediaTypeFilters { get; } = [];
    public ObservableCollection<LocalizedOption<TagSortOption>> SortOptions { get; } = [];

    public string PageTitle => T("Tags.Title");
    public string Subtitle => T("Tags.Subtitle");
    public string EmptyText => T("Tags.Empty");
    public string SearchPlaceholder => T("Tags.SearchPlaceholder");
    public string SortPlaceholder => T("Tags.SortPlaceholder");
    public string ResultsText => string.Format(T("Tags.ResultsFormat"), Tags.Count);

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

    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set => SetProperty(ref _isSearchVisible, value);
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
            ApplyFilter();
        }
    }

    public LocalizedOption<TagSortOption>? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                ApplyFilter();
            }
        }
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var userId = await _auth.GetCurrentUserIdAsync() ?? "local-user";
            var library = await _mediaItems.GetLibraryAsync(userId);
            _allTags = library
                .SelectMany(item => item.TagNames.Select(tag => (Name: tag.Trim(), Item: item)))
                .Where(value => value.Name.Length > 0)
                .GroupBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TagSourceGroup(
                    group.First().Name,
                    group.Select(value => value.Item).DistinctBy(item => item.Id).ToList()))
                .ToList();
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        if (!IsSearchVisible) SearchText = string.Empty;
    }

    [RelayCommand]
    private void SelectMediaTypeFilter(LocalizedOption<MediaType?> option)
    {
        SelectedMediaTypeFilter = option;
    }

    [RelayCommand]
    private Task OpenTagAsync(TagCardViewModel tag) => AppNavigation.OpenTagAsync(tag.Name);

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

        Tags.Clear();
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

            Tags.Add(new TagCardViewModel(
                group.Name,
                visibleItems.Count,
                string.Format(T("Tags.ItemCountFormat"), visibleItems.Count),
                categorySummary));
        }

        OnPropertyChanged(nameof(ResultsText));
    }

    private static int CountVisibleItems(TagSourceGroup group, MediaType? mediaType) =>
        mediaType.HasValue ? group.Items.Count(item => item.MediaType == mediaType.Value) : group.Items.Count;

    private void ReloadFilterOptions()
    {
        var selectedMediaType = SelectedMediaTypeFilter?.Value;
        var selectedSort = SelectedSortOption?.Value ?? TagSortOption.NameAscending;

        MediaTypeFilters.Clear();
        MediaTypeFilters.Add(new LocalizedOption<MediaType?>(null, T("Common.All")));
        foreach (var mediaType in Enum.GetValues<MediaType>())
        {
            MediaTypeFilters.Add(new LocalizedOption<MediaType?>(mediaType, T($"MediaType.{mediaType}")));
        }

        SortOptions.Clear();
        foreach (var sortOption in Enum.GetValues<TagSortOption>())
        {
            SortOptions.Add(new LocalizedOption<TagSortOption>(sortOption, T($"Tags.Sort.{sortOption}")));
        }

        SelectedMediaTypeFilter = MediaTypeFilters.FirstOrDefault(option => option.Value == selectedMediaType) ?? MediaTypeFilters[0];
        SelectedSortOption = SortOptions.First(option => option.Value == selectedSort);
    }

    protected override void RefreshLocalizedProperties()
    {
        ReloadFilterOptions();
        base.RefreshLocalizedProperties();
    }

    private sealed record TagSourceGroup(string Name, IReadOnlyList<MediaItemDto> Items);
}
