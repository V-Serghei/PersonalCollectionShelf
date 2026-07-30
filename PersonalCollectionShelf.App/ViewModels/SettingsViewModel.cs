using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Diagnostics;
using System.Net;
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
    private readonly IBackgroundOperationNotifier _backgroundOperationNotifier;
    private bool _suppressLanguageChange;
    private bool _isDarkTheme;
    private double _backgroundBlur;
    private int _libraryGridColumnCount;
    private int _peopleGridColumnCount;
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
        IBackgroundOperationNotifier backgroundOperationNotifier,
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
        _backgroundOperationNotifier = backgroundOperationNotifier;
        _appearanceService = appearanceService;
        _isDarkTheme = _appearanceService.IsDarkTheme;
        _backgroundBlur = _appearanceService.BackgroundBlur;
        _libraryGridColumnCount = Math.Clamp(Preferences.Get("library.gridColumnCount", 2), 1, 4);
        _peopleGridColumnCount = Math.Clamp(Preferences.Get("people.gridColumnCount", 3), 1, 4);
        _appearanceService.AppearanceChanged += HandleAppearanceChanged;
        InitializeLanguageOptions();
        UpdateBackgroundImageDescription();
        StatusMessage = T(_statusMessageKey);
    }

    public ObservableCollection<LocalizedOption<string>> LanguageOptions { get; } = [];
    public IReadOnlyList<int> GridColumnOptions { get; } = [1, 2, 3, 4];

    private LocalizedOption<string>? _selectedLanguageOption;

    private string _statusMessage = string.Empty;
    private string _appearanceStatusMessage = string.Empty;
    private string _backgroundImageDescription = string.Empty;
    private bool _hasBackgroundImage;
    private bool _isSyncing;
    private double _syncProgress;
    private string _syncProgressPercentText = "0%";
    private string _syncProgressEtaText = string.Empty;
    private bool _hasSyncProgress;
    private bool _isDriveBackupRunning;
    private bool _isDriveRestoreRunning;
    private double _driveProgress;
    private string _driveProgressPercentText = "0%";
    private string _driveProgressEtaText = string.Empty;
    private bool _hasDriveProgress;
    private readonly ProgressEtaEstimator _syncEtaEstimator = new();
    private readonly ProgressEtaEstimator _driveEtaEstimator = new();
    private CancellationTokenSource? _syncEtaTicker;
    private CancellationTokenSource? _driveEtaTicker;
    private int _syncProgressCompleted;
    private int _syncProgressTotal;
    private CancellationTokenSource? _activeCloudOperationCts;
    private bool _hasActiveCloudOperation;
    private bool _isCancellingCloudOperation;
    private bool _notificationFinalized;

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

    public bool IsSyncing
    {
        get => _isSyncing;
        private set
        {
            if (SetProperty(ref _isSyncing, value))
            {
                OnPropertyChanged(nameof(IsNotSyncing));
                OnPropertyChanged(nameof(SyncNowButtonText));
            }
        }
    }

    public bool IsNotSyncing => !IsSyncing;

    public bool HasActiveCloudOperation
    {
        get => _hasActiveCloudOperation;
        private set
        {
            if (!SetProperty(ref _hasActiveCloudOperation, value)) return;
            OnPropertyChanged(nameof(CanCancelCloudOperation));
        }
    }

    public bool CanCancelCloudOperation => HasActiveCloudOperation && !_isCancellingCloudOperation;

    public string CancelCloudOperationText => T("Common.Cancel");

    public double SyncProgress
    {
        get => _syncProgress;
        private set => SetProperty(ref _syncProgress, Math.Clamp(value, 0, 1));
    }

    public string SyncProgressPercentText
    {
        get => _syncProgressPercentText;
        private set => SetProperty(ref _syncProgressPercentText, value);
    }

    public bool HasSyncProgress
    {
        get => _hasSyncProgress;
        private set => SetProperty(ref _hasSyncProgress, value);
    }

    public string SyncProgressEtaText
    {
        get => _syncProgressEtaText;
        private set => SetProperty(ref _syncProgressEtaText, value);
    }

    public double DriveProgress
    {
        get => _driveProgress;
        private set => SetProperty(ref _driveProgress, Math.Clamp(value, 0, 1));
    }

    public string DriveProgressPercentText
    {
        get => _driveProgressPercentText;
        private set => SetProperty(ref _driveProgressPercentText, value);
    }

    public string DriveProgressEtaText
    {
        get => _driveProgressEtaText;
        private set => SetProperty(ref _driveProgressEtaText, value);
    }

    public bool HasDriveProgress
    {
        get => _hasDriveProgress;
        private set => SetProperty(ref _hasDriveProgress, value);
    }

    public bool IsDriveBackupRunning
    {
        get => _isDriveBackupRunning;
        private set
        {
            if (SetProperty(ref _isDriveBackupRunning, value))
            {
                OnPropertyChanged(nameof(DriveBackupButtonText));
            }
        }
    }

    public bool IsDriveRestoreRunning
    {
        get => _isDriveRestoreRunning;
        private set
        {
            if (SetProperty(ref _isDriveRestoreRunning, value))
            {
                OnPropertyChanged(nameof(DriveRestoreButtonText));
            }
        }
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

    public int LibraryGridColumnCount
    {
        get => _libraryGridColumnCount;
        set
        {
            var normalized = Math.Clamp(value, 1, 4);
            if (SetProperty(ref _libraryGridColumnCount, normalized))
            {
                Preferences.Set("library.gridColumnCount", normalized);
            }
        }
    }

    public int PeopleGridColumnCount
    {
        get => _peopleGridColumnCount;
        set
        {
            var normalized = Math.Clamp(value, 1, 4);
            if (SetProperty(ref _peopleGridColumnCount, normalized))
            {
                Preferences.Set("people.gridColumnCount", normalized);
            }
        }
    }

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

    public string GridDensityTitle => T("Settings.GridDensity.Title");

    public string LibraryGridDensityLabel => T("Settings.GridDensity.Library");

    public string PeopleGridDensityLabel => T("Settings.GridDensity.People");

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

    public string SyncNowButtonText => T(IsSyncing ? "Sync.Status.InProgressButton" : "Settings.Sync.Button");

    public string DriveBackupButtonText => T(IsDriveBackupRunning
        ? "Settings.DriveBackup.InProgressButton"
        : "Settings.DriveBackup.Button");

    public string DriveRestoreButtonText => T(IsDriveRestoreRunning
        ? "Settings.DriveRestore.InProgressButton"
        : "Settings.DriveRestore.Button");

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
        OnPropertyChanged(nameof(CancelCloudOperationText));
        StatusMessage = T(_statusMessageKey);

        if (_accountStatusKey is not null)
        {
            AccountStatusMessage = T(_accountStatusKey);
        }

        if (_appearanceStatusKey is not null)
        {
            AppearanceStatusMessage = T(_appearanceStatusKey);
        }

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
    private void CancelCloudOperation()
    {
        if (!CanCancelCloudOperation) return;
        _isCancellingCloudOperation = true;
        OnPropertyChanged(nameof(CanCancelCloudOperation));
        SetSyncStatus("CloudOperation.Cancelling");
        _backgroundOperationNotifier.Report(
            (int)Math.Round(Math.Max(SyncProgress, DriveProgress) * 100),
            StatusMessage);
        _activeCloudOperationCts?.Cancel();
    }

    private CancellationToken BeginCloudOperation(string initialMessageKey)
    {
        _activeCloudOperationCts?.Dispose();
        _activeCloudOperationCts = new CancellationTokenSource();
        _isCancellingCloudOperation = false;
        _notificationFinalized = false;
        HasActiveCloudOperation = true;
        OnPropertyChanged(nameof(CanCancelCloudOperation));
        _backgroundOperationNotifier.Start(T("App.Name"), T(initialMessageKey));
        return _activeCloudOperationCts.Token;
    }

    private void EndCloudOperation()
    {
        if (!_notificationFinalized)
        {
            _backgroundOperationNotifier.Stop();
        }
        _activeCloudOperationCts?.Dispose();
        _activeCloudOperationCts = null;
        _isCancellingCloudOperation = false;
        HasActiveCloudOperation = false;
        OnPropertyChanged(nameof(CanCancelCloudOperation));
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (IsBusy) return;
        var cancellationToken = BeginCloudOperation("Sync.Progress.Preparing");
        IsBusy = true;
        IsSyncing = true;
        HasSyncProgress = true;
        _syncEtaEstimator.Reset();
        _syncEtaTicker?.Cancel();
        _syncEtaTicker = new CancellationTokenSource();
        _ = RunEtaTickerAsync(_syncEtaEstimator, isSync: true, _syncEtaTicker.Token);
        UpdateSyncProgress(new SyncProgressDto(0, "Sync.Progress.Preparing"));
        SetSyncStatus("Sync.Status.Started");
        UserNotification.Show(StatusMessage);
        CrashReporter.LogMessage("SettingsViewModel.SyncNowAsync", "Synchronization started.");
        try
        {
            await Task.Yield();
            var progress = new Progress<SyncProgressDto>(UpdateSyncProgress);
            var result = await _syncService.RequestSyncAsync(progress, cancellationToken);
            UpdateSyncProgress(new SyncProgressDto(100, result.HasWarnings ? "Sync.Progress.CompleteWithWarnings" : "Sync.Progress.Complete"));
            SetSyncStatus(result.HasWarnings ? "Sync.Status.CompletedWithWarnings" : "Sync.Status.Completed");
            _backgroundOperationNotifier.Complete(StatusMessage);
            _notificationFinalized = true;
            UserNotification.Show(StatusMessage);
            CrashReporter.LogMessage("SettingsViewModel.SyncNowAsync", "Synchronization completed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetSyncStatus("CloudOperation.Cancelled");
            SyncProgressEtaText = T("Progress.Stopped");
            UserNotification.Show(StatusMessage);
        }
        catch (Exception exception)
        {
            SetSyncStatus(IsNetworkFailure(exception) ? "Sync.Status.NetworkFailed" : "Sync.Status.Failed");
            SyncProgressEtaText = T("Progress.Stopped");
            _backgroundOperationNotifier.Fail(StatusMessage);
            _notificationFinalized = true;
            UserNotification.Show(StatusMessage);
            CrashReporter.Log(exception, "SettingsViewModel.SyncNowAsync");
        }
        finally
        {
            _syncEtaTicker?.Cancel();
            IsSyncing = false;
            IsBusy = false;
            EndCloudOperation();
        }
    }

    private void SetSyncStatus(string key)
    {
        _statusMessageKey = key;
        StatusMessage = T(key);
    }

    private void UpdateSyncProgress(SyncProgressDto progress)
    {
        SyncProgress = progress.Percent / 100d;
        SyncProgressPercentText = $"{progress.Percent}%";
        _syncProgressCompleted = progress.Completed;
        _syncProgressTotal = progress.Total;
        SyncProgressEtaText = BuildProgressEtaText(
            _syncEtaEstimator.Update(progress.Percent),
            _syncProgressCompleted,
            _syncProgressTotal);
        SetSyncStatus(_isCancellingCloudOperation ? "CloudOperation.Cancelling" : progress.MessageKey);
        _backgroundOperationNotifier.Report(progress.Percent, StatusMessage);
    }

    private static bool IsNetworkFailure(Exception exception) =>
        exception is HttpRequestException or WebException or IOException ||
        exception.InnerException is not null && IsNetworkFailure(exception.InnerException);

    [RelayCommand]
    private async Task BackupToDriveAsync()
    {
        if (IsBusy) return;
        var cancellationToken = BeginCloudOperation("Settings.DriveBackup.Preparing");
        IsBusy = true;
        IsDriveBackupRunning = true;
        HasDriveProgress = true;
        _driveEtaEstimator.Reset();
        _driveEtaTicker?.Cancel();
        _driveEtaTicker = new CancellationTokenSource();
        _ = RunEtaTickerAsync(_driveEtaEstimator, isSync: false, _driveEtaTicker.Token);
        UpdateDriveProgress(0, "Settings.DriveBackup.Preparing");
        UserNotification.Show(StatusMessage);
        CrashReporter.LogMessage("SettingsViewModel.BackupToDriveAsync", "Google Drive backup started.");
        try
        {
            await Task.Yield();
            var document = await BuildExportDocumentAsync(
                compressImages: true,
                (percent, key) => UpdateDriveProgress(percent, key),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var archive = CreateBackupArchive(document);
            var name = $"personal-collection-shelf-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
            UpdateDriveProgress(74, "Settings.DriveBackup.Uploading");
            var transferProgress = new Progress<DriveTransferProgressDto>(value =>
            {
                var fraction = value.TotalBytes is > 0 ? value.TransferredBytes / (double)value.TotalBytes.Value : 0;
                UpdateDriveProgress(74 + (int)Math.Round(fraction * 24), "Settings.DriveBackup.Uploading");
            });
            await _driveBackupService.UploadBackupAsync(name, archive, transferProgress, cancellationToken);
            UpdateDriveProgress(100, "Settings.DriveBackup.Completed");
            _backgroundOperationNotifier.Complete(StatusMessage);
            _notificationFinalized = true;
            UserNotification.Show(StatusMessage);
            CrashReporter.LogMessage("SettingsViewModel.BackupToDriveAsync", "Google Drive backup completed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetSyncStatus("CloudOperation.Cancelled");
            DriveProgressEtaText = T("Progress.Stopped");
            UserNotification.Show(StatusMessage);
        }
        catch (Exception exception)
        {
            SetSyncStatus("Settings.DriveBackup.Failed");
            DriveProgressEtaText = T("Progress.Stopped");
            _backgroundOperationNotifier.Fail(StatusMessage);
            _notificationFinalized = true;
            UserNotification.Show(StatusMessage);
            await CrashReporter.ReportAsync(exception, "SettingsViewModel.BackupToDriveAsync");
        }
        finally
        {
            _driveEtaTicker?.Cancel();
            IsDriveBackupRunning = false;
            IsBusy = false;
            EndCloudOperation();
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

    private async Task<LibraryExportDocument> BuildExportDocumentAsync(
        bool compressImages = false,
        Action<int, string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Invoke(2, "Settings.DriveBackup.Preparing");
        cancellationToken.ThrowIfCancellationRequested();
        var userId = await GetCurrentUserIdAsync();
        var items = await _mediaItemService.GetLibraryAsync(userId);
        progress?.Invoke(6, "Settings.DriveBackup.Preparing");
        var people = await BuildPersonExportsAsync(userId, compressImages, (completed, total) =>
        {
            var fraction = total == 0 ? 1 : completed / (double)total;
            progress?.Invoke(6 + (int)Math.Round(fraction * 34), "Settings.DriveBackup.Preparing");
        }, cancellationToken);
        var covers = await BuildCoverExportsAsync(items, compressImages, (completed, total) =>
        {
            var fraction = total == 0 ? 1 : completed / (double)total;
            progress?.Invoke(40 + (int)Math.Round(fraction * 32), "Settings.DriveBackup.Preparing");
        }, cancellationToken);
        return new LibraryExportDocument
        {
            Items = items,
            People = people,
            Covers = covers
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
        bool compressImages,
        Action<int, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var covers = new ConcurrentBag<PortableMediaCover>();
        var completed = 0;
        await Parallel.ForEachAsync(
            items,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 3,
                CancellationToken = cancellationToken
            },
            async (item, token) =>
            {
                var path = ResolveFilePath(item.CoverUrl);
                if (path is not null)
                {
                    var image = await ReadPortableImageAsync(path, compressImages, token);
                    covers.Add(new PortableMediaCover
                    {
                        MediaItemId = item.Id,
                        Extension = image.Extension,
                        DataBase64 = image.DataBase64,
                        CloudObjectName = image.CloudObjectName
                    });
                }
                var current = Interlocked.Increment(ref completed);
                await MainThread.InvokeOnMainThreadAsync(() => progress?.Invoke(current, items.Count));
            });

        return covers.OrderBy(cover => cover.MediaItemId).ToList();
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
        var cancellationToken = BeginCloudOperation("Settings.DriveRestore.Downloading");
        IsBusy = true;
        IsDriveRestoreRunning = true;
        HasDriveProgress = true;
        _driveEtaEstimator.Reset();
        _driveEtaTicker?.Cancel();
        _driveEtaTicker = new CancellationTokenSource();
        _ = RunEtaTickerAsync(_driveEtaEstimator, isSync: false, _driveEtaTicker.Token);
        UpdateDriveProgress(0, "Settings.DriveRestore.Downloading");
        UserNotification.Show(StatusMessage);
        CrashReporter.LogMessage("SettingsViewModel.RestoreFromDriveAsync", "Google Drive restore started.");
        try
        {
            await Task.Yield();
            var transferProgress = new Progress<DriveTransferProgressDto>(value =>
            {
                var fraction = value.TotalBytes is > 0 ? value.TransferredBytes / (double)value.TotalBytes.Value : 0;
                UpdateDriveProgress(3 + (int)Math.Round(fraction * 52), "Settings.DriveRestore.Downloading");
            });
            var backup = await _driveBackupService.DownloadLatestBackupAsync(transferProgress, cancellationToken);
            if (backup is null)
            {
                SetSyncStatus("Settings.DriveRestore.NotFound");
                DriveProgressEtaText = T("Progress.Stopped");
                UserNotification.Show(StatusMessage);
                return;
            }

            using var input = new MemoryStream(backup.Content);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read);
            var dataEntry = archive.GetEntry("library.json")
                ?? throw new InvalidDataException("Backup does not contain library.json.");
            await using var dataStream = dataEntry.Open();
            using var buffer = new MemoryStream();
            await dataStream.CopyToAsync(buffer, cancellationToken);
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
            UpdateDriveProgress(60, "Settings.DriveRestore.Importing");
            await ImportDocumentAsync(document, (completed, total) =>
            {
                var fraction = total == 0 ? 1 : completed / (double)total;
                UpdateDriveProgress(60 + (int)Math.Round(fraction * 39), "Settings.DriveRestore.Importing");
            }, cancellationToken);
            UpdateDriveProgress(100, "Settings.DriveRestore.Completed");
            _backgroundOperationNotifier.Complete(StatusMessage);
            _notificationFinalized = true;
            UserNotification.Show(StatusMessage);
            CrashReporter.LogMessage("SettingsViewModel.RestoreFromDriveAsync", "Google Drive restore completed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetSyncStatus("CloudOperation.Cancelled");
            DriveProgressEtaText = T("Progress.Stopped");
            UserNotification.Show(StatusMessage);
        }
        catch (Exception exception)
        {
            SetSyncStatus("Settings.DriveRestore.Failed");
            DriveProgressEtaText = T("Progress.Stopped");
            _backgroundOperationNotifier.Fail(StatusMessage);
            _notificationFinalized = true;
            UserNotification.Show(StatusMessage);
            await CrashReporter.ReportAsync(exception, "SettingsViewModel.RestoreFromDriveAsync");
        }
        finally
        {
            _driveEtaTicker?.Cancel();
            IsDriveRestoreRunning = false;
            IsBusy = false;
            EndCloudOperation();
        }
    }

    private async Task ImportDocumentAsync(
        LibraryExportDocument document,
        Action<int, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = await GetCurrentUserIdAsync();
        var imported = 0;
        var updated = 0;
        var progressTotal = document.People.Count + document.Items.Count;
        var progressCompleted = 0;

        foreach (var exportedPerson in document.People)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                cancellationToken.ThrowIfCancellationRequested();
                var photoBytes = await GetPortableImageBytesAsync(
                    portablePhoto.DataBase64,
                    portablePhoto.CloudObjectName,
                    cancellationToken);
                if (photoBytes is null) continue;
                var extension = NormalizePhotoExtension(portablePhoto.Extension);
                var directory = Path.Combine(FileSystem.AppDataDirectory, "person-photos");
                Directory.CreateDirectory(directory);
                var photoPath = Path.Combine(directory, $"{portablePhoto.Id:N}{extension}");
                if (!File.Exists(photoPath))
                {
                    await File.WriteAllBytesAsync(photoPath, photoBytes, cancellationToken);
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
            progress?.Invoke(++progressCompleted, progressTotal);
        }

        var importedRelations = new HashSet<Guid>();
        foreach (var exportedPerson in document.People)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
            var item = exportedItem;
            var portableCover = document.Covers.FirstOrDefault(cover => cover.MediaItemId == item.Id);
            if (portableCover is not null)
            {
                var coverBytes = await GetPortableImageBytesAsync(
                    portableCover.DataBase64,
                    portableCover.CloudObjectName,
                    cancellationToken);
                if (coverBytes is not null)
                {
                    var directory = Path.Combine(FileSystem.AppDataDirectory, "covers");
                    Directory.CreateDirectory(directory);
                    var coverPath = Path.Combine(directory, $"{item.Id:N}{NormalizePhotoExtension(portableCover.Extension)}");
                    if (!File.Exists(coverPath))
                    {
                        await File.WriteAllBytesAsync(coverPath, coverBytes, cancellationToken);
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
            progress?.Invoke(++progressCompleted, progressTotal);
        }

        StatusMessage = string.Format(T("Settings.Import.Completed"), imported, updated);
    }

    private async Task<IReadOnlyList<PersonExportDocument>> BuildPersonExportsAsync(
        string userId,
        bool compressImages = false,
        Action<int, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var summaries = await _peopleManagementService.GetCatalogAsync(userId);
        var result = new ConcurrentBag<PersonExportDocument>();
        var completed = 0;
        await Parallel.ForEachAsync(
            summaries,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 3,
                CancellationToken = cancellationToken
            },
            async (summary, token) =>
            {
                var person = await _peopleManagementService.GetAsync(userId, summary.Id);
                if (person is not null)
                {
                    var photos = new List<PortablePersonPhoto>();
                    foreach (var photo in person.Photos)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!File.Exists(photo.FilePath)) continue;
                        var image = await ReadPortableImageAsync(photo.FilePath, compressImages, token);
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
                var current = Interlocked.Increment(ref completed);
                await MainThread.InvokeOnMainThreadAsync(() => progress?.Invoke(current, summaries.Count));
            });
        return result.OrderBy(document => document.Person.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void UpdateDriveProgress(int percent, string messageKey)
    {
        var normalized = Math.Clamp(percent, 0, 100);
        DriveProgress = normalized / 100d;
        DriveProgressPercentText = $"{normalized}%";
        DriveProgressEtaText = BuildProgressEtaText(_driveEtaEstimator.Update(normalized));
        SetSyncStatus(_isCancellingCloudOperation ? "CloudOperation.Cancelling" : messageKey);
        _backgroundOperationNotifier.Report(normalized, StatusMessage);
    }

    private string BuildProgressEtaText(TimeSpan? remaining, int completed = 0, int total = 0)
    {
        var eta = remaining switch
        {
            { TotalSeconds: <= 0 } => T("Progress.Done"),
            null => T("Progress.Estimating"),
            var value => string.Format(
                T("Progress.Remaining"),
                value.Value.TotalHours >= 1
                    ? value.Value.ToString(@"h\:mm\:ss")
                    : value.Value.ToString(@"m\:ss"))
        };
        return total > 0 ? $"{eta}  •  {Math.Min(completed, total)}/{total}" : eta;
    }

    private async Task RunEtaTickerAsync(
        ProgressEtaEstimator estimator,
        bool isSync,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
                var remaining = estimator.Tick();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (isSync)
                    {
                        SyncProgressEtaText = BuildProgressEtaText(
                            remaining,
                            _syncProgressCompleted,
                            _syncProgressTotal);
                    }
                    else
                    {
                        DriveProgressEtaText = BuildProgressEtaText(remaining);
                    }
                });
            }
        }
        catch (OperationCanceledException)
        {
            // The operation completed or failed; its final status is already shown.
        }
    }

    private sealed class ProgressEtaEstimator
    {
        private readonly object _gate = new();
        private Stopwatch _timer = new();
        private TimeSpan _lastSample;
        private double? _remainingSeconds;
        private int _lastPercent;

        public void Reset()
        {
            lock (_gate)
            {
                _timer = Stopwatch.StartNew();
                _lastSample = TimeSpan.Zero;
                _remainingSeconds = null;
                _lastPercent = 0;
            }
        }

        public TimeSpan? Update(int percent)
        {
            lock (_gate)
            {
                TickCore();
                var normalized = Math.Clamp(percent, 0, 100);
                if (normalized >= 100)
                {
                    _lastPercent = 100;
                    _remainingSeconds = 0;
                    return TimeSpan.Zero;
                }

                if (normalized > _lastPercent && normalized >= 2 && _timer.Elapsed.TotalSeconds >= 3)
                {
                    // Start slightly conservatively. Subsequent samples may lower
                    // the estimate, but never make a running countdown grow.
                    var measured = _timer.Elapsed.TotalSeconds / normalized * (100 - normalized);
                    var candidate = Math.Clamp(measured * 1.2 + 15, 1, 24 * 60 * 60);
                    _remainingSeconds = _remainingSeconds.HasValue
                        ? Math.Min(_remainingSeconds.Value, candidate)
                        : candidate;
                }

                _lastPercent = Math.Max(_lastPercent, normalized);
                return ToTimeSpan();
            }
        }

        public TimeSpan? Tick()
        {
            lock (_gate)
            {
                TickCore();
                return ToTimeSpan();
            }
        }

        private void TickCore()
        {
            var elapsed = _timer.Elapsed;
            if (_remainingSeconds.HasValue && _lastPercent < 100)
            {
                _remainingSeconds = Math.Max(1, _remainingSeconds.Value - (elapsed - _lastSample).TotalSeconds);
            }
            _lastSample = elapsed;
        }

        private TimeSpan? ToTimeSpan() => _remainingSeconds.HasValue
            ? TimeSpan.FromSeconds(_remainingSeconds.Value)
            : null;
    }

    private async Task<PortableImageData> ReadPortableImageAsync(
        string path,
        bool compress,
        CancellationToken cancellationToken = default)
    {
        if (!compress)
        {
            return new PortableImageData(
                NormalizePhotoExtension(Path.GetExtension(path)),
                Convert.ToBase64String(await File.ReadAllBytesAsync(path, cancellationToken)));
        }

        var cachedObjectName = await _cloudImageCompressor.TryGetCachedObjectNameAsync(path, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedObjectName) &&
            await _cloudAssetStore.ExistsAsync(cachedObjectName, cancellationToken))
        {
            return new PortableImageData(".webp", string.Empty, cachedObjectName);
        }

        var compressed = await _cloudImageCompressor.CompressAsync(path, cancellationToken);
        var objectName = $"{compressed.SourceHash}.webp";
        await _cloudAssetStore.UploadAsync(
            objectName,
            compressed.Bytes,
            "image/webp",
            cancellationToken);
        return new PortableImageData(".webp", string.Empty, objectName);
    }

    private async Task<byte[]?> GetPortableImageBytesAsync(
        string? dataBase64,
        string? cloudObjectName,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(dataBase64))
        {
            return Convert.FromBase64String(dataBase64);
        }

        return string.IsNullOrWhiteSpace(cloudObjectName)
            ? null
            : await _cloudAssetStore.DownloadAsync(cloudObjectName, cancellationToken);
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
            CatalogProvider = item.CatalogProvider,
            CatalogItemId = item.CatalogItemId,
            CatalogSourceUrl = item.CatalogSourceUrl,
            CatalogRatingPrimarySource = item.CatalogRatingPrimarySource,
            CatalogRatingPrimary = item.CatalogRatingPrimary,
            CatalogRatingPrimaryCount = item.CatalogRatingPrimaryCount,
            CatalogRatingSecondarySource = item.CatalogRatingSecondarySource,
            CatalogRatingSecondary = item.CatalogRatingSecondary,
            CatalogRatingSecondaryCount = item.CatalogRatingSecondaryCount,
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
            CatalogProvider = item.CatalogProvider,
            CatalogItemId = item.CatalogItemId,
            CatalogSourceUrl = item.CatalogSourceUrl,
            CatalogRatingPrimarySource = item.CatalogRatingPrimarySource,
            CatalogRatingPrimary = item.CatalogRatingPrimary,
            CatalogRatingPrimaryCount = item.CatalogRatingPrimaryCount,
            CatalogRatingSecondarySource = item.CatalogRatingSecondarySource,
            CatalogRatingSecondary = item.CatalogRatingSecondary,
            CatalogRatingSecondaryCount = item.CatalogRatingSecondaryCount,
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
