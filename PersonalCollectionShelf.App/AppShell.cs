using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using PersonalCollectionShelf.App.Pages;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App;

public sealed class AppShell : Shell
{
    private readonly ILocalizationService _localizationService;
    private readonly IServiceProvider _services;
    private readonly FlyoutItem _homeItem;
    private readonly FlyoutItem _libraryItem;
    private readonly FlyoutItem _statisticsItem;
    private readonly FlyoutItem _settingsItem;
    private readonly List<Button> _navigationButtons = [];

    public AppShell(IServiceProvider services, ILocalizationService localizationService)
    {
        _services = services;
        _localizationService = localizationService;

        FlyoutBehavior = FlyoutBehavior.Locked;
        FlyoutWidth = 210;
        FlyoutBackgroundColor = Color.FromArgb("#100E1A");
        BackgroundColor = Color.FromArgb("#0D0B14");

        Shell.SetBackgroundColor(this, Color.FromArgb("#0D0B14"));
        Shell.SetForegroundColor(this, Color.FromArgb("#EDE9F8"));
        Shell.SetTitleColor(this, Color.FromArgb("#EDE9F8"));
        Shell.SetUnselectedColor(this, Color.FromArgb("#8179A3"));
        Shell.SetDisabledColor(this, Color.FromArgb("#4B4265"));

        _homeItem = CreateItem("Home", nameof(DashboardPage), () => _services.GetRequiredService<DashboardPage>());
        _libraryItem = CreateItem("Library", nameof(LibraryPage), () => _services.GetRequiredService<LibraryPage>());
        _statisticsItem = CreateItem("Statistics", nameof(StatisticsPage), () => _services.GetRequiredService<StatisticsPage>());
        _settingsItem = CreateItem("Settings", nameof(SettingsPage), () => _services.GetRequiredService<SettingsPage>());

        Items.Add(_homeItem);
        Items.Add(_libraryItem);
        Items.Add(_statisticsItem);
        Items.Add(_settingsItem);

        Routing.RegisterRoute(nameof(MediaDetailsPage), typeof(MediaDetailsPage));
        Routing.RegisterRoute(nameof(EditMediaItemPage), typeof(EditMediaItemPage));

        FlyoutContentTemplate = new DataTemplate(BuildFlyoutContent);
        Navigated += HandleNavigated;
        _localizationService.LanguageChanged += HandleLanguageChanged;
        ApplyLocalization();
    }

    private FlyoutItem CreateItem(string route, string contentRoute, Func<Page> pageFactory)
    {
        var content = new ShellContent
        {
            Route = contentRoute,
            ContentTemplate = new DataTemplate(pageFactory)
        };

        var item = new FlyoutItem
        {
            Route = route,
            FlyoutItemIsVisible = false
        };
        item.Items.Add(content);
        return item;
    }

    private View BuildFlyoutContent()
    {
        _navigationButtons.Clear();

        var root = new Grid
        {
            BackgroundColor = Color.FromArgb("#100E1A"),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        var header = new HorizontalStackLayout
        {
            Padding = new Thickness(18, 20, 14, 20),
            Spacing = 10,
            Children =
            {
                new Border
                {
                    HeightRequest = 32,
                    WidthRequest = 32,
                    BackgroundColor = Color.FromArgb("#9D7FF4"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 16 },
                    Content = new Label
                    {
                        Text = "S",
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 14,
                        TextColor = Color.FromArgb("#0D0B14"),
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center
                    }
                },
                new Label
                {
                    Text = "Shelf",
                    FontAttributes = FontAttributes.Bold,
                    FontFamily = "serif",
                    FontSize = 18,
                    TextColor = Color.FromArgb("#EDE9F8"),
                    VerticalTextAlignment = TextAlignment.Center
                }
            }
        };
        root.Add(header, 0, 0);

        var navigation = new VerticalStackLayout
        {
            Padding = new Thickness(12, 4, 12, 0),
            Spacing = 6
        };

        navigation.Children.Add(CreateNavButton("⌂  Home", "//Home", true));
        navigation.Children.Add(CreateNavButton("▱  Library", "//Library", false));
        navigation.Children.Add(CreateNavButton("▥  Statistics", "//Statistics", false));
        navigation.Children.Add(CreateNavButton("⚙  Settings", "//Settings", false));
        navigation.Children.Add(new Label
        {
            Text = "CATEGORIES",
            FontAttributes = FontAttributes.Bold,
            FontSize = 10,
            TextColor = Color.FromArgb("#6F6098"),
            Margin = new Thickness(4, 18, 0, 6)
        });

        navigation.Children.Add(CreateCategoryLabel("▦", "Movies", "#E07C54"));
        navigation.Children.Add(CreateCategoryLabel("▭", "TV Series", "#5BA4F0"));
        navigation.Children.Add(CreateCategoryLabel("▯", "Books", "#7CCC8A"));
        navigation.Children.Add(CreateCategoryLabel("▱", "Games", "#C47CF0"));
        navigation.Children.Add(CreateCategoryLabel("✧", "Anime", "#F07CB8"));
        navigation.Children.Add(CreateCategoryLabel("♪", "Music", "#F0C040"));

        root.Add(new ScrollView { Content = navigation }, 0, 1);

        var footer = new VerticalStackLayout
        {
            Padding = new Thickness(16, 14),
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = "☼  Light Mode",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#C6BEE0")
                },
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Border
                        {
                            BackgroundColor = Color.FromArgb("#2B2440"),
                            HeightRequest = 28,
                            StrokeThickness = 0,
                            StrokeShape = new RoundRectangle { CornerRadius = 14 },
                            WidthRequest = 28,
                            Content = new Label
                            {
                                Text = "A",
                                FontAttributes = FontAttributes.Bold,
                                FontSize = 12,
                                TextColor = Color.FromArgb("#9D7FF4"),
                                HorizontalTextAlignment = TextAlignment.Center,
                                VerticalTextAlignment = TextAlignment.Center
                            }
                        },
                        new VerticalStackLayout
                        {
                            Spacing = 1,
                            Children =
                            {
                                new Label
                                {
                                    Text = "Alex Morgan",
                                    FontAttributes = FontAttributes.Bold,
                                    FontSize = 12,
                                    TextColor = Color.FromArgb("#EDE9F8")
                                },
                                new Label
                                {
                                    Text = "14 items collected",
                                    FontSize = 10,
                                    TextColor = Color.FromArgb("#9D7FF4")
                                }
                            }
                        }
                    }
                }
            }
        };
        root.Add(footer, 0, 2);

        return root;
    }

    private Button CreateNavButton(string text, string route, bool active)
    {
        var button = new Button
        {
            Text = text,
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(12, 9),
            CornerRadius = 18,
            FontSize = 14,
            BackgroundColor = active ? Color.FromArgb("#1F1A31") : Colors.Transparent,
            TextColor = active ? Color.FromArgb("#9D7FF4") : Color.FromArgb("#C6BEE0"),
            BorderWidth = 0
        };

        button.Clicked += async (_, _) =>
        {
            SetActiveButton(button);
            await GoToAsync(route);
        };

        _navigationButtons.Add(button);
        return button;
    }

    private static Label CreateCategoryLabel(string icon, string text, string color)
    {
        return new Label
        {
            Text = $"{icon}  {text}",
            FontSize = 14,
            TextColor = Color.FromArgb(color),
            Padding = new Thickness(6, 7)
        };
    }

    private void SetActiveButton(Button activeButton)
    {
        foreach (var button in _navigationButtons)
        {
            var isActive = ReferenceEquals(button, activeButton);
            button.BackgroundColor = isActive ? Color.FromArgb("#1F1A31") : Colors.Transparent;
            button.TextColor = isActive ? Color.FromArgb("#9D7FF4") : Color.FromArgb("#C6BEE0");
        }
    }

    private void HandleNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        var location = e.Current.Location.OriginalString;
        var index = location.Contains("Library", StringComparison.OrdinalIgnoreCase) ? 1
            : location.Contains("Statistics", StringComparison.OrdinalIgnoreCase) ? 2
            : location.Contains("Settings", StringComparison.OrdinalIgnoreCase) ? 3
            : 0;

        if (index >= 0 && index < _navigationButtons.Count)
        {
            SetActiveButton(_navigationButtons[index]);
        }
    }

    private void HandleLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        Title = _localizationService.GetString("App.Name");
        _homeItem.Title = "Home";
        _libraryItem.Title = _localizationService.GetString("Library.Title");
        _statisticsItem.Title = "Statistics";
        _settingsItem.Title = _localizationService.GetString("Settings.Title");
    }
}
