using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Pages;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class MediaDetailsViewModel : BaseViewModel
{
    private readonly IMediaItemService _mediaItemService;
    private readonly IAuthService _authService;
    private Guid? _currentItemId;

    public MediaDetailsViewModel(
        IMediaItemService mediaItemService,
        IAuthService authService,
        ILocalizationService localizationService)
        : base(localizationService)
    {
        _mediaItemService = mediaItemService;
        _authService = authService;
    }

    [ObservableProperty]
    private MediaItemDto? item;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public string PageTitle => Item?.Title ?? T("Details.Title");

    public string TypeLabel => T("Details.TypeLabel");

    public string StatusLabel => T("Details.StatusLabel");

    public string ProgressLabel => T("Details.ProgressLabel");

    public string RatingLabel => T("Details.RatingLabel");

    public string NotesLabel => T("Details.NotesLabel");

    public string OriginalTitleLabel => T("Details.OriginalTitleLabel");

    public string DescriptionLabel => T("Details.DescriptionLabel");

    public string StartDateLabel => T("Details.StartDateLabel");

    public string FinishDateLabel => T("Details.FinishDateLabel");

    public string ReleaseYearLabel => T("Details.ReleaseYearLabel");

    public string FavoriteLabel => T("Details.FavoriteLabel");

    public string CreatedAtLabel => T("Details.CreatedAtLabel");

    public string UpdatedAtLabel => T("Details.UpdatedAtLabel");

    public string EditButtonText => T("Details.EditButton");

    public string DeleteButtonText => T("Details.DeleteButton");

    public string DeleteConfirmTitle => T("Details.DeleteConfirmTitle");

    public string DeleteConfirmMessage => T("Details.DeleteConfirmMessage");

    public string DeleteConfirmButtonText => T("Details.DeleteConfirmButton");

    public string DeleteCancelButtonText => T("Common.Cancel");

    public string OriginalTitleValue => Item?.OriginalTitle ?? T("Common.NotSet");

    public string DescriptionValue => Item?.Description ?? T("Common.NotSet");

    public string TypeValue => Item is null ? T("Common.NotSet") : T($"MediaType.{Item.MediaType}");

    public string StatusValue => Item is null ? T("Common.NotSet") : T($"MediaStatus.{Item.Status}");

    public string ProgressValue => Item is null
        ? T("Common.NotSet")
        : Item.ProgressTotal.HasValue
            ? string.Format(T("Library.ProgressWithTotalFormat"), Item.ProgressCurrent, Item.ProgressTotal.Value)
            : string.Format(T("Library.ProgressFormat"), Item.ProgressCurrent);

    public string RatingValue => Item?.Rating is null
        ? T("Library.NoRating")
        : string.Format(T("Library.RatingFormat"), Item.Rating.Value);

    public string NotesValue => Item?.Notes ?? T("Common.NotSet");

    public string StartDateValue => FormatDate(Item?.StartDate);

    public string FinishDateValue => FormatDate(Item?.FinishDate);

    public string ReleaseYearValue => Item?.ReleaseYear?.ToString(CultureInfo.InvariantCulture) ?? T("Common.NotSet");

    public string FavoriteValue => Item?.IsFavorite == true ? T("Common.Yes") : T("Common.No");

    public string CreatedAtValue => FormatDate(Item?.CreatedAt);

    public string UpdatedAtValue => FormatDate(Item?.UpdatedAt);

    public async Task LoadAsync(Guid itemId)
    {
        _currentItemId = itemId;
        var userId = await GetCurrentUserIdAsync();
        Item = await _mediaItemService.GetMediaItemAsync(itemId, userId);
        ErrorMessage = Item is null ? T("Details.NotFound") : string.Empty;
        RefreshItemProperties();
    }

    public async Task ReloadAsync()
    {
        if (_currentItemId.HasValue)
        {
            await LoadAsync(_currentItemId.Value);
        }
    }

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        RefreshItemProperties();
    }

    partial void OnItemChanged(MediaItemDto? value)
    {
        RefreshItemProperties();
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (Item is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"{nameof(EditMediaItemPage)}?id={Item.Id}");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Item is null)
        {
            return;
        }

        var shouldDelete = await Shell.Current.DisplayAlertAsync(
            DeleteConfirmTitle,
            string.Format(DeleteConfirmMessage, Item.Title),
            DeleteConfirmButtonText,
            DeleteCancelButtonText);

        if (!shouldDelete)
        {
            return;
        }

        var userId = await GetCurrentUserIdAsync();
        await _mediaItemService.DeleteMediaItemAsync(Item.Id, userId);
        await Shell.Current.GoToAsync("..");
    }

    private void RefreshItemProperties()
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(OriginalTitleValue));
        OnPropertyChanged(nameof(DescriptionValue));
        OnPropertyChanged(nameof(TypeValue));
        OnPropertyChanged(nameof(StatusValue));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(RatingValue));
        OnPropertyChanged(nameof(NotesValue));
        OnPropertyChanged(nameof(StartDateValue));
        OnPropertyChanged(nameof(FinishDateValue));
        OnPropertyChanged(nameof(ReleaseYearValue));
        OnPropertyChanged(nameof(FavoriteValue));
        OnPropertyChanged(nameof(CreatedAtValue));
        OnPropertyChanged(nameof(UpdatedAtValue));
    }

    private string FormatDate(DateTime? value)
    {
        return value?.ToString("d", CultureInfo.CurrentCulture) ?? T("Common.NotSet");
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        return await _authService.GetCurrentUserIdAsync() ?? "local-user";
    }
}
