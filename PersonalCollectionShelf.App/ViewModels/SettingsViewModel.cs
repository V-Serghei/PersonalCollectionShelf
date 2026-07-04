using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISyncService _syncService;
    private bool _suppressLanguageChange;
    private string _statusMessageKey = "Sync.Status.NotConfigured";

    public SettingsViewModel(ISyncService syncService, ILocalizationService localizationService)
        : base(localizationService)
    {
        _syncService = syncService;
        ReloadLanguageOptions();
        StatusMessage = T(_statusMessageKey);
    }

    public ObservableCollection<LocalizedOption<string>> LanguageOptions { get; } = [];

    [ObservableProperty]
    private LocalizedOption<string>? selectedLanguageOption;

    [ObservableProperty]
    private string statusMessage = string.Empty;

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

    partial void OnSelectedLanguageOptionChanged(LocalizedOption<string>? value)
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
    private void Export()
    {
        _statusMessageKey = "Settings.Export.Placeholder";
        StatusMessage = T(_statusMessageKey);
    }

    [RelayCommand]
    private void Import()
    {
        _statusMessageKey = "Settings.Import.Placeholder";
        StatusMessage = T(_statusMessageKey);
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
}
