using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using PersonalCollectionShelf.App.Pages;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App;

public sealed class AppShell : Shell
{
    private readonly ILocalizationService _localizationService;
    private readonly IAppearanceService _appearanceService;
    private readonly IServiceProvider _services;
    private readonly FlyoutItem _homeItem;
    private readonly FlyoutItem _libraryItem;
    private readonly FlyoutItem _statisticsItem;
    private readonly FlyoutItem _settingsItem;
    private readonly List<(Border Container, Label Icon, Label Text)> _navigationButtons = [];

    public AppShell(
        IServiceProvider services,
        ILocalizationService localizationService,
        IAppearanceService appearanceService)
    {
        _services = services;
        _localizationService = localizationService;
        _appearanceService = appearanceService;

        FlyoutBehavior = FlyoutBehavior.Locked;
        FlyoutWidth = 210;
        ApplyShellColors();

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
        _appearanceService.AppearanceChanged += HandleAppearanceChanged;
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
            BackgroundColor = SidebarColor,
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
                    BackgroundColor = PrimaryColor,
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 16 },
                    Content = new Label
                    {
                        Text = "S",
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 14,
                        TextColor = PrimaryForegroundColor,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center
                    }
                },
                new Label
                {
                    Text = "Shelf",
                    FontAttributes = FontAttributes.Bold,
                    FontFamily = "Cambria",
                    FontSize = 18,
                    TextColor = ForegroundColor,
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

        navigation.Children.Add(CreateNavButton("", "Home", "//Home", true, symbolFont: true));
        navigation.Children.Add(CreateNavButton("▤", "Library", "//Library", false, symbolFont: false));
        navigation.Children.Add(CreateNavButton("▥", "Statistics", "//Statistics", false, symbolFont: false));
        navigation.Children.Add(CreateNavButton("", "Settings", "//Settings", false, symbolFont: true));
        navigation.Children.Add(new Label
        {
            Text = "CATEGORIES",
            FontAttributes = FontAttributes.Bold,
            FontSize = 10,
            TextColor = MutedForegroundColor,
            Margin = new Thickness(4, 18, 0, 6)
        });

        navigation.Children.Add(CreateCategoryLabel("Movies", "#E07C54"));
        navigation.Children.Add(CreateCategoryLabel("TV Series", "#5BA4F0"));
        navigation.Children.Add(CreateCategoryLabel("Books", "#7CCC8A"));
        navigation.Children.Add(CreateCategoryLabel("Games", "#C47CF0"));
        navigation.Children.Add(CreateCategoryLabel("Anime", "#F07CB8"));
        navigation.Children.Add(CreateCategoryLabel("Music", "#F0C040"));

        root.Add(new ScrollView { Content = navigation }, 0, 1);

        var footer = new VerticalStackLayout
        {
            Padding = new Thickness(16, 14),
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = _appearanceService.IsDarkTheme ? "☼  Light Mode" : "☾  Dark Mode",
                    FontSize = 14,
                    TextColor = SecondaryForegroundColor
                },
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Border
                        {
                            BackgroundColor = BorderColor,
                            HeightRequest = 28,
                            StrokeThickness = 0,
                            StrokeShape = new RoundRectangle { CornerRadius = 14 },
                            WidthRequest = 28,
                            Content = new Label
                            {
                                Text = "A",
                                FontAttributes = FontAttributes.Bold,
                                FontSize = 12,
                                TextColor = PrimaryColor,
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
                                    TextColor = ForegroundColor
                                },
                                new Label
                                {
                                    Text = "14 items collected",
                                    FontSize = 10,
                                    TextColor = PrimaryColor
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

    private Border CreateNavButton(string icon, string label, string route, bool active, bool symbolFont)
    {
        var activeColor = PrimaryColor;
        var inactiveColor = SecondaryForegroundColor;

        var iconLabel = new Label
        {
            Text = icon,
            FontFamily = symbolFont ? "Segoe MDL2 Assets" : null,
            FontSize = symbolFont ? 15 : 13,
            WidthRequest = 20,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            TextColor = active ? activeColor : inactiveColor
        };

        var textLabel = new Label
        {
            Text = label,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            VerticalTextAlignment = TextAlignment.Center,
            TextColor = active ? activeColor : inactiveColor
        };

        var container = new Border
        {
            Padding = new Thickness(12, 9),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            BackgroundColor = active ? ActiveNavigationBackgroundColor : Colors.Transparent,
            Content = new HorizontalStackLayout
            {
                Spacing = 10,
                Children = { iconLabel, textLabel }
            }
        };

        container.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                SetActiveButton(container, iconLabel, textLabel);
                await GoToAsync(route);
            })
        });

        var hoverColor = HoverNavigationBackgroundColor;
        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) =>
        {
            if (container.BackgroundColor == Colors.Transparent)
            {
                container.BackgroundColor = hoverColor;
            }
        };
        pointer.PointerExited += (_, _) =>
        {
            if (container.BackgroundColor == hoverColor)
            {
                container.BackgroundColor = Colors.Transparent;
            }
        };
        container.GestureRecognizers.Add(pointer);

        _navigationButtons.Add((container, iconLabel, textLabel));
        return container;
    }

    private static View CreateCategoryLabel(string text, string color)
    {
        var row = new HorizontalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(6, 7),
            Children =
            {
                new Border
                {
                    WidthRequest = 10,
                    HeightRequest = 10,
                    BackgroundColor = Color.FromArgb(color),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 3 },
                    VerticalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = text,
                    FontSize = 14,
                    TextColor = Color.FromArgb(color),
                    VerticalTextAlignment = TextAlignment.Center
                }
            }
        };

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => row.Scale = 1.05;
        pointer.PointerExited += (_, _) => row.Scale = 1.0;
        row.GestureRecognizers.Add(pointer);

        return row;
    }

    private void SetActiveButton(Border activeContainer, Label activeIcon, Label activeText)
    {
        foreach (var (container, icon, text) in _navigationButtons)
        {
            var isActive = ReferenceEquals(container, activeContainer);
            var color = isActive ? PrimaryColor : SecondaryForegroundColor;
            container.BackgroundColor = isActive ? ActiveNavigationBackgroundColor : Colors.Transparent;
            icon.TextColor = color;
            text.TextColor = color;
        }
    }

    private Color AppBackgroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#0D0B14" : "#F7F4FF");

    private Color SidebarColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#100E1A" : "#EFEAFB");

    private Color ForegroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#EDE9F8" : "#1A1728");

    private Color SecondaryForegroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#C6BEE0" : "#4C4263");

    private Color MutedForegroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#6F6098" : "#776A94");

    private Color BorderColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#2B2440" : "#D8CEEE");

    private Color PrimaryColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#9D7FF4" : "#7C5CE6");

    private Color PrimaryForegroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#0D0B14" : "#FFFFFF");

    private Color ActiveNavigationBackgroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#1F1A31" : "#E4DCF8");

    private Color HoverNavigationBackgroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#171325" : "#EAE4F7");

    private void ApplyShellColors()
    {
        FlyoutBackgroundColor = SidebarColor;
        BackgroundColor = AppBackgroundColor;

        Shell.SetBackgroundColor(this, AppBackgroundColor);
        Shell.SetForegroundColor(this, ForegroundColor);
        Shell.SetTitleColor(this, ForegroundColor);
        Shell.SetUnselectedColor(this, SecondaryForegroundColor);
        Shell.SetDisabledColor(this, MutedForegroundColor);
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
            var entry = _navigationButtons[index];
            SetActiveButton(entry.Container, entry.Icon, entry.Text);
        }
    }

    private void HandleLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
    }

    private void HandleAppearanceChanged(object? sender, EventArgs e)
    {
        ApplyShellColors();
        FlyoutContentTemplate = new DataTemplate(BuildFlyoutContent);
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
