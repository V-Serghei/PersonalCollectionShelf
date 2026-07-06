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
        return new Window(_services.GetRequiredService<AppShell>());
    }
}
