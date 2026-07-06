using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
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
        FlyoutBehavior = FlyoutBehavior.Locked;
        FlyoutWidth = 224;
        FlyoutBackgroundColor = Color.FromArgb("#100E1A");
        BackgroundColor = Color.FromArgb("#0D0B14");

        Shell.SetBackgroundColor(this, Color.FromArgb("#0D0B14"));
        Shell.SetForegroundColor(this, Color.FromArgb("#EDE9F8"));
        Shell.SetTitleColor(this, Color.FromArgb("#EDE9F8"));
        Shell.SetUnselectedColor(this, Color.FromArgb("#8179A3"));
        Shell.SetDisabledColor(this, Color.FromArgb("#4B4265"));

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
        BuildFlyoutHeader();
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

    private void BuildFlyoutHeader()
    {
        var mark = new Border
        {
            HeightRequest = 34,
            WidthRequest = 34,
            BackgroundColor = Color.FromArgb("#9D7FF4"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = 8
            },
            Content = new Label
            {
                Text = "S",
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                TextColor = Color.FromArgb("#0D0B14"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

        var title = new Label
        {
            Text = "Shelf",
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            TextColor = Color.FromArgb("#EDE9F8"),
            VerticalOptions = LayoutOptions.Center
        };

        FlyoutHeader = new HorizontalStackLayout
        {
            Padding = new Thickness(20, 20, 16, 18),
            Spacing = 10,
            Children =
            {
                mark,
                title
            }
        };
    }
}
