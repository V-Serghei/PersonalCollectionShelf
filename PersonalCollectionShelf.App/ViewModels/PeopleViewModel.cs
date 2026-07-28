using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class PeopleViewModel : BaseViewModel
{
    private readonly IPersonService _personService;
    private readonly IPeopleManagementService _peopleManagementService;
    private readonly IAuthService _authService;
    private string _userId = "local-user";
    private IReadOnlyList<PersonDto> _allPeople = [];

    [ObservableProperty] public partial PersonDto? SelectedPerson { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string FirstName { get; set; } = string.Empty;
    [ObservableProperty] public partial string MiddleName { get; set; } = string.Empty;
    [ObservableProperty] public partial string LastName { get; set; } = string.Empty;
    [ObservableProperty] public partial string PenName { get; set; } = string.Empty;
    [ObservableProperty] public partial string SortName { get; set; } = string.Empty;
    [ObservableProperty] public partial string BirthYear { get; set; } = string.Empty;
    [ObservableProperty] public partial string DeathYear { get; set; } = string.Empty;
    [ObservableProperty] public partial string Country { get; set; } = string.Empty;
    [ObservableProperty] public partial string PlaceOfBirth { get; set; } = string.Empty;
    [ObservableProperty] public partial string Gender { get; set; } = string.Empty;
    [ObservableProperty] public partial string OfficialWebsite { get; set; } = string.Empty;
    [ObservableProperty] public partial string Tagline { get; set; } = string.Empty;
    [ObservableProperty] public partial string Description { get; set; } = string.Empty;
    [ObservableProperty] public partial string Notes { get; set; } = string.Empty;
    [ObservableProperty] public partial string NewProfession { get; set; } = string.Empty;
    [ObservableProperty] public partial PersonDto? SelectedRelatedPerson { get; set; }
    [ObservableProperty] public partial LocalizedOption<PersonRelationKind>? SelectedRelationKind { get; set; }
    [ObservableProperty] public partial string RelationNotes { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsSearchVisible { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    private Guid? _editingId;

    public PeopleViewModel(
        IPersonService personService,
        IPeopleManagementService peopleManagementService,
        IAuthService authService,
        ILocalizationService localizationService) : base(localizationService)
    {
        _personService = personService;
        _peopleManagementService = peopleManagementService;
        _authService = authService;
        InitializeRelationKinds();
    }

    public ObservableCollection<PersonDto> People { get; } = [];
    public ObservableCollection<PersonDto> RelatedPeople { get; } = [];
    public ObservableCollection<string> Professions { get; } = [];
    public ObservableCollection<PersonRelationDto> Relations { get; } = [];
    public ObservableCollection<PersonWorkDto> Works { get; } = [];
    public ObservableCollection<LocalizedOption<PersonRelationKind>> RelationKinds { get; } = [];

    public string PageTitle => T("People.Title");
    public string BackText => T("Common.Back");
    public string NewPersonText => T("People.New");
    public string SaveText => T("Common.Save");
    public string ProfileSectionTitle => T("People.Profile");
    public string ProfessionsSectionTitle => T("People.Professions");
    public string RelationsSectionTitle => T("People.Relations");
    public string WorksSectionTitle => T("People.Works");
    public string AddText => T("Common.Add");
    public string EmptyText => T("People.Empty");
    public string NameLabel => T("People.Name");
    public string PenNameLabel => T("People.PenName");
    public string FirstNameLabel => T("People.FirstName");
    public string LastNameLabel => T("People.LastName");
    public string SortNameLabel => T("People.SortName");
    public string BirthYearLabel => T("People.BirthYear");
    public string DeathYearLabel => T("People.DeathYear");
    public string CountryLabel => T("People.Country");
    public string PlaceOfBirthLabel => T("People.PlaceOfBirth");
    public string GenderLabel => T("People.Gender");
    public string WebsiteLabel => T("People.Website");
    public string TaglineLabel => T("People.Tagline");
    public string DescriptionLabel => T("People.Description");
    public string NotesLabel => T("People.Notes");
    public string ProfessionPlaceholder => T("People.ProfessionPlaceholder");
    public string RelatedPersonLabel => T("People.RelatedPerson");
    public string RelationKindLabel => T("People.RelationKind");
    public string SearchPlaceholder => T("People.SearchPlaceholder");

    partial void OnSearchTextChanged(string value) => ApplyPeopleFilter();

    partial void OnSelectedPersonChanged(PersonDto? value)
    {
        if (value is not null)
        {
            _ = LoadPersonAsync(value.Id);
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        _userId = await _authService.GetCurrentUserIdAsync() ?? "local-user";
        var selectedId = _editingId;
        _allPeople = await _personService.SearchAsync(_userId, null, 500);
        ApplyPeopleFilter();
        RefreshRelatedPeople();
        if (selectedId.HasValue)
        {
            SelectedPerson = People.FirstOrDefault(value => value.Id == selectedId.Value);
        }
        else if (People.Count == 0)
        {
            NewPerson();
        }
    }

    [RelayCommand]
    private void NewPerson()
    {
        _editingId = null;
        SelectedPerson = null;
        ClearForm();
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        if (!IsSearchVisible) SearchText = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = T("People.Validation.Name");
            return;
        }

        if (!TryParseYear(BirthYear, out var birthYear) || !TryParseYear(DeathYear, out var deathYear))
        {
            StatusMessage = T("People.Validation.Year");
            return;
        }

        IsBusy = true;
        try
        {
            var saved = await _peopleManagementService.SaveAsync(new SavePersonRequest
            {
                Id = _editingId,
                UserId = _userId,
                Name = Name,
                FirstName = FirstName,
                MiddleName = MiddleName,
                LastName = LastName,
                PenName = PenName,
                SortName = SortName,
                BirthYear = birthYear,
                DeathYear = deathYear,
                Country = Country,
                PlaceOfBirth = PlaceOfBirth,
                Gender = Gender,
                OfficialWebsite = OfficialWebsite,
                Tagline = Tagline,
                Description = Description,
                Notes = Notes,
                Professions = Professions.ToList()
            });
            _editingId = saved.Id;
            await LoadAsync();
            StatusMessage = T("People.Saved");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            await CrashReporter.ReportAsync(exception, "PeopleViewModel.SaveAsync");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddProfession()
    {
        var value = NewProfession.Trim();
        if (value.Length > 0 && !Professions.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            Professions.Add(value);
        }
        NewProfession = string.Empty;
    }

    [RelayCommand]
    private void RemoveProfession(string value) => Professions.Remove(value);

    [RelayCommand]
    private async Task AddRelationAsync()
    {
        if (!_editingId.HasValue)
        {
            StatusMessage = T("People.RelationSaveFirst");
            return;
        }
        if (SelectedRelatedPerson is null || SelectedRelationKind is null)
        {
            StatusMessage = T("People.RelationRequired");
            return;
        }

        await _peopleManagementService.SaveRelationAsync(new SavePersonRelationRequest
        {
            UserId = _userId,
            PersonId = _editingId.Value,
            RelatedPersonId = SelectedRelatedPerson.Id,
            Kind = SelectedRelationKind.Value,
            InverseKind = Inverse(SelectedRelationKind.Value),
            Notes = RelationNotes
        });
        SelectedRelatedPerson = null;
        RelationNotes = string.Empty;
        await LoadPersonAsync(_editingId.Value);
    }

    [RelayCommand]
    private async Task RemoveRelationAsync(PersonRelationDto relation)
    {
        await _peopleManagementService.RemoveRelationAsync(_userId, relation.Id);
        Relations.Remove(relation);
    }

    [RelayCommand]
    private Task OpenWorkAsync(PersonWorkDto work) => AppNavigation.OpenMediaDetailsAsync(work.MediaItemId);

    [RelayCommand]
    private Task GoBackAsync() => AppNavigation.CloseAsync();

    private async Task LoadPersonAsync(Guid id)
    {
        var details = await _peopleManagementService.GetAsync(_userId, id);
        if (details is null)
        {
            return;
        }
        _editingId = details.Id;
        Name = details.Name;
        FirstName = details.FirstName ?? string.Empty;
        MiddleName = details.MiddleName ?? string.Empty;
        LastName = details.LastName ?? string.Empty;
        PenName = details.PenName ?? string.Empty;
        SortName = details.SortName ?? string.Empty;
        BirthYear = details.BirthYear?.ToString() ?? string.Empty;
        DeathYear = details.DeathYear?.ToString() ?? string.Empty;
        Country = details.Country ?? string.Empty;
        PlaceOfBirth = details.PlaceOfBirth ?? string.Empty;
        Gender = details.Gender ?? string.Empty;
        OfficialWebsite = details.OfficialWebsite ?? string.Empty;
        Tagline = details.Tagline ?? string.Empty;
        Description = details.Description ?? string.Empty;
        Notes = details.Notes ?? string.Empty;
        Replace(Professions, details.Professions);
        Replace(Relations, details.Relations);
        Replace(Works, details.Works);
        RefreshRelatedPeople();
    }

    private void ClearForm()
    {
        Name = FirstName = MiddleName = LastName = PenName = SortName = string.Empty;
        BirthYear = DeathYear = Country = PlaceOfBirth = Gender = OfficialWebsite = string.Empty;
        Tagline = Description = Notes = NewProfession = RelationNotes = string.Empty;
        SelectedRelatedPerson = null;
        Professions.Clear();
        Relations.Clear();
        Works.Clear();
        RefreshRelatedPeople();
    }

    private void ApplyPeopleFilter()
    {
        var term = SearchText.Trim();
        Replace(People, _allPeople.Where(value => term.Length == 0 ||
            value.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(value.Tagline) && value.Tagline.Contains(term, StringComparison.OrdinalIgnoreCase))));
    }

    private void RefreshRelatedPeople() => Replace(RelatedPeople, _allPeople.Where(value => value.Id != _editingId));

    private void InitializeRelationKinds()
    {
        RelationKinds.Clear();
        foreach (var kind in Enum.GetValues<PersonRelationKind>())
        {
            RelationKinds.Add(new LocalizedOption<PersonRelationKind>(kind, T($"PersonRelationKind.{kind}")));
        }
        SelectedRelationKind = RelationKinds.FirstOrDefault(value => value.Value == PersonRelationKind.Collaborator);
    }

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        var selectedKind = SelectedRelationKind?.Value;
        InitializeRelationKinds();
        SelectedRelationKind = RelationKinds.FirstOrDefault(value => value.Value == selectedKind) ?? RelationKinds.FirstOrDefault();
    }

    private static PersonRelationKind Inverse(PersonRelationKind kind) => kind switch
    {
        PersonRelationKind.Parent => PersonRelationKind.Child,
        PersonRelationKind.Child => PersonRelationKind.Parent,
        PersonRelationKind.Mentor => PersonRelationKind.Student,
        PersonRelationKind.Student => PersonRelationKind.Mentor,
        _ => kind
    };

    private static bool TryParseYear(string value, out int? year)
    {
        year = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!int.TryParse(value, out var parsed) || parsed is < 1 or > 2200) return false;
        year = parsed;
        return true;
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }
}
