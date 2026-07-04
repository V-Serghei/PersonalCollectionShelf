using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Pages;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App;

public sealed class AppShell : Shell
{
    private readonly ILocalizationService _localizationService;
    private readonly FlyoutItem _libraryItem;
    private readonly ShellContent _libraryContent;
    private readonly FlyoutItem _settingsItem;
    private readonly ShellContent _settingsContent;

    public AppShell(IServiceProvider services, ILocalizationService localizationService)
    {
        _localizationService = localizationService;

        _libraryContent = new ShellContent
        {
            Route = nameof(LibraryPage),
            ContentTemplate = new DataTemplate(() => services.GetRequiredService<LibraryPage>())
        };

        _libraryItem = new FlyoutItem();
        _libraryItem.Items.Add(_libraryContent);
        Items.Add(_libraryItem);

        _settingsContent = new ShellContent
        {
            Route = nameof(SettingsPage),
            ContentTemplate = new DataTemplate(() => services.GetRequiredService<SettingsPage>())
        };

        _settingsItem = new FlyoutItem();
        _settingsItem.Items.Add(_settingsContent);
        Items.Add(_settingsItem);

        Routing.RegisterRoute(nameof(MediaDetailsPage), typeof(MediaDetailsPage));
        Routing.RegisterRoute(nameof(EditMediaItemPage), typeof(EditMediaItemPage));

        _localizationService.LanguageChanged += HandleLanguageChanged;
        ApplyLocalization();
    }

    private void HandleLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        Title = _localizationService.GetString("App.Name");
        _libraryItem.Title = _localizationService.GetString("Library.Title");
        _libraryContent.Title = _localizationService.GetString("Library.Title");
        _settingsItem.Title = _localizationService.GetString("Settings.Title");
        _settingsContent.Title = _localizationService.GetString("Settings.Title");
    }
}
