using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class PersonGalleryViewModel(IPeopleManagementService people, IAuthService auth, ILocalizationService localization) : BaseViewModel(localization), IQueryAttributable
{
    private Guid _personId;
    [ObservableProperty] public partial string PersonName { get; set; } = string.Empty;
    [ObservableProperty] public partial PersonPhotoViewModel? SelectedPhoto { get; set; }
    public ObservableCollection<PersonPhotoViewModel> Photos { get; } = [];
    public string PageTitle => string.Format(T("People.GalleryTitleFormat"), PersonName);
    public string EmptyText => T("People.GalleryEmpty");

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && Guid.TryParse(raw?.ToString(), out var id)) _personId = id;
    }

    public async Task LoadAsync()
    {
        var userId = await auth.GetCurrentUserIdAsync() ?? "local-user";
        var person = await people.GetAsync(userId, _personId);
        if (person is null) return;
        PersonName = person.Name;
        Photos.Clear();
        foreach (var photo in person.Photos.Where(photo => File.Exists(photo.FilePath) || Uri.TryCreate(photo.FilePath, UriKind.Absolute, out _)))
            Photos.Add(new PersonPhotoViewModel(photo.Id, photo.FilePath, photo.Caption ?? string.Empty, photo.IsPrimary, photo.IsPrimary ? T("People.PrimaryPhoto") : string.Empty));
        SelectedPhoto = Photos.FirstOrDefault(photo => photo.IsPrimary) ?? Photos.FirstOrDefault();
        OnPropertyChanged(nameof(PageTitle));
    }

    [RelayCommand]
    private void SelectPhoto(PersonPhotoViewModel photo) => SelectedPhoto = photo;
}
