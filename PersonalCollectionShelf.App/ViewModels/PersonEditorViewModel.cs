using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class PersonEditorViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IPeopleManagementService _people;
    private readonly IPersonService _personSearch;
    private readonly IAuthService _auth;
    private Guid? _personId;
    private string _userId = "local-user";

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
    [ObservableProperty] public partial string PhotoPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial PersonDto? SelectedRelatedPerson { get; set; }
    [ObservableProperty] public partial LocalizedOption<PersonRelationKind>? SelectedRelationKind { get; set; }
    [ObservableProperty] public partial string RelationNotes { get; set; } = string.Empty;

    public PersonEditorViewModel(IPeopleManagementService people, IPersonService personSearch, IAuthService auth, ILocalizationService localization)
        : base(localization)
    {
        _people = people;
        _personSearch = personSearch;
        _auth = auth;
        InitializeRelationKinds();
    }

    public ObservableCollection<string> Professions { get; } = [];
    public ObservableCollection<PersonDto> RelatedPeople { get; } = [];
    public ObservableCollection<PersonRelationDto> Relations { get; } = [];
    public ObservableCollection<LocalizedOption<PersonRelationKind>> RelationKinds { get; } = [];

    public string PageTitle => _personId.HasValue ? T("People.EditTitle") : T("People.CreateTitle");
    public string SaveText => T("Common.Save");
    public string AddText => T("Common.Add");
    public string ProfileSectionTitle => T("People.Profile");
    public string ProfessionsSectionTitle => T("People.Professions");
    public string RelationsSectionTitle => T("People.Relations");
    public string NameLabel => T("People.Name");
    public string PenNameLabel => T("People.PenName");
    public string FirstNameLabel => T("People.FirstName");
    public string MiddleNameLabel => T("People.MiddleName");
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
    public string PickPhotoText => T("People.PickMainPhoto");
    public string RelatedPersonLabel => T("People.RelatedPerson");
    public string RelationKindLabel => T("People.RelationKind");
    public bool HasPhoto => IsUsablePhoto(PhotoPath);
    public bool HasDefaultPhoto => !HasPhoto;
    public bool CanManageRelations => _personId.HasValue;
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : char.ToUpperInvariant(Name[0]).ToString();

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(Initial));
    partial void OnPhotoPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasPhoto));
        OnPropertyChanged(nameof(HasDefaultPhoto));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && Guid.TryParse(raw?.ToString(), out var id))
        {
            _personId = id;
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(CanManageRelations));
        }
    }

    public async Task LoadAsync()
    {
        _userId = await _auth.GetCurrentUserIdAsync() ?? "local-user";
        Replace(RelatedPeople, (await _personSearch.SearchAsync(_userId, null, 500)).Where(person => person.Id != _personId));
        if (!_personId.HasValue) return;
        var details = await _people.GetAsync(_userId, _personId.Value);
        if (details is null) return;
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
        PhotoPath = details.PhotoPath ?? string.Empty;
        Replace(Professions, details.Professions);
        Replace(Relations, details.Relations);
    }

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = T("People.PickPhotoTitle"),
            FileTypes = FilePickerFileType.Images
        });
        if (result is null) return;
        var directory = Path.Combine(FileSystem.AppDataDirectory, "person-photos");
        Directory.CreateDirectory(directory);
        var extension = Path.GetExtension(result.FileName);
        var destination = Path.Combine(directory, $"{Guid.NewGuid():N}{extension}");
        await using var input = await result.OpenReadAsync();
        await using var output = File.Create(destination);
        await input.CopyToAsync(output);
        PhotoPath = destination;
    }

    [RelayCommand]
    private void AddProfession()
    {
        var value = NewProfession.Trim();
        if (value.Length > 0 && !Professions.Contains(value, StringComparer.OrdinalIgnoreCase)) Professions.Add(value);
        NewProfession = string.Empty;
    }

    [RelayCommand]
    private void RemoveProfession(string value) => Professions.Remove(value);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { StatusMessage = T("People.Validation.Name"); return; }
        if (!TryParseYear(BirthYear, out var birthYear) || !TryParseYear(DeathYear, out var deathYear)) { StatusMessage = T("People.Validation.Year"); return; }
        IsBusy = true;
        try
        {
            var saved = await _people.SaveAsync(new SavePersonRequest
            {
                Id = _personId, UserId = _userId, Name = Name, FirstName = FirstName, MiddleName = MiddleName,
                LastName = LastName, PenName = PenName, SortName = SortName, BirthYear = birthYear, DeathYear = deathYear,
                Country = Country, PlaceOfBirth = PlaceOfBirth, Gender = Gender, OfficialWebsite = OfficialWebsite,
                PhotoPath = PhotoPath, Tagline = Tagline, Description = Description, Notes = Notes, Professions = Professions.ToList()
            });
            _personId = saved.Id;
            var selectedPhoto = saved.Photos.FirstOrDefault(photo => string.Equals(photo.FilePath, PhotoPath, StringComparison.OrdinalIgnoreCase));
            if (HasPhoto && selectedPhoto is null)
            {
                var addedPhoto = await _people.AddPhotoAsync(new AddPersonPhotoRequest { UserId = _userId, PersonId = saved.Id, FilePath = PhotoPath });
                await _people.SetPrimaryPhotoAsync(_userId, saved.Id, addedPhoto.Id);
            }
            else if (selectedPhoto is not null && selectedPhoto.Id != Guid.Empty)
            {
                await _people.SetPrimaryPhotoAsync(_userId, saved.Id, selectedPhoto.Id);
            }
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            await CrashReporter.ReportAsync(exception, "PersonEditorViewModel.SaveAsync");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddRelationAsync()
    {
        if (!_personId.HasValue || SelectedRelatedPerson is null || SelectedRelationKind is null) return;
        await _people.SaveRelationAsync(new SavePersonRelationRequest
        {
            UserId = _userId, PersonId = _personId.Value, RelatedPersonId = SelectedRelatedPerson.Id,
            Kind = SelectedRelationKind.Value, InverseKind = Inverse(SelectedRelationKind.Value), Notes = RelationNotes
        });
        RelationNotes = string.Empty;
        SelectedRelatedPerson = null;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RemoveRelationAsync(PersonRelationDto relation)
    {
        await _people.RemoveRelationAsync(_userId, relation.Id);
        Relations.Remove(relation);
    }

    private void InitializeRelationKinds()
    {
        RelationKinds.Clear();
        foreach (var kind in Enum.GetValues<PersonRelationKind>()) RelationKinds.Add(new LocalizedOption<PersonRelationKind>(kind, T($"PersonRelationKind.{kind}")));
        SelectedRelationKind = RelationKinds.FirstOrDefault(option => option.Value == PersonRelationKind.Collaborator);
    }

    private static PersonRelationKind Inverse(PersonRelationKind kind) => kind switch
    {
        PersonRelationKind.Parent => PersonRelationKind.Child, PersonRelationKind.Child => PersonRelationKind.Parent,
        PersonRelationKind.Mentor => PersonRelationKind.Student, PersonRelationKind.Student => PersonRelationKind.Mentor, _ => kind
    };

    private static bool TryParseYear(string value, out int? year)
    {
        year = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!int.TryParse(value, out var parsed) || parsed is < 1 or > 2200) return false;
        year = parsed; return true;
    }

    private static bool IsUsablePhoto(string? path) => !string.IsNullOrWhiteSpace(path) &&
        ((Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") || File.Exists(path));

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear(); foreach (var value in source) target.Add(value);
    }
}
