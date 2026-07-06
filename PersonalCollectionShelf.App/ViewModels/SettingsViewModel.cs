using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISyncService _syncService;
    private readonly IMediaItemService _mediaItemService;
    private readonly IAuthService _authService;
    private bool _suppressLanguageChange;
    private string _statusMessageKey = "Sync.Status.NotConfigured";
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
        IAuthService authService,
        ILocalizationService localizationService)
        : base(localizationService)
    {
        _syncService = syncService;
        _mediaItemService = mediaItemService;
        _authService = authService;
        ReloadLanguageOptions();
        StatusMessage = T(_statusMessageKey);
    }

    public ObservableCollection<LocalizedOption<string>> LanguageOptions { get; } = [];

    private LocalizedOption<string>? _selectedLanguageOption;

    private string _statusMessage = string.Empty;

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

    public string PageTitle => T("Settings.Title");

    public string AccountSectionTitle => T("Settings.Account.Title");

    public string AccountDescription => T("Settings.Account.Description");

    public string SyncSectionTitle => T("Settings.Sync.Title");

    public string SyncDescription => T("Settings.Sync.Description");

    public string SyncNowButtonText => T("Settings.Sync.Button");

    public string ExportImportSectionTitle => T("Settings.ExportImport.Title");

    public string ExportButtonText => T("Settings.Export.Button");

    public string ImportButtonText => T("Settings.Import.Button");

    public string ThemeSectionTitle => T("Settings.Theme.Title");

    public string ThemeDescription => T("Settings.Theme.Description");

    public string LanguageSectionTitle => T("Settings.Language.Title");

    public string LanguageDescription => T("Settings.Language.Description");

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        ReloadLanguageOptions();
        StatusMessage = T(_statusMessageKey);
    }

    private void OnSelectedLanguageOptionChanged(LocalizedOption<string>? value)
    {
        if (!_suppressLanguageChange && value is not null)
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
        await _syncService.RequestSyncAsync();
        _statusMessageKey = "Sync.Status.Requested";
        StatusMessage = T(_statusMessageKey);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var userId = await GetCurrentUserIdAsync();
        var items = await _mediaItemService.GetLibraryAsync(userId);
        var document = new LibraryExportDocument
        {
            Items = items
        };

        var exportsDirectory = Path.Combine(FileSystem.AppDataDirectory, "exports");
        Directory.CreateDirectory(exportsDirectory);

        var fileName = $"personal-collection-shelf-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(exportsDirectory, fileName);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);

        StatusMessage = string.Format(T("Settings.Export.Completed"), items.Count, filePath);

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = T("Settings.Export.ShareTitle"),
            File = new ShareFile(filePath)
        });
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

        var userId = await GetCurrentUserIdAsync();
        var imported = 0;
        var updated = 0;

        foreach (var item in document.Items)
        {
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

    private void ReloadLanguageOptions()
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
            UserId = userId,
            Title = item.Title,
            OriginalTitle = item.OriginalTitle,
            Description = item.Description,
            Category = item.Category,
            Tags = item.Tags,
            MediaType = item.MediaType,
            Status = item.Status,
            Rating = item.Rating,
            ProgressCurrent = item.ProgressCurrent,
            ProgressTotal = item.ProgressTotal,
            StartDate = item.StartDate,
            FinishDate = item.FinishDate,
            ReleaseYear = item.ReleaseYear,
            CoverUrl = item.CoverUrl,
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
            Tags = item.Tags,
            MediaType = item.MediaType,
            Status = item.Status,
            Rating = item.Rating,
            ProgressCurrent = item.ProgressCurrent,
            ProgressTotal = item.ProgressTotal,
            StartDate = item.StartDate,
            FinishDate = item.FinishDate,
            ReleaseYear = item.ReleaseYear,
            CoverUrl = item.CoverUrl,
            Notes = item.Notes,
            IsFavorite = item.IsFavorite
        };
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        return await _authService.GetCurrentUserIdAsync() ?? "local-user";
    }
}
