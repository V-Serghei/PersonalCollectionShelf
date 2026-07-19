using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using PersonalCollectionShelf.App.Pages;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Domain.Enums;

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
    private Label? _profileInitialLabel;
    private Label? _profileNameLabel;
    private Label? _profileDetailLabel;

    private static readonly (MediaType Type, string Color)[] SidebarCategories =
    [
        (MediaType.Movie, "#E07C54"),
        (MediaType.Series, "#5BA4F0"),
        (MediaType.Book, "#7CCC8A"),
        (MediaType.Manga, "#F0C040"),
        (MediaType.Comic, "#6FD8C8"),
        (MediaType.Game, "#C47CF0"),
        (MediaType.Anime, "#F07CB8")
    ];

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
        Routing.RegisterRoute(nameof(PeoplePage), typeof(PeoplePage));
        Routing.RegisterRoute(nameof(CategoryManagementPage), typeof(CategoryManagementPage));

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

        navigation.Children.Add(CreateNavButton("", T("Shell.Home"), "//Home", true, symbolFont: true));
        navigation.Children.Add(CreateNavButton("▤", T("Library.Title"), "//Library", false, symbolFont: false));
        navigation.Children.Add(CreateNavButton("▥", T("Shell.Statistics"), "//Statistics", false, symbolFont: false));
        navigation.Children.Add(CreateNavButton("", T("Settings.Title"), "//Settings", false, symbolFont: true));
        navigation.Children.Add(new Label
        {
            Text = T("Shell.Categories"),
            FontAttributes = FontAttributes.Bold,
            FontSize = 10,
            TextColor = MutedForegroundColor,
            Margin = new Thickness(4, 18, 0, 6)
        });

        foreach (var (mediaType, color) in SidebarCategories)
        {
            navigation.Children.Add(CreateCategoryLabel(mediaType, color));
        }

        root.Add(new ScrollView { Content = navigation }, 0, 1);

        _profileInitialLabel = new Label
        {
            Text = "S",
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            TextColor = PrimaryColor,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        _profileNameLabel = new Label
        {
            Text = T("Shell.LocalProfile"),
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            LineBreakMode = LineBreakMode.TailTruncation,
            TextColor = ForegroundColor
        };

        _profileDetailLabel = new Label
        {
            Text = string.Empty,
            FontSize = 10,
            TextColor = PrimaryColor
        };

        var footer = new VerticalStackLayout
        {
            Padding = new Thickness(16, 14),
            Spacing = 12,
            Children =
            {
                new Border
                {
                    Padding = new Thickness(4, 6),
                    StrokeThickness = 0,
                    BackgroundColor = Colors.Transparent,
                    Content = new Label
                    {
                        Text = _appearanceService.IsDarkTheme ? $"☼  {T("Shell.LightMode")}" : $"☾  {T("Shell.DarkMode")}",
                        FontSize = 14,
                        TextColor = SecondaryForegroundColor
                    },
                    GestureRecognizers =
                    {
                        new TapGestureRecognizer
                        {
                            Command = new Command(ToggleTheme)
                        }
                    }
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
                            Content = _profileInitialLabel
                        },
                        new VerticalStackLayout
                        {
                            Spacing = 1,
                            MaximumWidthRequest = 140,
                            Children = { _profileNameLabel, _profileDetailLabel }
                        }
                    }
                }
            }
        };
        root.Add(footer, 0, 2);

        _ = RefreshProfileAsync();

        return root;
    }

    private async Task RefreshProfileAsync()
    {
        try
        {
            var authService = _services.GetRequiredService<IAuthService>();
            var mediaItemService = _services.GetRequiredService<IMediaItemService>();

            var email = await authService.GetSignedInEmailAsync();
            var userId = await authService.GetCurrentUserIdAsync() ?? "local-user";
            var itemCount = (await mediaItemService.GetLibraryAsync(userId)).Count;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_profileNameLabel is null || _profileDetailLabel is null || _profileInitialLabel is null)
                {
                    return;
                }

                _profileNameLabel.Text = email ?? T("Shell.LocalProfile");
                _profileDetailLabel.Text = string.Format(T("Shell.ItemsCollected"), itemCount);
                _profileInitialLabel.Text = string.IsNullOrEmpty(email)
                    ? "S"
                    : char.ToUpperInvariant(email[0]).ToString();
            });
        }
        catch
        {
            // The sidebar profile is informational; keep the defaults if data is unavailable.
        }
    }

    private string T(string key)
    {
        return _localizationService.GetString(key);
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

    private View CreateCategoryLabel(MediaType mediaType, string color)
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
                    Text = T($"MediaType.{mediaType}"),
                    FontSize = 14,
                    TextColor = Color.FromArgb(color),
                    VerticalTextAlignment = TextAlignment.Center
                }
            }
        };

        row.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                try
                {
                    await GoToAsync($"//Library?mediaType={mediaType}");
                }
                catch (Exception exception)
                {
                    await Services.CrashReporter.ReportAsync(exception, $"AppShell.CategoryNavigate {mediaType}");
                }
            })
        });

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

    private Color AppBackgroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#201C2D" : "#F7F4FF");

    private Color SidebarColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#1A1726" : "#EFEAFB");

    private Color ForegroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#EDE9F8" : "#1A1728");

    private Color SecondaryForegroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#C6BEE0" : "#4C4263");

    private Color MutedForegroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#6F6098" : "#776A94");

    private Color BorderColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#3A3350" : "#D8CEEE");

    private Color PrimaryColor => Color.FromArgb(_appearanceService.AccentColorHex);

    private Color PrimaryForegroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#171421" : "#FFFFFF");

    private Color ActiveNavigationBackgroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#2D2740" : "#E4DCF8");

    private Color HoverNavigationBackgroundColor => Color.FromArgb(_appearanceService.IsDarkTheme ? "#272238" : "#EAE4F7");

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

        _ = RefreshProfileAsync();
    }

    private void HandleLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
        FlyoutContentTemplate = new DataTemplate(BuildFlyoutContent);
    }

    private void HandleAppearanceChanged(object? sender, EventArgs e)
    {
        ApplyShellColors();
        FlyoutContentTemplate = new DataTemplate(BuildFlyoutContent);
    }

    private void ToggleTheme()
    {
        _appearanceService.SetTheme(!_appearanceService.IsDarkTheme);
    }

    private void ApplyLocalization()
    {
        Title = _localizationService.GetString("App.Name");
        _homeItem.Title = T("Shell.Home");
        _libraryItem.Title = T("Library.Title");
        _statisticsItem.Title = T("Shell.Statistics");
        _settingsItem.Title = T("Settings.Title");
    }
}
