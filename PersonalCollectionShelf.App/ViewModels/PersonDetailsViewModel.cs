using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record PersonPhotoViewModel(Guid Id, string FilePath, string Caption, bool IsPrimary, string PrimaryText);

public sealed record PersonWorkViewModel(
    Guid MediaItemId,
    string Title,
    string MediaTypeLabel,
    string RoleLabel,
    decimal? Rating);

public sealed record PersonRelationViewModel(
    Guid Id,
    Guid RelatedPersonId,
    string RelatedPersonName,
    string KindLabel);

public partial class PersonDetailsViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IPeopleManagementService _people;
    private readonly IAuthService _auth;
    private Guid _personId;
    private string _userId = "local-user";
    private PersonDetailsDto? _details;
    private IReadOnlyList<PersonWorkDto> _allWorks = [];
    private ContributionRole? _selectedWorkRole;

    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string PhotoPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string Tagline { get; set; } = string.Empty;
    [ObservableProperty] public partial string Biography { get; set; } = string.Empty;
    [ObservableProperty] public partial string Notes { get; set; } = string.Empty;
    [ObservableProperty] public partial string YearsText { get; set; } = string.Empty;
    [ObservableProperty] public partial string LocationText { get; set; } = string.Empty;
    [ObservableProperty] public partial string Website { get; set; } = string.Empty;
    [ObservableProperty] public partial string RoleSummary { get; set; } = string.Empty;
    [ObservableProperty] public partial string CategorySummary { get; set; } = string.Empty;

    public PersonDetailsViewModel(IPeopleManagementService people, IAuthService auth, ILocalizationService localization) : base(localization)
    {
        _people = people;
        _auth = auth;
    }

    public ObservableCollection<string> Professions { get; } = [];
    public ObservableCollection<PersonPhotoViewModel> Photos { get; } = [];
    public ObservableCollection<PersonWorkViewModel> TopWorks { get; } = [];
    public ObservableCollection<LocalizedOption<ContributionRole?>> WorkRoleTabs { get; } = [];
    public ObservableCollection<PersonWorkViewModel> Works { get; } = [];
    public ObservableCollection<PersonRelationViewModel> Relations { get; } = [];

    public string PageTitle => Name;
    public string EditText => T("Common.Edit");
    public string AddPhotoText => T("People.AddPhoto");
    public string GalleryText => T("People.OpenGallery");
    public string AboutTitle => T("People.About");
    public string ProfessionsTitle => T("People.Professions");
    public string TopWorksTitle => T("People.TopWorks");
    public string AllWorksTitle => T("People.AllWorks");
    public string GalleryTitle => T("People.Gallery");
    public string RelationsTitle => T("People.Relations");
    public string CategoriesTitle => T("People.CreativeCategories");
    public bool HasPhoto => IsUsablePhoto(PhotoPath);
    public bool HasDefaultPhoto => !HasPhoto;
    public bool HasPhotos => Photos.Count > 0;
    public bool HasBiography => !string.IsNullOrWhiteSpace(Biography);
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
    public bool HasWebsite => !string.IsNullOrWhiteSpace(Website);
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : char.ToUpperInvariant(Name[0]).ToString();
    public string WorkCountText => string.Format(T("People.WorkCountFormat"), _allWorks.Select(work => work.MediaItemId).Distinct().Count());

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && Guid.TryParse(raw?.ToString(), out var id)) _personId = id;
    }

    public async Task LoadAsync()
    {
        if (_personId == Guid.Empty || IsBusy) return;
        IsBusy = true;
        try
        {
            _userId = await _auth.GetCurrentUserIdAsync() ?? "local-user";
            var person = await _people.GetAsync(_userId, _personId);
            if (person is null) return;
            _details = person;
            Name = person.Name;
            PhotoPath = person.PhotoPath ?? person.Photos.FirstOrDefault(photo => photo.IsPrimary)?.FilePath ?? string.Empty;
            Tagline = person.Tagline ?? string.Empty;
            Biography = person.Description ?? T("People.NoDescription");
            Notes = person.Notes ?? string.Empty;
            YearsText = FormatYears(person.BirthYear, person.DeathYear);
            LocationText = string.Join(" • ", new[] { person.PlaceOfBirth, person.Country }.Where(value => !string.IsNullOrWhiteSpace(value)));
            Website = person.OfficialWebsite ?? string.Empty;
            Replace(Professions, person.Professions);
            PopulateLocalizedDetails(person);
            NotifyCalculated();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task EditAsync() => AppNavigation.OpenPersonEditorAsync(_personId);

    [RelayCommand]
    private Task OpenGalleryAsync() => AppNavigation.OpenPersonGalleryAsync(_personId);

    [RelayCommand]
    private Task OpenWorkAsync(PersonWorkViewModel work) => AppNavigation.OpenMediaDetailsAsync(work.MediaItemId);

    [RelayCommand]
    private void SelectWorkRole(LocalizedOption<ContributionRole?> option)
    {
        _selectedWorkRole = option.Value;
        foreach (var tab in WorkRoleTabs) tab.IsSelected = ReferenceEquals(tab, option);
        ApplyWorkRoleFilter();
    }

    [RelayCommand]
    private Task OpenRelatedPersonAsync(PersonRelationViewModel relation) => AppNavigation.OpenPersonAsync(relation.RelatedPersonId);

    [RelayCommand]
    private async Task AddPhotoAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = T("People.PickPhotoTitle"), FileTypes = FilePickerFileType.Images });
        if (result is null) return;
        var directory = Path.Combine(FileSystem.AppDataDirectory, "person-photos");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, $"{Guid.NewGuid():N}{Path.GetExtension(result.FileName)}");
        await using var input = await result.OpenReadAsync();
        await using var output = File.Create(destination);
        await input.CopyToAsync(output);
        await _people.AddPhotoAsync(new AddPersonPhotoRequest { UserId = _userId, PersonId = _personId, FilePath = destination });
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SetPrimaryPhotoAsync(PersonPhotoViewModel photo)
    {
        if (photo.Id == Guid.Empty) return;
        await _people.SetPrimaryPhotoAsync(_userId, _personId, photo.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RemovePhotoAsync(PersonPhotoViewModel photo)
    {
        if (photo.Id == Guid.Empty) return;
        await _people.RemovePhotoAsync(_userId, _personId, photo.Id);
        await LoadAsync();
    }

    private void NotifyCalculated()
    {
        OnPropertyChanged(nameof(PageTitle)); OnPropertyChanged(nameof(HasPhoto)); OnPropertyChanged(nameof(HasDefaultPhoto));
        OnPropertyChanged(nameof(HasPhotos)); OnPropertyChanged(nameof(HasBiography)); OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasWebsite)); OnPropertyChanged(nameof(Initial)); OnPropertyChanged(nameof(WorkCountText));
    }

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        if (_details is not null)
        {
            Biography = _details.Description ?? T("People.NoDescription");
            PopulateLocalizedDetails(_details);
            NotifyCalculated();
        }
    }

    private void PopulateLocalizedDetails(PersonDetailsDto person)
    {
        _allWorks = person.Works;
        RoleSummary = string.Join("  •  ", person.Works.Select(work => T($"ContributionRole.{work.Role}")).Distinct().Take(6));
        CategorySummary = string.Join("  •  ", person.Works
            .GroupBy(work => work.MediaType)
            .OrderByDescending(group => group.Select(work => work.MediaItemId).Distinct().Count())
            .Select(group => $"{T($"MediaType.{group.Key}")} {group.Select(work => work.MediaItemId).Distinct().Count()}"));
        Replace(Relations, person.Relations.Select(relation => new PersonRelationViewModel(
            relation.Id,
            relation.RelatedPersonId,
            relation.RelatedPersonName,
            T($"PersonRelationKind.{relation.Kind}"))));
        RebuildWorkRoleTabs();
        ApplyWorkRoleFilter();
        Replace(TopWorks, ConsolidateWorks(person.Works)
            .Where(work => work.Rating.HasValue)
            .OrderByDescending(work => work.Rating)
            .ThenBy(work => work.Title)
            .Take(5));
        Replace(Photos, person.Photos.Where(photo => IsUsablePhoto(photo.FilePath)).Select(photo => new PersonPhotoViewModel(
            photo.Id, photo.FilePath, photo.Caption ?? string.Empty, photo.IsPrimary, photo.IsPrimary ? T("People.PrimaryPhoto") : string.Empty)));
    }

    private void RebuildWorkRoleTabs()
    {
        var availableRoles = _allWorks
            .Select(work => work.Role)
            .Distinct()
            .OrderBy(RoleOrder)
            .ToArray();

        if (_selectedWorkRole.HasValue && !availableRoles.Contains(_selectedWorkRole.Value))
        {
            _selectedWorkRole = null;
        }

        WorkRoleTabs.Clear();
        WorkRoleTabs.Add(new LocalizedOption<ContributionRole?>(null, T("Common.All")) { IsSelected = !_selectedWorkRole.HasValue });
        foreach (var role in availableRoles)
        {
            WorkRoleTabs.Add(new LocalizedOption<ContributionRole?>(role, T($"ContributionRole.{role}")) { IsSelected = _selectedWorkRole == role });
        }
    }

    private void ApplyWorkRoleFilter()
    {
        var filtered = _selectedWorkRole.HasValue
            ? _allWorks.Where(work => work.Role == _selectedWorkRole.Value)
            : _allWorks;

        Replace(Works, ConsolidateWorks(filtered)
            .OrderByDescending(work => work.Rating ?? -1)
            .ThenBy(work => work.Title));
    }

    private IEnumerable<PersonWorkViewModel> ConsolidateWorks(IEnumerable<PersonWorkDto> works) =>
        works
            .GroupBy(work => work.MediaItemId)
            .Select(group =>
            {
                var representative = group
                    .OrderByDescending(work => work.Rating ?? -1)
                    .ThenBy(work => work.Title)
                    .First();
                var roles = group
                    .Select(work => work.Role)
                    .Distinct()
                    .OrderBy(RoleOrder)
                    .Select(role => T($"ContributionRole.{role}"));

                return new PersonWorkViewModel(
                    representative.MediaItemId,
                    representative.Title,
                    T($"MediaType.{representative.MediaType}"),
                    string.Join(" • ", roles),
                    representative.Rating);
            });

    private static int RoleOrder(ContributionRole role) => role switch
    {
        ContributionRole.Director => 0,
        ContributionRole.Author => 1,
        ContributionRole.Developer => 2,
        ContributionRole.Screenwriter => 3,
        ContributionRole.Producer => 4,
        ContributionRole.Actor => 5,
        ContributionRole.VoiceActor => 6,
        ContributionRole.Cinematographer => 7,
        ContributionRole.Composer => 8,
        ContributionRole.CastingDirector => 9,
        ContributionRole.ProductionDesigner => 10,
        ContributionRole.Illustrator => 11,
        ContributionRole.Artist => 12,
        ContributionRole.Translator => 13,
        ContributionRole.Editor => 14,
        _ => 15
    };

    private static string FormatYears(int? birth, int? death) => birth.HasValue || death.HasValue ? $"{birth?.ToString(CultureInfo.InvariantCulture) ?? "?"} — {death?.ToString(CultureInfo.InvariantCulture) ?? "…"}" : string.Empty;
    private static bool IsUsablePhoto(string? path) => !string.IsNullOrWhiteSpace(path) && ((Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") || File.Exists(path));
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source) { target.Clear(); foreach (var value in source) target.Add(value); }
}
