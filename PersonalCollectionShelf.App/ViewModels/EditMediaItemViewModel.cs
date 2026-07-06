using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Application.Validation;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class EditMediaItemViewModel : BaseViewModel
{
    private readonly IMediaItemService _mediaItemService;
    private readonly IAuthService _authService;
    public EditMediaItemViewModel(
        IMediaItemService mediaItemService,
        IAuthService authService,
        ILocalizationService localizationService)
        : base(localizationService)
    {
        _mediaItemService = mediaItemService;
        _authService = authService;
        ReloadOptions();
        ResetFields();
    }

    public ObservableCollection<LocalizedOption<MediaType>> MediaTypes { get; } = [];

    public ObservableCollection<LocalizedOption<MediaStatus>> Statuses { get; } = [];

    [ObservableProperty]
    private Guid? mediaItemId;

    [ObservableProperty]
    private string itemTitle = string.Empty;

    [ObservableProperty]
    private string originalTitle = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string category = string.Empty;

    [ObservableProperty]
    private string tags = string.Empty;

    [ObservableProperty]
    private LocalizedOption<MediaType>? selectedMediaType;

    [ObservableProperty]
    private LocalizedOption<MediaStatus>? selectedStatus;

    [ObservableProperty]
    private string rating = string.Empty;

    [ObservableProperty]
    private string progressCurrent = string.Empty;

    [ObservableProperty]
    private string progressTotal = string.Empty;

    [ObservableProperty]
    private bool hasStartDate;

    [ObservableProperty]
    private DateTime startDate = DateTime.Today;

    [ObservableProperty]
    private bool hasFinishDate;

    [ObservableProperty]
    private DateTime finishDate = DateTime.Today;

    [ObservableProperty]
    private string releaseYear = string.Empty;

    [ObservableProperty]
    private string coverUrl = string.Empty;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEditMode => MediaItemId.HasValue;

    public string PageTitle => IsEditMode ? T("Edit.Title.Edit") : T("Edit.Title.Create");

    public string TitleLabel => T("Edit.Label.Title");

    public string TitlePlaceholder => T("Edit.Placeholder.Title");

    public string OriginalTitleLabel => T("Edit.Label.OriginalTitle");

    public string OriginalTitlePlaceholder => T("Edit.Placeholder.OriginalTitle");

    public string DescriptionLabel => T("Edit.Label.Description");

    public string DescriptionPlaceholder => T("Edit.Placeholder.Description");

    public string CategoryLabel => T("Edit.Label.Category");

    public string CategoryPlaceholder => T("Edit.Placeholder.Category");

    public string TagsLabel => T("Edit.Label.Tags");

    public string TagsPlaceholder => T("Edit.Placeholder.Tags");

    public string MediaTypeLabel => T("Edit.Label.MediaType");

    public string StatusLabel => T("Edit.Label.Status");

    public string RatingLabel => T("Edit.Label.Rating");

    public string RatingPlaceholder => T("Edit.Placeholder.Rating");

    public string ProgressCurrentLabel => T("Edit.Label.ProgressCurrent");

    public string ProgressCurrentPlaceholder => T("Edit.Placeholder.ProgressCurrent");

    public string ProgressTotalLabel => T("Edit.Label.ProgressTotal");

    public string ProgressTotalPlaceholder => T("Edit.Placeholder.ProgressTotal");

    public string StartDateLabel => T("Edit.Label.StartDate");

    public string FinishDateLabel => T("Edit.Label.FinishDate");

    public string ReleaseYearLabel => T("Edit.Label.ReleaseYear");

    public string ReleaseYearPlaceholder => T("Edit.Placeholder.ReleaseYear");

    public string CoverUrlLabel => T("Edit.Label.CoverUrl");

    public string CoverUrlPlaceholder => T("Edit.Placeholder.CoverUrl");

    public string NotesLabel => T("Edit.Label.Notes");

    public string NotesPlaceholder => T("Edit.Placeholder.Notes");

    public string FavoriteLabel => T("Edit.Label.Favorite");

    public string SaveButtonText => T("Common.Save");

    public string CancelButtonText => T("Common.Cancel");

    public async Task LoadForCreateAsync()
    {
        MediaItemId = null;
        ResetFields();
        await Task.CompletedTask;
    }

    public async Task LoadForEditAsync(Guid itemId)
    {
        MediaItemId = itemId;
        ErrorMessage = string.Empty;

        var userId = await GetCurrentUserIdAsync();
        var item = await _mediaItemService.GetMediaItemAsync(itemId, userId);
        if (item is null)
        {
            ErrorMessage = T("Edit.Error.NotFound");
            return;
        }

        ItemTitle = item.Title;
        OriginalTitle = item.OriginalTitle ?? string.Empty;
        Description = item.Description ?? string.Empty;
        Category = item.Category ?? string.Empty;
        Tags = item.Tags ?? string.Empty;
        SelectedMediaType = MediaTypes.First(option => option.Value == item.MediaType);
        SelectedStatus = Statuses.First(option => option.Value == item.Status);
        Rating = item.Rating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        ProgressCurrent = item.ProgressCurrent.ToString(CultureInfo.InvariantCulture);
        ProgressTotal = item.ProgressTotal?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        HasStartDate = item.StartDate.HasValue;
        StartDate = item.StartDate ?? DateTime.Today;
        HasFinishDate = item.FinishDate.HasValue;
        FinishDate = item.FinishDate ?? DateTime.Today;
        ReleaseYear = item.ReleaseYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        CoverUrl = item.CoverUrl ?? string.Empty;
        Notes = item.Notes ?? string.Empty;
        IsFavorite = item.IsFavorite;
    }

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        ReloadOptions();
    }

    partial void OnMediaItemIdChanged(Guid? value)
    {
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(PageTitle));
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasErrorMessage));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;

        if (!TryParseOptionalInt(Rating, out var parsedRating) ||
            !TryParseRequiredInt(ProgressCurrent, 0, out var parsedProgressCurrent) ||
            !TryParseOptionalInt(ProgressTotal, out var parsedProgressTotal) ||
            !TryParseOptionalInt(ReleaseYear, out var parsedReleaseYear))
        {
            ErrorMessage = T("Validation.NumberInvalid");
            return;
        }

        try
        {
            var userId = await GetCurrentUserIdAsync();

            if (MediaItemId.HasValue)
            {
                await _mediaItemService.UpdateMediaItemAsync(new UpdateMediaItemRequest
                {
                    Id = MediaItemId.Value,
                    UserId = userId,
                    Title = ItemTitle,
                    OriginalTitle = OriginalTitle,
                    Description = Description,
                    Category = Category,
                    Tags = Tags,
                    MediaType = SelectedMediaType?.Value ?? MediaType.Other,
                    Status = SelectedStatus?.Value ?? MediaStatus.Planned,
                    Rating = parsedRating,
                    ProgressCurrent = parsedProgressCurrent,
                    ProgressTotal = parsedProgressTotal,
                    StartDate = HasStartDate ? StartDate : null,
                    FinishDate = HasFinishDate ? FinishDate : null,
                    ReleaseYear = parsedReleaseYear,
                    CoverUrl = CoverUrl,
                    Notes = Notes,
                    IsFavorite = IsFavorite
                });
            }
            else
            {
                await _mediaItemService.CreateMediaItemAsync(new CreateMediaItemRequest
                {
                    UserId = userId,
                    Title = ItemTitle,
                    OriginalTitle = OriginalTitle,
                    Description = Description,
                    Category = Category,
                    Tags = Tags,
                    MediaType = SelectedMediaType?.Value ?? MediaType.Other,
                    Status = SelectedStatus?.Value ?? MediaStatus.Planned,
                    Rating = parsedRating,
                    ProgressCurrent = parsedProgressCurrent,
                    ProgressTotal = parsedProgressTotal,
                    StartDate = HasStartDate ? StartDate : null,
                    FinishDate = HasFinishDate ? FinishDate : null,
                    ReleaseYear = parsedReleaseYear,
                    CoverUrl = CoverUrl,
                    Notes = Notes,
                    IsFavorite = IsFavorite
                });
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (ValidationException exception)
        {
            ErrorMessage = string.Join(Environment.NewLine, exception.Errors.Select(error => T(error.Code)));
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = T("Edit.Error.NotFound");
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private void ReloadOptions()
    {
        var selectedMediaType = SelectedMediaType?.Value ?? MediaType.Other;
        var selectedStatus = SelectedStatus?.Value ?? MediaStatus.Planned;

        MediaTypes.Clear();
        foreach (var mediaType in Enum.GetValues<MediaType>())
        {
            MediaTypes.Add(new LocalizedOption<MediaType>(mediaType, T($"MediaType.{mediaType}")));
        }

        Statuses.Clear();
        foreach (var status in Enum.GetValues<MediaStatus>())
        {
            Statuses.Add(new LocalizedOption<MediaStatus>(status, T($"MediaStatus.{status}")));
        }

        SelectedMediaType = MediaTypes.First(option => option.Value == selectedMediaType);
        SelectedStatus = Statuses.First(option => option.Value == selectedStatus);
    }

    private void ResetFields()
    {
        ItemTitle = string.Empty;
        OriginalTitle = string.Empty;
        Description = string.Empty;
        Category = string.Empty;
        Tags = string.Empty;
        SelectedMediaType = MediaTypes.First(option => option.Value == MediaType.Other);
        SelectedStatus = Statuses.First(option => option.Value == MediaStatus.Planned);
        Rating = string.Empty;
        ProgressCurrent = string.Empty;
        ProgressTotal = string.Empty;
        HasStartDate = false;
        StartDate = DateTime.Today;
        HasFinishDate = false;
        FinishDate = DateTime.Today;
        ReleaseYear = string.Empty;
        CoverUrl = string.Empty;
        Notes = string.Empty;
        IsFavorite = false;
        ErrorMessage = string.Empty;
    }

    private static bool TryParseOptionalInt(string value, out int? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParseRequiredInt(string value, int defaultValue, out int result)
    {
        result = defaultValue;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        return await _authService.GetCurrentUserIdAsync() ?? "local-user";
    }
}
