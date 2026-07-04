using CommunityToolkit.Mvvm.ComponentModel;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public abstract partial class BaseViewModel : ObservableObject, IDisposable
{
    protected BaseViewModel(ILocalizationService localizationService)
    {
        LocalizationService = localizationService;
        LocalizationService.LanguageChanged += HandleLanguageChanged;
    }

    [ObservableProperty]
    private bool isBusy;

    protected ILocalizationService LocalizationService { get; }

    protected string T(string key)
    {
        return LocalizationService.GetString(key);
    }

    protected virtual void RefreshLocalizedProperties()
    {
        OnPropertyChanged(string.Empty);
    }

    public void Dispose()
    {
        LocalizationService.LanguageChanged -= HandleLanguageChanged;
        GC.SuppressFinalize(this);
    }

    private void HandleLanguageChanged(object? sender, EventArgs e)
    {
        RefreshLocalizedProperties();
    }
}
