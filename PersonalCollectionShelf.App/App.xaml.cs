using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();

        CrashReporter.InstallGlobalHandlers();
        UserAppTheme = AppTheme.Dark;
        _services = services;
        Services = services;
    }

    public static IServiceProvider Services { get; private set; } = default!;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AppShell>())
        {
            TitleBar = new TitleBar
            {
                BackgroundColor = Color.FromArgb("#0D0B14"),
                ForegroundColor = Color.FromArgb("#EDE9F8"),
                Title = string.Empty
            }
        };

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
}
