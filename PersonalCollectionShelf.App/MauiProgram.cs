using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Application.Services;
using PersonalCollectionShelf.App.Pages;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;
using PersonalCollectionShelf.Infrastructure.DependencyInjection;

namespace PersonalCollectionShelf.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();

        builder.Services.AddSingleton<ILocalizationService, JsonLocalizationService>();
        builder.Services.AddInfrastructure(GetDatabasePath());
        builder.Services.AddSingleton<IMediaItemService, MediaItemService>();

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<LibraryViewModel>();
        builder.Services.AddTransient<MediaDetailsViewModel>();
        builder.Services.AddTransient<EditMediaItemViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<LibraryPage>();
        builder.Services.AddTransient<StatisticsPage>();
        builder.Services.AddTransient<MediaDetailsPage>();
        builder.Services.AddTransient<EditMediaItemPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static string GetDatabasePath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, "personalcollectionshelf.db3");
    }
}
