using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.Application.Services;
using PersonalCollectionShelf.App.Pages;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.App.ViewModels;
using PersonalCollectionShelf.Infrastructure.DependencyInjection;
using PersonalCollectionShelf.Infrastructure.Services.Firebase;

namespace PersonalCollectionShelf.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>();

        builder.Services.AddSingleton<ILocalizationService, JsonLocalizationService>();
        builder.Services.AddInfrastructure(GetDatabasePath(), FirebaseOptions.Load(FileSystem.AppDataDirectory));
        builder.Services.AddSingleton<IAuthTokenStore, SecureStorageAuthTokenStore>();
        builder.Services.AddSingleton<IPersonService, PersonService>();
        builder.Services.AddSingleton<IPeopleManagementService, PeopleManagementService>();
        builder.Services.AddSingleton<ICategoryManagementService, CategoryManagementService>();
        builder.Services.AddSingleton<IStudioService, StudioService>();
        builder.Services.AddSingleton<ITagService, TagService>();
        builder.Services.AddSingleton<IMediaItemService, MediaItemService>();
        builder.Services.AddSingleton<ICollectionExplorerService, CollectionExplorerService>();
        builder.Services.AddSingleton<IAppearanceService, AppearanceService>();

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<LibraryViewModel>();
        builder.Services.AddTransient<MediaDetailsViewModel>();
        builder.Services.AddTransient<EditMediaItemViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<PeopleViewModel>();
        builder.Services.AddTransient<PersonDetailsViewModel>();
        builder.Services.AddTransient<PersonEditorViewModel>();
        builder.Services.AddTransient<PersonGalleryViewModel>();
        builder.Services.AddTransient<CategoryManagementViewModel>();
        builder.Services.AddTransient<CollectionsViewModel>();
        builder.Services.AddTransient<TagsViewModel>();
        builder.Services.AddTransient<TagDetailsViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<SignInViewModel>();
        builder.Services.AddTransient<SignInPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<LibraryPage>();
        builder.Services.AddTransient<StatisticsPage>();
        builder.Services.AddTransient<MediaDetailsPage>();
        builder.Services.AddTransient<EditMediaItemPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<PeoplePage>();
        builder.Services.AddTransient<PersonDetailsPage>();
        builder.Services.AddTransient<PersonEditorPage>();
        builder.Services.AddTransient<PersonGalleryPage>();
        builder.Services.AddTransient<CategoryManagementPage>();
        builder.Services.AddTransient<CollectionsPage>();
        builder.Services.AddTransient<TagsPage>();
        builder.Services.AddTransient<TagDetailsPage>();
        builder.Services.AddTransient<ProfilePage>();

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
