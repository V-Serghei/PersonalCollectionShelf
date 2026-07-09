using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;
    private readonly IAppearanceService _appearanceService;

    public App(IServiceProvider services, IAppearanceService appearanceService)
    {
        InitializeComponent();

        CrashReporter.InstallGlobalHandlers();
        _services = services;
        _appearanceService = appearanceService;
        Services = services;
        _appearanceService.Apply();
    }

    public static IServiceProvider Services { get; private set; } = default!;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AppShell>())
        {
            TitleBar = new TitleBar
            {
                Title = string.Empty
            }
        };

        ApplyTitleBarTheme(window);
        _appearanceService.AppearanceChanged += (_, _) =>
            MainThread.BeginInvokeOnMainThread(() => ApplyTitleBarTheme(window));

#if WINDOWS
        window.HandlerChanged += (_, _) =>
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                Platforms.Windows.WindowsWindowConfigurator.Configure(nativeWindow);
            }
        };
#endif

        return window;
    }

    private void ApplyTitleBarTheme(Window window)
    {
        if (window.TitleBar is null)
        {
            return;
        }

        window.TitleBar.BackgroundColor = _appearanceService.IsDarkTheme
            ? Color.FromArgb("#0D0B14")
            : Color.FromArgb("#F7F4FF");
        window.TitleBar.ForegroundColor = _appearanceService.IsDarkTheme
            ? Color.FromArgb("#EDE9F8")
            : Color.FromArgb("#1A1728");
    }
}
