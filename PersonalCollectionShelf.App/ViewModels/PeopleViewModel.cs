using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record PersonCardViewModel(
    Guid Id,
    string Name,
    string PhotoPath,
    bool HasPhoto,
    bool HasDefaultPhoto,
    string Initial,
    string Subtitle,
    string RoleSummary,
    IReadOnlyList<PersonWorkDto> TopWorks,
    string WorkCountText,
    string AverageRatingText,
    bool HasRating);

public partial class PeopleViewModel : BaseViewModel
{
    private const string ViewPreferenceKey = "people.viewMode";
    private readonly IPeopleManagementService _people;
    private readonly IAuthService _auth;
    private IReadOnlyList<PersonCatalogDto> _catalog = [];
    private string _searchText = string.Empty;
    private bool _isSearchVisible;
    private bool _isGridView = Microsoft.Maui.Storage.Preferences.Get(ViewPreferenceKey, "grid") != "list";
    private LocalizedOption<PeopleSortOption>? _selectedSortOption;

    public PeopleViewModel(IPeopleManagementService people, IAuthService auth, ILocalizationService localization)
        : base(localization)
    {
        _people = people;
        _auth = auth;
        ReloadSortOptions();
    }

    public ObservableCollection<PersonCardViewModel> People { get; } = [];
    public ObservableCollection<LocalizedOption<PeopleSortOption>> SortOptions { get; } = [];

    public string PageTitle => T("People.Title");
    public string Subtitle => T("People.CatalogSubtitle");
    public string NewPersonText => T("People.New");
    public string EmptyText => T("People.Empty");
    public string SearchPlaceholder => T("People.SearchPlaceholder");
    public string ResultsText => string.Format(T("People.ResultsFormat"), People.Count);

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
            _catalog = await _people.GetCatalogAsync(userId);
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

        People.Clear();
        foreach (var person in filtered)
        {
            var hasPhoto = IsUsablePhoto(person.PhotoPath);
            var roles = person.TopWorks.Select(work => T($"ContributionRole.{work.Role}"))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(3);
            People.Add(new PersonCardViewModel(
                person.Id,
                person.Name,
                hasPhoto ? person.PhotoPath! : string.Empty,
                hasPhoto,
                !hasPhoto,
                string.IsNullOrWhiteSpace(person.Name) ? "?" : char.ToUpperInvariant(person.Name[0]).ToString(),
                person.Tagline ?? person.Country ?? T("People.NoDescription"),
                string.Join("  •  ", roles),
                person.TopWorks,
                string.Format(T("People.WorkCountFormat"), person.WorkCount),
                person.AverageRating?.ToString("0.0", CultureInfo.CurrentCulture) ?? string.Empty,
                person.AverageRating.HasValue));
        }
        OnPropertyChanged(nameof(ResultsText));
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
        (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" || File.Exists(path));
}
