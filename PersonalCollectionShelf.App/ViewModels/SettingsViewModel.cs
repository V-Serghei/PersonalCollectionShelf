using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Pages;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Infrastructure.Services.Sync;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISyncService _syncService;
    private readonly IMediaItemService _mediaItemService;
    private readonly IPeopleManagementService _peopleManagementService;
    private readonly IAuthService _authService;
    private readonly IAppearanceService _appearanceService;
    private readonly IGoogleAccountService _googleAccountService;
    private readonly IGoogleDriveBackupService _driveBackupService;
    private readonly CloudImageCompressor _cloudImageCompressor;
    private readonly ICloudAssetStore _cloudAssetStore;
    private bool _suppressLanguageChange;
    private bool _isDarkTheme;
    private double _backgroundBlur;
    private string _statusMessageKey = "Sync.Status.NotConfigured";
    private string? _appearanceStatusKey;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly FilePickerFileType JsonFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.WinUI, [".json"] },
        { DevicePlatform.Android, ["application/json"] }
    });

    public SettingsViewModel(
        ISyncService syncService,
        IMediaItemService mediaItemService,
        IPeopleManagementService peopleManagementService,
        IAuthService authService,
        IGoogleAccountService googleAccountService,
        IGoogleDriveBackupService driveBackupService,
        CloudImageCompressor cloudImageCompressor,
        ICloudAssetStore cloudAssetStore,
        ILocalizationService localizationService,
        IAppearanceService appearanceService)
        : base(localizationService)
    {
        _syncService = syncService;
        _mediaItemService = mediaItemService;
        _peopleManagementService = peopleManagementService;
        _authService = authService;
        _googleAccountService = googleAccountService;
        _driveBackupService = driveBackupService;
        _cloudImageCompressor = cloudImageCompressor;
        _cloudAssetStore = cloudAssetStore;
        _appearanceService = appearanceService;
        _isDarkTheme = _appearanceService.IsDarkTheme;
        _backgroundBlur = _appearanceService.BackgroundBlur;
        _appearanceService.AppearanceChanged += HandleAppearanceChanged;
        InitializeLanguageOptions();
        UpdateBackgroundImageDescription();
        StatusMessage = T(_statusMessageKey);
    }

    public ObservableCollection<LocalizedOption<string>> LanguageOptions { get; } = [];

    private LocalizedOption<string>? _selectedLanguageOption;

    private string _statusMessage = string.Empty;
    private string _appearanceStatusMessage = string.Empty;
    private string _backgroundImageDescription = string.Empty;
    private bool _hasBackgroundImage;

    public LocalizedOption<string>? SelectedLanguageOption
    {
        get => _selectedLanguageOption;
        set
        {
            if (SetProperty(ref _selectedLanguageOption, value))
            {
                OnSelectedLanguageOptionChanged(value);
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
            {
                _appearanceService.SetTheme(value);
                SetAppearanceStatus(value ? "Settings.Appearance.DarkEnabled" : "Settings.Appearance.LightEnabled");
                OnPropertyChanged(nameof(ThemeModeDescription));
                OnPropertyChanged(nameof(ThemeSwitchLabel));
            }
        }
    }

    public string ThemeModeDescription => T("Settings.Theme.ModeDescription");

    public string ThemeSwitchLabel => T(IsDarkTheme ? "Settings.Theme.Dark" : "Settings.Theme.Light");

    public double BackgroundBlur
    {
        get => _backgroundBlur;
        set
        {
            if (SetProperty(ref _backgroundBlur, value))
            {
                OnPropertyChanged(nameof(BackgroundBlurText));
                _appearanceService.SetBackgroundBlur(value);
            }
        }
    }

    public string BackgroundBlurText => $"{BackgroundBlur:0}";

    public string BackgroundImageDescription
    {
        get => _backgroundImageDescription;
        set => SetProperty(ref _backgroundImageDescription, value);
    }

    public bool HasBackgroundImage
    {
        get => _hasBackgroundImage;
        set => SetProperty(ref _hasBackgroundImage, value);
    }

    public string AppearanceStatusMessage
    {
        get => _appearanceStatusMessage;
        set => SetProperty(ref _appearanceStatusMessage, value);
    }

    public string PageTitle => T("Settings.Title");

    public string AddItemText => T("Library.AddButton");

    public string AppearanceSectionTitle => T("Settings.Appearance.Title");

    public string AccentColorTitle => T("Settings.Accent.Title");

    public string AccentColorDescription => T("Settings.Accent.Description");

    public string BackgroundSectionTitle => T("Settings.Background.Title");

    public string CustomBackgroundTitle => T("Settings.Background.CustomImage");

    public string ChooseBackgroundImageText => T("Settings.Background.ChooseImage");

    public string RemoveBackgroundText => T("Settings.Background.Remove");

    public string BackgroundBlurLabel => T("Settings.Background.Blur");

    public string DataImportSectionTitle => T("Settings.DataImport.Title");

    public string AccountSectionTitle => T("Settings.Account.Title");

    public string AccountDescription => IsAuthConfigured
        ? T("Settings.Account.Description")
        : T("Auth.Error.NotConfigured");

    public string SignInButtonText => T("Settings.Account.SignIn");

    public string SignOutButtonText => T("Settings.Account.SignOut");

    public string AccountSignedInLabel => T("Settings.Account.SignedIn");

    public bool IsAuthConfigured => _authService.IsConfigured;

    public bool IsAccountNotConfigured => !IsAuthConfigured;

    public bool IsAccountFormVisible => IsAuthConfigured && !IsSignedIn;

    private string? _signedInEmail;
    private bool _isSignedIn;
    private string? _accountStatusKey;
    private string _accountStatusMessage = string.Empty;

    public string? SignedInEmail
    {
        get => _signedInEmail;
        set => SetProperty(ref _signedInEmail, value);
    }

    public bool IsSignedIn
    {
        get => _isSignedIn;
        set
        {
            if (SetProperty(ref _isSignedIn, value))
            {
                OnPropertyChanged(nameof(IsAccountFormVisible));
            }
        }
    }

    public string AccountStatusMessage
    {
        get => _accountStatusMessage;
        set => SetProperty(ref _accountStatusMessage, value);
    }

    [RelayCommand]
    public async Task LoadAccountStateAsync()
    {
        IsSignedIn = await _authService.IsSignedInAsync();
        SignedInEmail = IsSignedIn ? await _authService.GetSignedInEmailAsync() : null;
    }

    [RelayCommand]
    private async Task OpenSignInAsync()
    {
        try
        {
            var page = new SignInPage();
            await Shell.Current.Navigation.PushModalAsync(page);
            await page.Completion;

            var wasSignedIn = IsSignedIn;
            await LoadAccountStateAsync();

            if (!wasSignedIn && IsSignedIn)
            {
                SetAccountStatus("Settings.Account.SignedInMessage");
            }
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, "SettingsViewModel.OpenSignInAsync");
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _googleAccountService.SignOutAsync();
        IsSignedIn = false;
        SignedInEmail = null;
        SetAccountStatus("Settings.Account.SignedOutMessage");
    }

    private void SetAccountStatus(string? key)
    {
        _accountStatusKey = key;
        AccountStatusMessage = key is null ? string.Empty : T(key);
    }

    public string SyncSectionTitle => T("Settings.Sync.Title");

    public string SyncDescription => T("Settings.Sync.Description");

    public string SyncNowButtonText => T("Settings.Sync.Button");

    public string DriveBackupButtonText => T("Settings.DriveBackup.Button");

    public string DriveRestoreButtonText => T("Settings.DriveRestore.Button");

    public string DriveBackupDescription => T("Settings.DriveBackup.Description");

    public string ExportImportSectionTitle => T("Settings.ExportImport.Title");

    public string ExportButtonText => T("Settings.Export.Button");

    public string ImportButtonText => T("Settings.Import.Button");

    public string ExportDescriptionText => T("Settings.Export.Description");

    public string ImportDescriptionText => T("Settings.Import.Description");

    public string ComingSoonText => T("Settings.Import.ComingSoon");

    public string ImportLetterboxdDescription => T("Settings.Import.Letterboxd.Description");

    public string ImportGoodreadsDescription => T("Settings.Import.Goodreads.Description");

    public string ImportSteamDescription => T("Settings.Import.Steam.Description");

    public string ImportMalDescription => T("Settings.Import.Mal.Description");

    public string ThemeSectionTitle => T("Settings.Theme.Title");

    public string ThemeDescription => T("Settings.Theme.Description");

    public string LanguageSectionTitle => T("Settings.Language.Title");

    public string LanguageDescription => T("Settings.Language.Description");

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        StatusMessage = T(_statusMessageKey);

        if (_accountStatusKey is not null)
        {
            AccountStatusMessage = T(_accountStatusKey);
        }

        if (_appearanceStatusKey is not null)
        {
            AppearanceStatusMessage = T(_appearanceStatusKey);
        }

        InitializeLanguageOptions();
        UpdateBackgroundImageDescription();
    }

    private void OnSelectedLanguageOptionChanged(LocalizedOption<string>? value)
    {
        if (!_suppressLanguageChange &&
            value is not null &&
            !string.Equals(LocalizationService.CurrentLanguage, value.Value, StringComparison.OrdinalIgnoreCase))
        {
            LocalizationService.SetLanguage(value.Value);
        }
    }

    [RelayCommand]
    public async Task LoadSyncStatusAsync()
    {
        var status = await _syncService.GetSyncStatusAsync();
        _statusMessageKey = status.MessageKey;
        StatusMessage = T(_statusMessageKey);
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _syncService.RequestSyncAsync();
            if (await _googleAccountService.GetDriveAccessTokenAsync() is not null)
            {
                var backup = await BuildExportDocumentAsync(compressImages: true);
                await _driveBackupService.UploadBackupAsync(
                    $"personal-collection-shelf-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip",
                    CreateBackupArchive(backup));
            }
            _statusMessageKey = "Sync.Status.Requested";
            StatusMessage = T(_statusMessageKey);
        }
        catch (Exception exception)
        {
            StatusMessage = T("Sync.Status.Failed");
            await CrashReporter.ReportAsync(exception, "SettingsViewModel.SyncNowAsync");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BackupToDriveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var document = await BuildExportDocumentAsync(compressImages: true);
            var archive = CreateBackupArchive(document);
            var name = $"personal-collection-shelf-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
            await _driveBackupService.UploadBackupAsync(name, archive);
            StatusMessage = T("Settings.DriveBackup.Completed");
        }
        catch (Exception exception)
        {
            StatusMessage = T("Settings.DriveBackup.Failed");
            await CrashReporter.ReportAsync(exception, "SettingsViewModel.BackupToDriveAsync");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var document = await BuildExportDocumentAsync();

        var exportsDirectory = Path.Combine(FileSystem.AppDataDirectory, "exports");
        Directory.CreateDirectory(exportsDirectory);

        var fileName = $"personal-collection-shelf-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(exportsDirectory, fileName);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);

        StatusMessage = string.Format(T("Settings.Export.Completed"), document.Items.Count, filePath);

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = T("Settings.Export.ShareTitle"),
            File = new ShareFile(filePath)
        });
    }

    private async Task<LibraryExportDocument> BuildExportDocumentAsync(bool compressImages = false)
    {
        var userId = await GetCurrentUserIdAsync();
        var items = await _mediaItemService.GetLibraryAsync(userId);
        return new LibraryExportDocument
        {
            Items = items,
            People = await BuildPersonExportsAsync(userId, compressImages),
            Covers = await BuildCoverExportsAsync(items, compressImages)
        };
    }

    private static byte[] CreateBackupArchive(LibraryExportDocument document)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        var checksum = Convert.ToHexString(SHA256.HashData(json)).ToLowerInvariant();
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var dataEntry = archive.CreateEntry("library.json", CompressionLevel.Optimal);
            using (var stream = dataEntry.Open())
            {
                stream.Write(json);
            }

            var checksumEntry = archive.CreateEntry("sha256.txt", CompressionLevel.NoCompression);
            using var writer = new StreamWriter(checksumEntry.Open(), Encoding.UTF8);
            writer.Write($"{checksum}  library.json");
        }

        return output.ToArray();
    }

    private async Task<IReadOnlyList<PortableMediaCover>> BuildCoverExportsAsync(
        IReadOnlyList<MediaItemDto> items,
        bool compressImages)
    {
        var covers = new List<PortableMediaCover>();
        foreach (var item in items)
        {
            var path = ResolveFilePath(item.CoverUrl);
            if (path is null) continue;
            var image = await ReadPortableImageAsync(path, compressImages);
            covers.Add(new PortableMediaCover
            {
                MediaItemId = item.Id,
                Extension = image.Extension,
                DataBase64 = image.DataBase64,
                CloudObjectName = image.CloudObjectName
            });
        }

        return covers;
    }

    private static string? ResolveFilePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile) value = uri.LocalPath;
        return File.Exists(value) ? value : null;
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var result = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = T("Settings.Import.PickerTitle"),
            FileTypes = JsonFileType
        });

        if (result is null)
        {
            return;
        }

        LibraryExportDocument? document;
        try
        {
            await using var stream = await result.OpenReadAsync();
            document = await JsonSerializer.DeserializeAsync<LibraryExportDocument>(stream, JsonOptions);
        }
        catch (JsonException)
        {
            StatusMessage = T("Settings.Import.InvalidFile");
            return;
        }

        if (document is null)
        {
            StatusMessage = T("Settings.Import.InvalidFile");
            return;
        }

        await ImportDocumentAsync(document);
    }

    [RelayCommand]
    private async Task RestoreFromDriveAsync()
    {
        if (IsBusy) return;
        var confirmed = Shell.Current is not null && await Shell.Current.DisplayAlertAsync(
            T("Settings.DriveRestore.ConfirmTitle"),
            T("Settings.DriveRestore.ConfirmMessage"),
            T("Settings.DriveRestore.Confirm"),
            T("Common.Cancel"));
        if (!confirmed) return;
        IsBusy = true;
        try
        {
            var backup = await _driveBackupService.DownloadLatestBackupAsync();
            if (backup is null)
            {
                StatusMessage = T("Settings.DriveRestore.NotFound");
                return;
            }

            using var input = new MemoryStream(backup.Content);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read);
            var dataEntry = archive.GetEntry("library.json")
                ?? throw new InvalidDataException("Backup does not contain library.json.");
            await using var dataStream = dataEntry.Open();
            using var buffer = new MemoryStream();
            await dataStream.CopyToAsync(buffer);
            var json = buffer.ToArray();

            var checksumEntry = archive.GetEntry("sha256.txt");
            if (checksumEntry is not null)
            {
                using var reader = new StreamReader(checksumEntry.Open(), Encoding.UTF8);
                var expected = (await reader.ReadToEndAsync()).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                var actual = Convert.ToHexString(SHA256.HashData(json)).ToLowerInvariant();
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Backup checksum does not match.");
                }
            }

            var document = JsonSerializer.Deserialize<LibraryExportDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("Backup data is invalid.");
            await ImportDocumentAsync(document);
            StatusMessage = T("Settings.DriveRestore.Completed");
        }
        catch (Exception exception)
        {
            StatusMessage = T("Settings.DriveRestore.Failed");
            await CrashReporter.ReportAsync(exception, "SettingsViewModel.RestoreFromDriveAsync");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ImportDocumentAsync(LibraryExportDocument document)
    {

        var userId = await GetCurrentUserIdAsync();
        var imported = 0;
        var updated = 0;

        foreach (var exportedPerson in document.People)
        {
            var person = exportedPerson.Person;
            var saved = await _peopleManagementService.SaveAsync(new SavePersonRequest
            {
                Id = person.Id,
                UserId = userId,
                Name = person.Name,
                FirstName = person.FirstName,
                MiddleName = person.MiddleName,
                LastName = person.LastName,
                PenName = person.PenName,
                SortName = person.SortName,
                BirthYear = person.BirthYear,
                DeathYear = person.DeathYear,
                Country = person.Country,
                PlaceOfBirth = person.PlaceOfBirth,
                Gender = person.Gender,
                OfficialWebsite = person.OfficialWebsite,
                Tagline = person.Tagline,
                Description = person.Description,
                Notes = person.Notes,
                Professions = person.Professions
            });

            foreach (var portablePhoto in exportedPerson.Photos)
            {
                var photoBytes = await GetPortableImageBytesAsync(
                    portablePhoto.DataBase64,
                    portablePhoto.CloudObjectName);
                if (photoBytes is null) continue;
                var extension = NormalizePhotoExtension(portablePhoto.Extension);
                var directory = Path.Combine(FileSystem.AppDataDirectory, "person-photos");
                Directory.CreateDirectory(directory);
                var photoPath = Path.Combine(directory, $"{portablePhoto.Id:N}{extension}");
                if (!File.Exists(photoPath))
                {
                    await File.WriteAllBytesAsync(photoPath, photoBytes);
                }
                await _peopleManagementService.AddPhotoAsync(new AddPersonPhotoRequest
                {
                    Id = portablePhoto.Id,
                    UserId = userId,
                    PersonId = saved.Id,
                    FilePath = photoPath,
                    Caption = portablePhoto.Caption
                });
                if (portablePhoto.IsPrimary)
                {
                    await _peopleManagementService.SetPrimaryPhotoAsync(userId, saved.Id, portablePhoto.Id);
                }
            }
        }

        var importedRelations = new HashSet<Guid>();
        foreach (var exportedPerson in document.People)
        {
            foreach (var relation in exportedPerson.Person.Relations.Where(relation => importedRelations.Add(relation.Id)))
            {
                await _peopleManagementService.SaveRelationAsync(new SavePersonRelationRequest
                {
                    Id = relation.Id,
                    UserId = userId,
                    PersonId = exportedPerson.Person.Id,
                    RelatedPersonId = relation.RelatedPersonId,
                    Kind = relation.Kind,
                    InverseKind = relation.InverseKind,
                    StartDate = relation.StartDate,
                    EndDate = relation.EndDate,
                    Notes = relation.Notes
                });
            }
        }

        foreach (var exportedItem in document.Items)
        {
            var item = exportedItem;
            var portableCover = document.Covers.FirstOrDefault(cover => cover.MediaItemId == item.Id);
            if (portableCover is not null)
            {
                var coverBytes = await GetPortableImageBytesAsync(
                    portableCover.DataBase64,
                    portableCover.CloudObjectName);
                if (coverBytes is not null)
                {
                    var directory = Path.Combine(FileSystem.AppDataDirectory, "covers");
                    Directory.CreateDirectory(directory);
                    var coverPath = Path.Combine(directory, $"{item.Id:N}{NormalizePhotoExtension(portableCover.Extension)}");
                    if (!File.Exists(coverPath))
                    {
                        await File.WriteAllBytesAsync(coverPath, coverBytes);
                    }
                    item = item with { CoverUrl = new Uri(coverPath).AbsoluteUri };
                }
            }

            var existing = item.Id == Guid.Empty
                ? null
                : await _mediaItemService.GetMediaItemAsync(item.Id, userId);

            if (existing is null)
            {
                await _mediaItemService.CreateMediaItemAsync(ToCreateRequest(item, userId));
                imported++;
            }
            else
            {
                await _mediaItemService.UpdateMediaItemAsync(ToUpdateRequest(item, userId));
                updated++;
            }
        }

        StatusMessage = string.Format(T("Settings.Import.Completed"), imported, updated);
    }

    private async Task<IReadOnlyList<PersonExportDocument>> BuildPersonExportsAsync(
        string userId,
        bool compressImages = false)
    {
        var result = new List<PersonExportDocument>();
        foreach (var summary in await _peopleManagementService.GetCatalogAsync(userId))
        {
            var person = await _peopleManagementService.GetAsync(userId, summary.Id);
            if (person is null) continue;
            var photos = new List<PortablePersonPhoto>();
            foreach (var photo in person.Photos)
            {
                if (!File.Exists(photo.FilePath)) continue;
                var image = await ReadPortableImageAsync(photo.FilePath, compressImages);
                photos.Add(new PortablePersonPhoto
                {
                    Id = photo.Id == Guid.Empty ? Guid.NewGuid() : photo.Id,
                    Caption = photo.Caption,
                    IsPrimary = photo.IsPrimary,
                    SortOrder = photo.SortOrder,
                    Extension = image.Extension,
                    DataBase64 = image.DataBase64,
                    CloudObjectName = image.CloudObjectName
                });
            }
            result.Add(new PersonExportDocument { Person = person, Photos = photos });
        }
        return result;
    }

    private async Task<PortableImageData> ReadPortableImageAsync(string path, bool compress)
    {
        if (!compress)
        {
            return new PortableImageData(
                NormalizePhotoExtension(Path.GetExtension(path)),
                Convert.ToBase64String(await File.ReadAllBytesAsync(path)));
        }

        var compressed = await _cloudImageCompressor.CompressAsync(path, CancellationToken.None);
        var objectName = $"{compressed.SourceHash}.webp";
        await _cloudAssetStore.UploadAsync(
            objectName,
            compressed.Bytes,
            "image/webp",
            CancellationToken.None);
        return new PortableImageData(".webp", string.Empty, objectName);
    }

    private async Task<byte[]?> GetPortableImageBytesAsync(string? dataBase64, string? cloudObjectName)
    {
        if (!string.IsNullOrWhiteSpace(dataBase64))
        {
            return Convert.FromBase64String(dataBase64);
        }

        return string.IsNullOrWhiteSpace(cloudObjectName)
            ? null
            : await _cloudAssetStore.DownloadAsync(cloudObjectName, CancellationToken.None);
    }

    private sealed record PortableImageData(
        string Extension,
        string DataBase64,
        string? CloudObjectName = null);

    private static string NormalizePhotoExtension(string? extension)
    {
        var normalized = extension?.ToLowerInvariant();
        return normalized is ".jpg" or ".jpeg" or ".png" or ".webp" ? normalized : ".jpg";
    }

    [RelayCommand]
    private async Task CreateMediaItemAsync()
    {
        try
        {
            await AppNavigation.OpenEditMediaItemAsync();
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, "SettingsViewModel.CreateMediaItemAsync");
        }
    }

    [RelayCommand]
    private Task OpenPeopleAsync() => AppNavigation.OpenPeopleAsync();

    [RelayCommand]
    private Task OpenCategoriesAsync() => AppNavigation.OpenCategoriesAsync();

    public string LibraryDataSectionTitle => T("Settings.LibraryData.Title");
    public string PeopleEditorTitle => T("Settings.People.Title");
    public string PeopleEditorDescription => T("Settings.People.Description");
    public string CategoryEditorTitle => T("Settings.Categories.Title");
    public string CategoryEditorDescription => T("Settings.Categories.Description");

    [RelayCommand]
    private async Task PickBackgroundImageAsync()
    {
        try
        {
            var picked = await _appearanceService.PickBackgroundImageAsync();
            if (!picked)
            {
                return;
            }

            UpdateBackgroundImageDescription();
            SetAppearanceStatus("Settings.Appearance.BackgroundUpdated");
        }
        catch (Exception exception)
        {
            SetAppearanceStatus("Settings.Appearance.BackgroundLoadError");
            await CrashReporter.ReportAsync(exception, "SettingsViewModel.PickBackgroundImageAsync");
        }
    }

    [RelayCommand]
    private void ClearBackgroundImage()
    {
        _appearanceService.ClearBackgroundImage();
        UpdateBackgroundImageDescription();
        SetAppearanceStatus("Settings.Appearance.BackgroundCleared");
    }

    [RelayCommand]
    private void SetAccentColor(string colorHex)
    {
        _appearanceService.SetAccentColor(colorHex);
        SetAppearanceStatus("Settings.Appearance.AccentUpdated");
    }

    private void UpdateBackgroundImageDescription()
    {
        var path = _appearanceService.BackgroundImagePath;
        HasBackgroundImage = !string.IsNullOrWhiteSpace(path);
        BackgroundImageDescription = string.IsNullOrWhiteSpace(path)
            ? T("Settings.Background.None")
            : Path.GetFileName(path);
    }

    private void SetAppearanceStatus(string key)
    {
        _appearanceStatusKey = key;
        AppearanceStatusMessage = T(key);
    }

    private void HandleAppearanceChanged(object? sender, EventArgs e)
    {
        if (_isDarkTheme != _appearanceService.IsDarkTheme)
        {
            _isDarkTheme = _appearanceService.IsDarkTheme;
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(ThemeSwitchLabel));
        }

        if (Math.Abs(_backgroundBlur - _appearanceService.BackgroundBlur) > 0.1)
        {
            _backgroundBlur = _appearanceService.BackgroundBlur;
            OnPropertyChanged(nameof(BackgroundBlur));
            OnPropertyChanged(nameof(BackgroundBlurText));
        }

        UpdateBackgroundImageDescription();
    }

    private void InitializeLanguageOptions()
    {
        _suppressLanguageChange = true;
        try
        {
            LanguageOptions.Clear();
            LanguageOptions.Add(new LocalizedOption<string>("en", T("Language.English")));
            LanguageOptions.Add(new LocalizedOption<string>("ru", T("Language.Russian")));
            SelectedLanguageOption = LanguageOptions.First(option => option.Value == LocalizationService.CurrentLanguage);
        }
        finally
        {
            _suppressLanguageChange = false;
        }
    }

    private static CreateMediaItemRequest ToCreateRequest(MediaItemDto item, string userId)
    {
        return new CreateMediaItemRequest
        {
            Id = item.Id,
            UserId = userId,
            Title = item.Title,
            OriginalTitle = item.OriginalTitle,
            Description = item.Description,
            Category = item.Category,
            MediaCategoryId = item.MediaCategoryId,
            Tags = item.Tags,
            TagNames = item.TagNames,
            Genres = item.Genres,
            Contributions = item.Contributions.Select(value => new PersonCreditInput
            {
                PersonId = value.PersonId,
                CreditRoleId = value.CreditRoleId,
                Name = value.PersonName,
                Role = value.Role,
                SortOrder = value.SortOrder,
                Details = value.Details,
                CreditedAs = value.CreditedAs
            }).ToList(),
            StudioCredits = item.StudioCredits.Select(value => new StudioCreditInput { StudioId = value.StudioId, Name = value.StudioName, Role = value.Role, SortOrder = value.SortOrder }).ToList(),
            BookDetails = ToBookDetailsInput(item.BookDetails),
            MovieDetails = item.MovieDetails is null ? null : new MovieDetailsInput { RuntimeMinutes = item.MovieDetails.RuntimeMinutes, OriginalLanguage = item.MovieDetails.OriginalLanguage, Language = item.MovieDetails.Language, CountryOfOrigin = item.MovieDetails.CountryOfOrigin, AgeRating = item.MovieDetails.AgeRating },
            EpisodicDetails = item.EpisodicDetails,
            GraphicPublicationDetails = item.GraphicPublicationDetails,
            GameDetails = item.GameDetails,
            Collection = item.Collection is null ? null : new CollectionMembershipInput
            {
                Name = item.Collection.Name,
                Kind = item.Collection.Kind,
                Position = item.Collection.Position
            },
            Relations = item.Relations.Where(value => value.IsOutgoing).Select(value => new MediaRelationInput
            {
                RelatedItemId = value.RelatedItemId,
                Kind = value.Kind,
                Notes = value.Notes
            }).ToList(),
            MediaType = item.MediaType,
            Status = item.Status,
            Rating = item.Rating,
            ProgressCurrent = item.ProgressCurrent,
            ProgressTotal = item.ProgressTotal,
            StartDate = item.StartDate,
            FinishDate = item.FinishDate,
            ReleaseYear = item.ReleaseYear,
            CoverUrl = item.CoverUrl,
            TmdbId = item.TmdbId,
            ImdbId = item.ImdbId,
            KinopoiskId = item.KinopoiskId,
            TmdbRating = item.TmdbRating,
            TmdbVoteCount = item.TmdbVoteCount,
            ImdbRating = item.ImdbRating,
            ImdbVoteCount = item.ImdbVoteCount,
            KinopoiskRating = item.KinopoiskRating,
            KinopoiskVoteCount = item.KinopoiskVoteCount,
            ExternalRatingsUpdatedAt = item.ExternalRatingsUpdatedAt,
            Notes = item.Notes,
            IsFavorite = item.IsFavorite
        };
    }

    private static UpdateMediaItemRequest ToUpdateRequest(MediaItemDto item, string userId)
    {
        return new UpdateMediaItemRequest
        {
            Id = item.Id,
            UserId = userId,
            Title = item.Title,
            OriginalTitle = item.OriginalTitle,
            Description = item.Description,
            Category = item.Category,
            MediaCategoryId = item.MediaCategoryId,
            Tags = item.Tags,
            TagNames = item.TagNames,
            Genres = item.Genres,
            Contributions = item.Contributions.Select(value => new PersonCreditInput
            {
                PersonId = value.PersonId,
                CreditRoleId = value.CreditRoleId,
                Name = value.PersonName,
                Role = value.Role,
                SortOrder = value.SortOrder,
                Details = value.Details,
                CreditedAs = value.CreditedAs
            }).ToList(),
            StudioCredits = item.StudioCredits.Select(value => new StudioCreditInput { StudioId = value.StudioId, Name = value.StudioName, Role = value.Role, SortOrder = value.SortOrder }).ToList(),
            BookDetails = ToBookDetailsInput(item.BookDetails),
            MovieDetails = item.MovieDetails is null ? null : new MovieDetailsInput { RuntimeMinutes = item.MovieDetails.RuntimeMinutes, OriginalLanguage = item.MovieDetails.OriginalLanguage, Language = item.MovieDetails.Language, CountryOfOrigin = item.MovieDetails.CountryOfOrigin, AgeRating = item.MovieDetails.AgeRating },
            EpisodicDetails = item.EpisodicDetails,
            GraphicPublicationDetails = item.GraphicPublicationDetails,
            GameDetails = item.GameDetails,
            Collection = item.Collection is null ? null : new CollectionMembershipInput
            {
                Name = item.Collection.Name,
                Kind = item.Collection.Kind,
                Position = item.Collection.Position
            },
            Relations = item.Relations.Where(value => value.IsOutgoing).Select(value => new MediaRelationInput
            {
                RelatedItemId = value.RelatedItemId,
                Kind = value.Kind,
                Notes = value.Notes
            }).ToList(),
            MediaType = item.MediaType,
            Status = item.Status,
            Rating = item.Rating,
            ProgressCurrent = item.ProgressCurrent,
            ProgressTotal = item.ProgressTotal,
            StartDate = item.StartDate,
            FinishDate = item.FinishDate,
            ReleaseYear = item.ReleaseYear,
            CoverUrl = item.CoverUrl,
            TmdbId = item.TmdbId,
            ImdbId = item.ImdbId,
            KinopoiskId = item.KinopoiskId,
            TmdbRating = item.TmdbRating,
            TmdbVoteCount = item.TmdbVoteCount,
            ImdbRating = item.ImdbRating,
            ImdbVoteCount = item.ImdbVoteCount,
            KinopoiskRating = item.KinopoiskRating,
            KinopoiskVoteCount = item.KinopoiskVoteCount,
            ExternalRatingsUpdatedAt = item.ExternalRatingsUpdatedAt,
            Notes = item.Notes,
            IsFavorite = item.IsFavorite
        };
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        return await _authService.GetCurrentUserIdAsync() ?? "local-user";
    }

    private static BookDetailsInput? ToBookDetailsInput(BookDetailsDto? value)
    {
        return value is null ? null : new BookDetailsInput
        {
            Subtitle = value.Subtitle,
            Publisher = value.Publisher,
            Edition = value.Edition,
            EditionNumber = value.EditionNumber,
            EditionYear = value.EditionYear,
            OriginalPublicationYear = value.OriginalPublicationYear,
            TranslationYear = value.TranslationYear,
            OriginalLanguage = value.OriginalLanguage,
            Language = value.Language,
            PageCount = value.PageCount,
            Isbn10 = value.Isbn10,
            Isbn13 = value.Isbn13,
            Format = value.Format,
            Binding = value.Binding,
            CountryOfOrigin = value.CountryOfOrigin,
            AgeRating = value.AgeRating
        };
    }
}
