using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class PersonGalleryViewModel(
    IPeopleManagementService people,
    IAuthService auth,
    ILocalizationService localization) : BaseViewModel(localization), IQueryAttributable
{
    private Guid _personId;
    private bool _isSynchronizingSelection;

    [ObservableProperty]
    public partial string PersonName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PersonPhotoViewModel? SelectedPhoto { get; set; }

    [ObservableProperty]
    public partial int SelectedPhotoIndex { get; set; } = -1;

    public ObservableCollection<PersonPhotoViewModel> Photos { get; } = [];

    public string PageTitle => string.Format(T("People.GalleryTitleFormat"), PersonName);
    public string EmptyText => T("People.GalleryEmpty");
    public string PreviousPhotoText => T("People.GalleryPrevious");
    public string NextPhotoText => T("People.GalleryNext");
    public bool HasPhotos => Photos.Count > 0;
    public bool HasNoPhotos => !HasPhotos;
    public bool CanShowPreviousPhoto => SelectedPhotoIndex > 0;
    public bool CanShowNextPhoto => SelectedPhotoIndex >= 0 && SelectedPhotoIndex < Photos.Count - 1;
    public string PhotoPositionText => HasPhotos
        ? string.Format(T("People.GalleryPositionFormat"), SelectedPhotoIndex + 1, Photos.Count)
        : string.Empty;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && Guid.TryParse(raw?.ToString(), out var id))
        {
            _personId = id;
        }
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var userId = await auth.GetCurrentUserIdAsync() ?? "local-user";
            var person = await people.GetAsync(userId, _personId);
            if (person is null)
            {
                return;
            }

            PersonName = person.Name;
            Photos.Clear();
            foreach (var photo in person.Photos.Where(photo =>
                         File.Exists(photo.FilePath) || Uri.TryCreate(photo.FilePath, UriKind.Absolute, out _)))
            {
                Photos.Add(new PersonPhotoViewModel(
                    photo.Id,
                    photo.FilePath,
                    photo.Caption ?? string.Empty,
                    photo.IsPrimary,
                    photo.IsPrimary ? T("People.PrimaryPhoto") : string.Empty));
            }

            var selected = Photos.FirstOrDefault(photo => photo.IsPrimary) ?? Photos.FirstOrDefault();
            SetSelectedPhotoIndex(selected is null ? -1 : Photos.IndexOf(selected));
            OnPropertyChanged(nameof(PageTitle));
            NotifyGalleryState();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedPhotoIndexChanged(int value)
    {
        if (_isSynchronizingSelection)
        {
            return;
        }

        _isSynchronizingSelection = true;
        SelectedPhoto = value >= 0 && value < Photos.Count ? Photos[value] : null;
        _isSynchronizingSelection = false;
        NotifyGalleryState();
    }

    partial void OnSelectedPhotoChanged(PersonPhotoViewModel? value)
    {
        if (_isSynchronizingSelection)
        {
            return;
        }

        _isSynchronizingSelection = true;
        SelectedPhotoIndex = value is null ? -1 : Photos.IndexOf(value);
        _isSynchronizingSelection = false;
        NotifyGalleryState();
    }

    [RelayCommand]
    private void ShowPreviousPhoto()
    {
        if (CanShowPreviousPhoto)
        {
            SelectedPhotoIndex--;
        }
    }

    [RelayCommand]
    private void ShowNextPhoto()
    {
        if (CanShowNextPhoto)
        {
            SelectedPhotoIndex++;
        }
    }

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(PreviousPhotoText));
        OnPropertyChanged(nameof(NextPhotoText));
        OnPropertyChanged(nameof(PhotoPositionText));
    }

    private void SetSelectedPhotoIndex(int index)
    {
        SelectedPhotoIndex = index;
        SelectedPhoto = index >= 0 && index < Photos.Count ? Photos[index] : null;
    }

    private void NotifyGalleryState()
    {
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(HasNoPhotos));
        OnPropertyChanged(nameof(CanShowPreviousPhoto));
        OnPropertyChanged(nameof(CanShowNextPhoto));
        OnPropertyChanged(nameof(PhotoPositionText));
    }
}
