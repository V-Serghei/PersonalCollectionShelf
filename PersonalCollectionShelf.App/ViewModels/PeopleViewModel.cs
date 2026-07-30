using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed class PersonCardViewModel : ObservableObject
{
    private string _photoPath;
    private bool _hasPhoto;
    private bool _hasDefaultPhoto;
    private bool _photoNotificationPending;

    public PersonCardViewModel(
        Guid id,
        string name,
        string originalPhotoPath,
        string photoPath,
        bool hasPhoto,
        bool hasDefaultPhoto,
        string initial,
        string subtitle,
        string roleSummary,
        IReadOnlyList<PersonWorkDto> topWorks,
        string topWorksSummary,
        string workCountText,
        string averageRatingText,
        bool hasRating)
    {
        Id = id;
        Name = name;
        OriginalPhotoPath = originalPhotoPath;
        _photoPath = photoPath;
        _hasPhoto = hasPhoto;
        _hasDefaultPhoto = hasDefaultPhoto;
        Initial = initial;
        Subtitle = subtitle;
        RoleSummary = roleSummary;
        TopWorks = topWorks;
        TopWorksSummary = topWorksSummary;
        WorkCountText = workCountText;
        AverageRatingText = averageRatingText;
        HasRating = hasRating;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string OriginalPhotoPath { get; }
    public string PhotoPath => _photoPath;
    public bool HasPhoto => _hasPhoto;
    public bool HasDefaultPhoto => _hasDefaultPhoto;
    public string Initial { get; }
    public string Subtitle { get; }
    public string RoleSummary { get; }
    public IReadOnlyList<PersonWorkDto> TopWorks { get; }
    public string TopWorksSummary { get; }
    public string WorkCountText { get; }
    public string AverageRatingText { get; }
    public bool HasRating { get; }

    public void SetPhotoSource(string source, bool notify = true)
    {
        if (string.Equals(_photoPath, source, StringComparison.Ordinal))
        {
            if (notify && _photoNotificationPending) NotifyPhotoChanged();
            return;
        }
        _photoPath = source;
        _hasPhoto = !string.IsNullOrWhiteSpace(source);
        _hasDefaultPhoto = !_hasPhoto;
        _photoNotificationPending = !notify;
        if (notify) NotifyPhotoChanged();
    }

    private void NotifyPhotoChanged()
    {
        _photoNotificationPending = false;
        OnPropertyChanged(nameof(PhotoPath));
        OnPropertyChanged(nameof(HasPhoto));
        OnPropertyChanged(nameof(HasDefaultPhoto));
    }

    public bool HasSameContent(PersonCardViewModel other) =>
        Id == other.Id &&
        Name == other.Name &&
        Subtitle == other.Subtitle &&
        RoleSummary == other.RoleSummary &&
        TopWorksSummary == other.TopWorksSummary &&
        PhotoPath == other.PhotoPath &&
        WorkCountText == other.WorkCountText &&
        AverageRatingText == other.AverageRatingText;
}

public partial class PeopleViewModel : BaseViewModel
{
    private const string GridColumnCountPreferenceKey = "people.gridColumnCount";
    private const string ViewPreferenceKey = "people.viewMode";
    private const int ThumbnailBatchSize = 500;
    private const int ThumbnailBatchLead = 400;
    private readonly IPeopleManagementService _people;
    private readonly IAuthService _auth;
    private readonly UiThumbnailCache _thumbnailCache;
    private readonly Dictionary<Guid, string> _photoSources = [];
    private int _thumbnailViewportVersion;
    private IReadOnlyList<PersonCatalogDto> _catalog = [];
    private IReadOnlyList<PersonCatalogDto> _preparedPeople = [];
    private int _filteredCount;
    private string _searchText = string.Empty;
    private bool _isSearchVisible;
    private bool _isGridView = Microsoft.Maui.Storage.Preferences.Get(ViewPreferenceKey, "grid") != "list";
    private LocalizedOption<PeopleSortOption>? _selectedSortOption;
    private bool _initialThumbnailBatchPrepared;
    private readonly object _thumbnailBatchLock = new();
    private int _nextThumbnailBatchStart;
    private int _thumbnailOrderingVersion;

    public PeopleViewModel(
        IPeopleManagementService people,
        IAuthService auth,
        UiThumbnailCache thumbnailCache,
        ILocalizationService localization)
        : base(localization)
    {
        _people = people;
        _auth = auth;
        _thumbnailCache = thumbnailCache;
        ReloadSortOptions();
    }

    private ObservableCollection<PersonCardViewModel> _peopleCards = [];

    public ObservableCollection<PersonCardViewModel> People
    {
        get => _peopleCards;
        private set => SetProperty(ref _peopleCards, value);
    }
    public ObservableCollection<LocalizedOption<PeopleSortOption>> SortOptions { get; } = [];

    public string PageTitle => T("People.Title");
    public string Subtitle => T("People.CatalogSubtitle");
    public string NewPersonText => T("People.New");
    public string EmptyText => T("People.Empty");
    public string SearchPlaceholder => T("People.SearchPlaceholder");
    public string ResultsText => string.Format(T("People.ResultsFormat"), _filteredCount);

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

    public bool IsGridView
    {
        get => _isGridView;
        private set
        {
            if (!SetProperty(ref _isGridView, value)) return;
            OnPropertyChanged(nameof(IsListView));
            Microsoft.Maui.Storage.Preferences.Set(ViewPreferenceKey, value ? "grid" : "list");
        }
    }

    public bool IsListView => !IsGridView;

    public int GridColumnCount => Math.Clamp(
        Microsoft.Maui.Storage.Preferences.Get(GridColumnCountPreferenceKey, 3), 1, 4);

    public void RefreshDisplayPreferences() => OnPropertyChanged(nameof(GridColumnCount));

    public LocalizedOption<PeopleSortOption>? SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value)) ApplyFilter();
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var userId = await _auth.GetCurrentUserIdAsync() ?? "local-user";
            var catalog = await _people.GetCatalogAsync(userId);
            _catalog = catalog;
            ApplyFilter();
            StartInitialThumbnailPipeline();
            _ = _thumbnailCache.PrimeDisplaySourcesAsync(catalog.Select(person => person.PhotoPath));
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
    private void SetGridView() => IsGridView = true;

    [RelayCommand]
    private void SetListView() => IsGridView = false;

    [RelayCommand]
    private Task NewPersonAsync() => AppNavigation.OpenPersonEditorAsync();

    [RelayCommand]
    private Task OpenPersonAsync(PersonCardViewModel person) => AppNavigation.OpenPersonAsync(person.Id);

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        IEnumerable<PersonCatalogDto> filtered = _catalog;
        if (term.Length > 0)
        {
            filtered = filtered.Where(person =>
                person.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(person.Tagline) && person.Tagline.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(person.Country) && person.Country.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                person.TopWorks.Any(work => work.Title.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        filtered = SelectedSortOption?.Value switch
        {
            PeopleSortOption.NameDescending => filtered.OrderByDescending(person => person.Name, StringComparer.OrdinalIgnoreCase),
            PeopleSortOption.HighestRated => filtered.OrderByDescending(person => person.AverageRating ?? -1).ThenBy(person => person.Name, StringComparer.OrdinalIgnoreCase),
            PeopleSortOption.MostWorks => filtered.OrderByDescending(person => person.WorkCount).ThenBy(person => person.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(person => person.Name, StringComparer.OrdinalIgnoreCase)
        };

        _preparedPeople = filtered.ToArray();
        Interlocked.Increment(ref _thumbnailOrderingVersion);
        lock (_thumbnailBatchLock)
        {
            _nextThumbnailBatchStart = _initialThumbnailBatchPrepared
                ? Math.Min(ThumbnailBatchSize, _preparedPeople.Count)
                : 0;
        }
        _filteredCount = _preparedPeople.Count;
        _photoSources.Clear();
        foreach (var person in _preparedPeople.Where(person => IsUsablePhoto(person.PhotoPath)))
        {
            _photoSources[person.Id] = person.PhotoPath!;
        }

        var canRetainVisibleCards = People.Count == _preparedPeople.Count &&
            People.Select(person => person.Id).SequenceEqual(_preparedPeople.Select(person => person.Id));

        if (canRetainVisibleCards)
        {
            for (var index = 0; index < People.Count; index++)
            {
                var updated = ToCard(_preparedPeople[index]);
                if (!People[index].HasSameContent(updated)) People[index] = updated;
            }
        }
        else
        {
            People = new ObservableCollection<PersonCardViewModel>(_preparedPeople.Select(ToCard));
        }
        OnPropertyChanged(nameof(ResultsText));
        PreloadThumbnails(0, Math.Min(30, People.Count - 1));
    }

    private PersonCardViewModel ToCard(PersonCatalogDto person)
    {
        var displayPhoto = _thumbnailCache.GetDisplaySource(person.PhotoPath);
        var hasPhoto = !string.IsNullOrWhiteSpace(displayPhoto);
        var roles = person.TopWorks.Select(work => T($"ContributionRole.{work.Role}"))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(3);
        return new PersonCardViewModel(
            person.Id,
            person.Name,
            person.PhotoPath ?? string.Empty,
            hasPhoto ? displayPhoto : string.Empty,
            hasPhoto,
            !hasPhoto,
            string.IsNullOrWhiteSpace(person.Name) ? "?" : char.ToUpperInvariant(person.Name[0]).ToString(),
            person.Tagline ?? person.Country ?? T("People.NoDescription"),
            string.Join("  •  ", roles),
            person.TopWorks,
            string.Join("  •  ", person.TopWorks.Take(3).Select(work =>
                work.Rating.HasValue ? $"{work.Title} ★ {work.Rating:0.#}" : work.Title)),
            string.Format(T("People.WorkCountFormat"), person.WorkCount),
            person.AverageRating?.ToString("0.0", CultureInfo.CurrentCulture) ?? string.Empty,
            person.AverageRating.HasValue);
    }

    public void PreloadThumbnails(int firstVisibleIndex, int lastVisibleIndex)
    {
        if (People.Count == 0 || lastVisibleIndex < 0) return;
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
                if (_nextThumbnailBatchStart >= _preparedPeople.Count ||
                    lastVisibleIndex < Math.Max(0, _nextThumbnailBatchStart - ThumbnailBatchLead))
                {
                    return;
                }

                var start = _nextThumbnailBatchStart;
                var count = Math.Min(ThumbnailBatchSize, _preparedPeople.Count - start);
                _nextThumbnailBatchStart += count;
                version = Volatile.Read(ref _thumbnailOrderingVersion);
                batch = _preparedPeople
                    .Skip(start)
                    .Take(count)
                    .Select(person => (person.Id, _photoSources.GetValueOrDefault(person.Id) ?? string.Empty))
                    .Where(item => !string.IsNullOrWhiteSpace(item.Item2))
                    .ToArray();
            }

            _ = WarmThumbnailBatchAsync(batch, version);
        }
    }

    private void StartInitialThumbnailPipeline()
    {
        if (_initialThumbnailBatchPrepared || _preparedPeople.Count == 0) return;
        _initialThumbnailBatchPrepared = true;
        var version = Volatile.Read(ref _thumbnailOrderingVersion);
        var firstBatch = _preparedPeople
            .Take(ThumbnailBatchSize)
            .Select(person => (person.Id, _photoSources.GetValueOrDefault(person.Id) ?? string.Empty))
            .Where(item => !string.IsNullOrWhiteSpace(item.Item2))
            .ToArray();
        lock (_thumbnailBatchLock)
        {
            _nextThumbnailBatchStart = Math.Min(ThumbnailBatchSize, _preparedPeople.Count);
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
            var cards = People.ToDictionary(person => person.Id);
            foreach (var (id, source) in batch)
            {
                if (!cards.TryGetValue(id, out var card)) continue;
                var displaySource = _thumbnailCache.GetDisplaySource(source);
                if (!string.IsNullOrWhiteSpace(displaySource)) card.SetPhotoSource(displaySource, notify: false);
            }
        });
    }

    private async Task PreloadThumbnailRangeAsync(int firstVisibleIndex, int lastVisibleIndex, int version)
    {
        var start = Math.Max(0, firstVisibleIndex - 30);
        var end = Math.Min(People.Count - 1, lastVisibleIndex + 30);
        var center = Math.Clamp((firstVisibleIndex + lastVisibleIndex) / 2, start, end);
        var indices = Enumerable.Range(start, end - start + 1)
            .OrderBy(index => Math.Abs(index - center));
        var ready = new List<(int Index, Guid Id, string Source)>();

        foreach (var index in indices)
        {
            if (version != Volatile.Read(ref _thumbnailViewportVersion)) return;
            if (index >= People.Count) return;
            var personId = People[index].Id;
            if (!_photoSources.TryGetValue(personId, out var source)) continue;
            var thumbnail = await _thumbnailCache.GetOrCreateAsync(source).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(thumbnail)) continue;
            ready.Add((index, personId, thumbnail));
        }


        if (version != Volatile.Read(ref _thumbnailViewportVersion) || ready.Count == 0) return;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (version != Volatile.Read(ref _thumbnailViewportVersion)) return;
            foreach (var item in ready)
            {
                if (item.Index >= People.Count || People[item.Index].Id != item.Id) continue;
                var isVisible = item.Index >= firstVisibleIndex && item.Index <= lastVisibleIndex;
                People[item.Index].SetPhotoSource(item.Source, notify: isVisible);
            }
        });
    }

    private void ReloadSortOptions()
    {
        var selected = SelectedSortOption?.Value ?? PeopleSortOption.NameAscending;
        SortOptions.Clear();
        foreach (var option in Enum.GetValues<PeopleSortOption>())
        {
            SortOptions.Add(new LocalizedOption<PeopleSortOption>(option, T($"People.Sort.{option}")));
        }
        SelectedSortOption = SortOptions.First(option => option.Value == selected);
    }

    protected override void RefreshLocalizedProperties()
    {
        ReloadSortOptions();
        base.RefreshLocalizedProperties();
    }

    private static bool IsUsablePhoto(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
            (uri.Scheme is "http" or "https" || uri.IsFile && File.Exists(uri.LocalPath)) || File.Exists(path));
}
