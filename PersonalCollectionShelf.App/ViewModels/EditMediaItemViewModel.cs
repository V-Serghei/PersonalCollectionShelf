using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
#if WINDOWS
using System.Runtime.InteropServices;
#else
using Microsoft.Maui.Devices;
#endif
using Microsoft.Maui.Storage;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Application.Validation;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class EditMediaItemViewModel : BaseViewModel
{
#if !WINDOWS
    private static readonly FilePickerFileType ImageFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.WinUI, [".png", ".jpg", ".jpeg", ".webp", ".bmp"] },
        { DevicePlatform.Android, ["image/*"] }
    });
#endif

    private readonly IMediaItemService _mediaItemService;
    private readonly IPersonService _personService;
    private readonly IAuthService _authService;
    public EditMediaItemViewModel(
        IMediaItemService mediaItemService,
        IPersonService personService,
        IAuthService authService,
        ILocalizationService localizationService)
        : base(localizationService)
    {
        _mediaItemService = mediaItemService;
        _personService = personService;
        _authService = authService;
        ReloadOptions();
        InitializeBookFields();
        ResetFields();
    }

    public ObservableCollection<LocalizedOption<MediaType>> MediaTypes { get; } = [];

    public ObservableCollection<LocalizedOption<MediaStatus>> Statuses { get; } = [];

    public ObservableCollection<string> TagChips { get; } = [];

    public ObservableCollection<string> CastChips { get; } = [];

    private Guid? _mediaItemId;
    private Guid? _mediaCategoryId;
    private string _itemTitle = string.Empty;
    private string _originalTitle = string.Empty;
    private string _description = string.Empty;
    private string _category = string.Empty;
    private string _creator = string.Empty;
    private string _publisher = string.Empty;
    private string _serialNumber = string.Empty;
    private string _newTagText = string.Empty;
    private string _newCastText = string.Empty;
    private LocalizedOption<MediaType>? _selectedMediaType;
    private LocalizedOption<MediaStatus>? _selectedStatus;
    private string _rating = string.Empty;
    private string _progressCurrent = string.Empty;
    private string _progressTotal = string.Empty;
    private bool _hasStartDate;
    private DateTime _startDate = DateTime.Today;
    private bool _hasFinishDate;
    private DateTime _finishDate = DateTime.Today;
    private string _releaseYear = string.Empty;
    private string _coverUrl = string.Empty;
    private string _notes = string.Empty;
    private bool _isFavorite;
    private string _errorMessage = string.Empty;

    public Guid? MediaItemId
    {
        get => _mediaItemId;
        set
        {
            if (SetProperty(ref _mediaItemId, value))
            {
                OnPropertyChanged(nameof(IsEditMode));
                OnPropertyChanged(nameof(PageTitle));
            }
        }
    }

    public string ItemTitle
    {
        get => _itemTitle;
        set => SetProperty(ref _itemTitle, value);
    }

    public string OriginalTitle
    {
        get => _originalTitle;
        set => SetProperty(ref _originalTitle, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string NewTagText
    {
        get => _newTagText;
        set => SetProperty(ref _newTagText, value);
    }

    public string NewCastText
    {
        get => _newCastText;
        set => SetProperty(ref _newCastText, value);
    }

    public string Creator
    {
        get => _creator;
        set => SetProperty(ref _creator, value);
    }

    public string Publisher
    {
        get => _publisher;
        set => SetProperty(ref _publisher, value);
    }

    public string SerialNumber
    {
        get => _serialNumber;
        set => SetProperty(ref _serialNumber, value);
    }

    public LocalizedOption<MediaType>? SelectedMediaType
    {
        get => _selectedMediaType;
        set
        {
            if (SetProperty(ref _selectedMediaType, value))
            {
                OnPropertyChanged(nameof(CreatorLabel));
                OnPropertyChanged(nameof(ShowCastField));
                OnPropertyChanged(nameof(ShowBookFields));
            }
        }
    }

    public bool ShowCastField => SelectedMediaType?.Value is MediaType.Movie or MediaType.Series;

    public LocalizedOption<MediaStatus>? SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
    }

    public string Rating
    {
        get => _rating;
        set
        {
            if (SetProperty(ref _rating, value))
            {
                OnRatingTextChanged();
            }
        }
    }

    public string ProgressCurrent
    {
        get => _progressCurrent;
        set => SetProperty(ref _progressCurrent, value);
    }

    public string ProgressTotal
    {
        get => _progressTotal;
        set => SetProperty(ref _progressTotal, value);
    }

    public bool HasStartDate
    {
        get => _hasStartDate;
        set => SetProperty(ref _hasStartDate, value);
    }

    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                HasStartDate = true;
            }
        }
    }

    public bool HasFinishDate
    {
        get => _hasFinishDate;
        set => SetProperty(ref _hasFinishDate, value);
    }

    public DateTime FinishDate
    {
        get => _finishDate;
        set
        {
            if (SetProperty(ref _finishDate, value))
            {
                HasFinishDate = true;
            }
        }
    }

    public string ReleaseYear
    {
        get => _releaseYear;
        set => SetProperty(ref _releaseYear, value);
    }

    public string CoverUrl
    {
        get => _coverUrl;
        set
        {
            if (SetProperty(ref _coverUrl, value))
            {
                OnPropertyChanged(nameof(HasCoverUrl));
                OnPropertyChanged(nameof(HasNoCoverUrl));
            }
        }
    }

    public bool HasCoverUrl => MediaPresentation.HasValidCoverUrl(CoverUrl);

    public bool HasNoCoverUrl => !HasCoverUrl;

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteIcon));
                OnPropertyChanged(nameof(FavoriteIconColor));
            }
        }
    }

    public string FavoriteIcon => IsFavorite ? "♥" : "♡";

    public Color FavoriteIconColor => IsFavorite ? Color.FromArgb("#F07CB8") : Color.FromArgb("#8179A3");

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }
    }

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

    public string CreatorLabel => SelectedMediaType?.Value switch
    {
        MediaType.Movie or MediaType.Series => T("Edit.Label.Creator.Director"),
        MediaType.Book or MediaType.Manga or MediaType.Comic => T("Edit.Label.Creator.Author"),
        MediaType.Game => T("Edit.Label.Creator.Developer"),
        MediaType.Anime => T("Edit.Label.Creator.Studio"),
        _ => T("Edit.Label.Creator.Generic")
    };

    public string CreatorPlaceholder => T("Edit.Placeholder.Creator");

    public string PublisherLabel => T("Edit.Label.Publisher");

    public string PublisherPlaceholder => T("Edit.Placeholder.Publisher");

    public string SerialNumberLabel => T("Edit.Label.SerialNumber");

    public string SerialNumberPlaceholder => T("Edit.Placeholder.SerialNumber");

    public string CastLabel => T("Edit.Label.Cast");

    public string CastPlaceholder => T("Edit.Placeholder.Cast");

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

    public string AddCoverText => T("Edit.AddCover");

    public string CoverUrlLabel => T("Edit.Label.CoverUrl");

    public string CoverUrlPlaceholder => T("Edit.Placeholder.CoverUrl");

    public string NotesLabel => T("Edit.Label.Notes");

    public string NotesPlaceholder => T("Edit.Placeholder.Notes");

    public string FavoriteLabel => T("Edit.Label.Favorite");

    public string EssentialsSectionTitle => T("Edit.Section.Essentials");

    public string DetailsSectionTitle => T("Edit.Section.Details");

    public string ProgressSectionTitle => T("Edit.Section.Progress");

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
        _mediaCategoryId = item.MediaCategoryId;
        Creator = item.Creator ?? string.Empty;
        Publisher = item.Publisher ?? string.Empty;
        SerialNumber = item.SerialNumber ?? string.Empty;
        SetTagChips(item.TagNames.Count > 0 ? item.TagNames : null);
        SetCastChips(item.Cast);
        SelectedMediaType = MediaTypes.First(option => option.Value == item.MediaType);
        SelectedStatus = Statuses.First(option => option.Value == item.Status);
        Rating = item.Rating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        ProgressCurrent = item.ProgressCurrent.ToString(CultureInfo.InvariantCulture);
        ProgressTotal = item.ProgressTotal?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        StartDate = item.StartDate ?? DateTime.Today;
        HasStartDate = item.StartDate.HasValue;
        FinishDate = item.FinishDate ?? DateTime.Today;
        HasFinishDate = item.FinishDate.HasValue;
        ReleaseYear = item.ReleaseYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        CoverUrl = item.CoverUrl ?? string.Empty;
        Notes = item.Notes ?? string.Empty;
        IsFavorite = item.IsFavorite;
        LoadBookFields(item);
    }

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        ReloadOptions();
        ReloadBookOptions();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = string.Empty;

        if (!TryParseOptionalDecimal(Rating, out var parsedRating) ||
            !TryParseRequiredInt(ProgressCurrent, 0, out var parsedProgressCurrent) ||
            !TryParseOptionalInt(ProgressTotal, out var parsedProgressTotal) ||
            !TryParseOptionalInt(ReleaseYear, out var parsedReleaseYear) ||
            !TryBuildBookInputs(out var bookDetails, out var collection))
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
                    MediaCategoryId = _mediaCategoryId,
                    Tags = BuildTagsString(),
                    TagNames = TagChips.ToList(),
                    Genres = GenreChips.ToList(),
                    Creator = Creator,
                    Publisher = Publisher,
                    SerialNumber = SerialNumber,
                    Cast = BuildCastList(),
                    Contributions = BuildContributions(),
                    BookDetails = bookDetails,
                    Collection = collection,
                    Relations = BuildRelations(),
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
                    MediaCategoryId = _mediaCategoryId,
                    Tags = BuildTagsString(),
                    TagNames = TagChips.ToList(),
                    Genres = GenreChips.ToList(),
                    Creator = Creator,
                    Publisher = Publisher,
                    SerialNumber = SerialNumber,
                    Cast = BuildCastList(),
                    Contributions = BuildContributions(),
                    BookDetails = bookDetails,
                    Collection = collection,
                    Relations = BuildRelations(),
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

            await NavigateBackAsync("EditMediaItemViewModel.SaveAsync");
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
        await NavigateBackAsync("EditMediaItemViewModel.CancelAsync");
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }

    [RelayCommand]
    private void AddTag()
    {
        var tag = NewTagText.Trim();
        NewTagText = string.Empty;

        if (tag.Length == 0 || TagChips.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        TagChips.Add(tag);
    }

    [RelayCommand]
    private void RemoveTag(string? tag)
    {
        if (tag is not null)
        {
            TagChips.Remove(tag);
        }
    }

    [RelayCommand]
    private void AddCast()
    {
        var name = NewCastText.Trim();
        NewCastText = string.Empty;

        if (name.Length == 0 || CastChips.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        CastChips.Add(name);
    }

    [RelayCommand]
    private void RemoveCast(string? name)
    {
        if (name is not null)
        {
            CastChips.Remove(name);
        }
    }

    [RelayCommand]
    private void ClearStartDate()
    {
        HasStartDate = false;
    }

    [RelayCommand]
    private void ClearFinishDate()
    {
        HasFinishDate = false;
    }

    [RelayCommand]
    private async Task PickCoverAsync()
    {
        try
        {
#if WINDOWS
            var path = PickImageFileWindows();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }
#else
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = T("Edit.CoverPicker.Title"),
                FileTypes = ImageFileType
            });

            if (result is null)
            {
                return;
            }

            var path = result.FullPath;
#endif

            var storedPath = CopyCoverIntoAppStorage(path);
            CoverUrl = new Uri(storedPath).AbsoluteUri;
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, "EditMediaItemViewModel.PickCoverAsync");
        }
    }

    private static string CoversDirectory => Path.Combine(FileSystem.AppDataDirectory, "covers");

    private string CopyCoverIntoAppStorage(string sourcePath)
    {
        Directory.CreateDirectory(CoversDirectory);

        DeletePreviousStoredCover();

        var extension = Path.GetExtension(sourcePath);
        var destinationPath = Path.Combine(CoversDirectory, $"{Guid.NewGuid():N}{extension}");
        File.Copy(sourcePath, destinationPath, overwrite: true);
        return destinationPath;
    }

    private void DeletePreviousStoredCover()
    {
        if (!HasCoverUrl)
        {
            return;
        }

        try
        {
            var previousPath = new Uri(CoverUrl).LocalPath;
            if (previousPath.StartsWith(CoversDirectory, StringComparison.OrdinalIgnoreCase) && File.Exists(previousPath))
            {
                File.Delete(previousPath);
            }
        }
        catch
        {
            // Best-effort cleanup; a stray file in the covers cache is harmless.
        }
    }

#if WINDOWS
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int StructSize;
        public IntPtr Owner;
        public IntPtr Instance;
        public string? Filter;
        public string? CustomFilter;
        public int MaxCustFilter;
        public int FilterIndex;
        public string File;
        public int MaxFile;
        public string? FileTitle;
        public int MaxFileTitle;
        public string? InitialDir;
        public string? Title;
        public int Flags;
        public short FileOffset;
        public short FileExtension;
        public string? DefExt;
        public IntPtr CustData;
        public IntPtr Hook;
        public string? TemplateName;
        public IntPtr Reserved1;
        public int Reserved2;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetOpenFileNameW(ref OpenFileName openFileName);

    private const int OfnFileMustExist = 0x00001000;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnExplorer = 0x00080000;

    private static string? PickImageFileWindows()
    {
        var openFileName = new OpenFileName
        {
            StructSize = Marshal.SizeOf<OpenFileName>(),
            Filter = "Image files\0*.png;*.jpg;*.jpeg;*.webp;*.bmp\0All files\0*.*\0\0",
            File = new string('\0', 260),
            MaxFile = 260,
            Title = "Choose a cover image",
            Flags = OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnExplorer
        };

        return GetOpenFileNameW(ref openFileName) ? openFileName.File.TrimEnd('\0') : null;
    }
#endif

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

    private void SetTagChips(string? tags)
    {
        TagChips.Clear();
        NewTagText = string.Empty;

        if (string.IsNullOrWhiteSpace(tags))
        {
            return;
        }

        foreach (var tag in tags.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TagChips.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
            {
                TagChips.Add(tag);
            }
        }
    }

    private void SetTagChips(IReadOnlyList<string>? tags)
    {
        TagChips.Clear();
        NewTagText = string.Empty;
        foreach (var tag in tags ?? [])
        {
            if (!string.IsNullOrWhiteSpace(tag) && !TagChips.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
            {
                TagChips.Add(tag.Trim());
            }
        }
    }

    private string BuildTagsString()
    {
        return TagChips.Count == 0 ? string.Empty : string.Join(", ", TagChips);
    }

    private void SetCastChips(IReadOnlyList<string>? cast)
    {
        CastChips.Clear();
        NewCastText = string.Empty;

        if (cast is null)
        {
            return;
        }

        foreach (var name in cast)
        {
            if (!string.IsNullOrWhiteSpace(name) && !CastChips.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
            {
                CastChips.Add(name);
            }
        }
    }

    private List<string> BuildCastList()
    {
        return CastChips.ToList();
    }

    private void ResetFields()
    {
        ItemTitle = string.Empty;
        OriginalTitle = string.Empty;
        Description = string.Empty;
        Category = string.Empty;
        _mediaCategoryId = null;
        Creator = string.Empty;
        Publisher = string.Empty;
        SerialNumber = string.Empty;
        SetTagChips((IReadOnlyList<string>?)null);
        SetCastChips(null);
        SelectedMediaType = MediaTypes.First(option => option.Value == MediaType.Other);
        SelectedStatus = Statuses.First(option => option.Value == MediaStatus.Planned);
        Rating = string.Empty;
        ProgressCurrent = string.Empty;
        ProgressTotal = string.Empty;
        StartDate = DateTime.Today;
        HasStartDate = false;
        FinishDate = DateTime.Today;
        HasFinishDate = false;
        ReleaseYear = string.Empty;
        CoverUrl = string.Empty;
        Notes = string.Empty;
        IsFavorite = false;
        ErrorMessage = string.Empty;
        ResetBookFields();
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

    private static bool TryParseOptionalDecimal(string value, out decimal? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        return await _authService.GetCurrentUserIdAsync() ?? "local-user";
    }

    private static async Task NavigateBackAsync(string context)
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, context);
        }
    }
}
