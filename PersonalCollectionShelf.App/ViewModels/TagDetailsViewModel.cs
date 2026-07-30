using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record TagMediaItemViewModel(
    Guid Id,
    string Title,
    string MediaTypeLabel,
    Color MediaTypeColor,
    string StatusLabel,
    Color StatusForegroundColor,
    Color StatusBackgroundColor,
    string CategoryLine,
    string TagsLine,
    string RatingText,
    bool HasRating,
    string ReleaseYearText,
    string CoverUrl,
    bool HasCoverUrl,
    bool HasNoCoverUrl,
    string Initial);

public partial class TagDetailsViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IMediaItemService _mediaItems;
    private readonly IAuthService _auth;
    private IReadOnlyList<MediaItemDto> _tagItems = [];
    private string _tagName = string.Empty;
    private string _searchText = string.Empty;
    private bool _isSearchVisible;
    private LocalizedOption<MediaType?>? _selectedMediaTypeFilter;
    private LocalizedOption<LibrarySortOption>? _selectedSortOption;
    private readonly bool _isGenreMode;

    public TagDetailsViewModel(
        IMediaItemService mediaItems,
        IAuthService auth,
        ILocalizationService localization)
        : this(mediaItems, auth, localization, isGenreMode: false)
    {
    }

    protected TagDetailsViewModel(
        IMediaItemService mediaItems,
        IAuthService auth,
        ILocalizationService localization,
        bool isGenreMode)
        : base(localization)
    {
        _mediaItems = mediaItems;
        _auth = auth;
        _isGenreMode = isGenreMode;
        ReloadFilterOptions();
    }

    private ObservableCollection<TagMediaItemViewModel> _items = [];
    public ObservableCollection<TagMediaItemViewModel> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }
    public ObservableCollection<LocalizedOption<MediaType?>> MediaTypeFilters { get; } = [];
    public ObservableCollection<LocalizedOption<LibrarySortOption>> SortOptions { get; } = [];

    public string TagName
    {
        get => _tagName;
        private set
        {
            if (!SetProperty(ref _tagName, value)) return;
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(TagBadgeText));
        }
    }

    private string LocalizationPrefix => _isGenreMode ? "Genres" : "Tags";
    public string PageTitle => string.IsNullOrWhiteSpace(TagName) ? T($"{LocalizationPrefix}.DetailsTitle") : TagName;
    public string TagBadgeText => _isGenreMode ? TagName : $"# {TagName}";
    public string Subtitle => T($"{LocalizationPrefix}.DetailsSubtitle");
    public string EmptyText => T($"{LocalizationPrefix}.DetailsEmpty");
    public string SearchPlaceholder => T($"{LocalizationPrefix}.DetailsSearchPlaceholder");
    public string ResultsText => string.Format(T($"{LocalizationPrefix}.ItemsResultFormat"), Items.Count);

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value)) ApplyFilter();
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

    public LocalizedOption<LibrarySortOption>? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value)) ApplyFilter();
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var queryKey = _isGenreMode ? "genre" : "tag";
        if (query.TryGetValue(queryKey, out var rawTag))
        {
            TagName = Uri.UnescapeDataString(rawTag?.ToString() ?? string.Empty).Trim();
        }
    }

    public async Task LoadAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(TagName)) return;
        IsBusy = true;
        try
        {
            var userId = await _auth.GetCurrentUserIdAsync() ?? "local-user";
            var library = await _mediaItems.GetLibraryAsync(userId);
            _tagItems = library
                .Where(item => (_isGenreMode ? item.Genres : item.TagNames)
                    .Any(value => string.Equals(value, TagName, StringComparison.OrdinalIgnoreCase)))
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
    private Task OpenItemAsync(TagMediaItemViewModel item) => AppNavigation.OpenMediaDetailsAsync(item.Id);

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        IEnumerable<MediaItemDto> filtered = _tagItems;

        if (SelectedMediaTypeFilter?.Value is { } mediaType)
        {
            filtered = filtered.Where(item => item.MediaType == mediaType);
        }

        if (term.Length > 0)
        {
            filtered = filtered.Where(item =>
                item.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(item.OriginalTitle) && item.OriginalTitle.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(item.Creator) && item.Creator.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(item.Category) && item.Category.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                item.TagNames.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                item.Genres.Any(genre => genre.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        filtered = SelectedSortOption?.Value switch
        {
            LibrarySortOption.TitleAsc => filtered.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            LibrarySortOption.TitleDesc => filtered.OrderByDescending(item => item.Title, StringComparer.OrdinalIgnoreCase),
            LibrarySortOption.DateAddedOldest => filtered.OrderBy(item => item.CreatedAt),
            LibrarySortOption.RatingHighest => filtered.OrderByDescending(item => item.Rating ?? -1),
            LibrarySortOption.RatingLowest => filtered.OrderBy(item => item.Rating ?? decimal.MaxValue),
            LibrarySortOption.ReleaseYearNewest => filtered.OrderByDescending(item => item.ReleaseYear ?? int.MinValue),
            LibrarySortOption.ReleaseYearOldest => filtered.OrderBy(item => item.ReleaseYear ?? int.MaxValue),
            _ => filtered.OrderByDescending(item => item.CreatedAt)
        };

        Items = new ObservableCollection<TagMediaItemViewModel>(filtered.Select(ToViewModel));
        OnPropertyChanged(nameof(ResultsText));
    }

    private TagMediaItemViewModel ToViewModel(MediaItemDto item)
    {
        var hasCover = MediaPresentation.HasValidCoverUrl(item.CoverUrl);
        var category = string.IsNullOrWhiteSpace(item.Category)
            ? T("Library.NoCategory")
            : item.Category;
        return new TagMediaItemViewModel(
            item.Id,
            item.Title,
            T($"MediaType.{item.MediaType}"),
            MediaPresentation.GetMediaTypeColor(item.MediaType),
            T($"MediaStatus.{item.Status}"),
            MediaPresentation.GetStatusForegroundColor(item.Status),
            MediaPresentation.GetStatusBackgroundColor(item.Status),
            category,
            string.Join("  •  ", (_isGenreMode ? item.Genres : item.TagNames)
                .Where(value => !string.Equals(value, TagName, StringComparison.OrdinalIgnoreCase))
                .Take(3)),
            item.Rating?.ToString("0.#", CultureInfo.CurrentCulture) ?? string.Empty,
            item.Rating.HasValue,
            item.ReleaseYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            hasCover ? item.CoverUrl! : string.Empty,
            hasCover,
            !hasCover,
            string.IsNullOrWhiteSpace(item.Title) ? "?" : char.ToUpperInvariant(item.Title[0]).ToString());
    }

    private void ReloadFilterOptions()
    {
        var selectedMediaType = SelectedMediaTypeFilter?.Value;
        var selectedSort = SelectedSortOption?.Value ?? LibrarySortOption.DateAddedNewest;

        MediaTypeFilters.Clear();
        MediaTypeFilters.Add(new LocalizedOption<MediaType?>(null, T("Common.All")));
        foreach (var mediaType in MediaPresentation.OrderedMediaTypes)
        {
            MediaTypeFilters.Add(new LocalizedOption<MediaType?>(mediaType, T($"MediaType.{mediaType}")));
        }

        SortOptions.Clear();
        foreach (var sortOption in Enum.GetValues<LibrarySortOption>())
        {
            SortOptions.Add(new LocalizedOption<LibrarySortOption>(sortOption, T($"Library.SortOption.{sortOption}")));
        }

        SelectedMediaTypeFilter = MediaTypeFilters.FirstOrDefault(option => option.Value == selectedMediaType) ?? MediaTypeFilters[0];
        SelectedSortOption = SortOptions.First(option => option.Value == selectedSort);
    }

    protected override void RefreshLocalizedProperties()
    {
        ReloadFilterOptions();
        base.RefreshLocalizedProperties();
    }
}

public sealed class GenreDetailsViewModel : TagDetailsViewModel
{
    public GenreDetailsViewModel(
        IMediaItemService mediaItems,
        IAuthService auth,
        ILocalizationService localization)
        : base(mediaItems, auth, localization, isGenreMode: true)
    {
    }
}
