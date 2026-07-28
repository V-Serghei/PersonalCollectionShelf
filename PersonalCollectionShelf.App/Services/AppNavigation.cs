using Microsoft.Extensions.DependencyInjection;
using PersonalCollectionShelf.App.Pages;

namespace PersonalCollectionShelf.App.Services;

internal static class AppNavigation
{
    public static async Task OpenEditMediaItemAsync(Guid? mediaItemId = null)
    {
        if (!UsesModalNavigation)
        {
            var route = mediaItemId.HasValue
                ? $"{nameof(EditMediaItemPage)}?id={mediaItemId.Value}"
                : nameof(EditMediaItemPage);
            await Shell.Current.GoToAsync(route);
            return;
        }

        var page = App.Services.GetRequiredService<EditMediaItemPage>();
        page.SetNavigationTarget(mediaItemId);
        await Shell.Current.Navigation.PushModalAsync(page);
        await page.LoadNavigationTargetAsync();
    }

    public static async Task OpenMediaDetailsAsync(Guid mediaItemId)
    {
        if (!UsesModalNavigation)
        {
            await Shell.Current.GoToAsync($"{nameof(MediaDetailsPage)}?id={mediaItemId}");
            return;
        }

        var page = App.Services.GetRequiredService<MediaDetailsPage>();
        page.SetNavigationTarget(mediaItemId);
        await Shell.Current.Navigation.PushModalAsync(page);
        await page.LoadNavigationTargetAsync();
    }

    public static Task OpenPeopleAsync() =>
        OpenUtilityPageAsync(
            () => App.Services.GetRequiredService<PeoplePage>(),
            nameof(PeoplePage));

    public static Task OpenCategoriesAsync() =>
        OpenUtilityPageAsync(
            () => App.Services.GetRequiredService<CategoryManagementPage>(),
            nameof(CategoryManagementPage));

    public static async Task CloseAsync()
    {
        if (Shell.Current.Navigation.ModalStack.Count > 0)
        {
            await Shell.Current.Navigation.PopModalAsync();
            return;
        }

        await Shell.Current.GoToAsync("..");
    }

    private static async Task OpenUtilityPageAsync(Func<Page> pageFactory, string route)
    {
        if (UsesModalNavigation)
        {
            await Shell.Current.Navigation.PushModalAsync(pageFactory());
            return;
        }

        await Shell.Current.GoToAsync(route);
    }

    private static bool UsesModalNavigation =>
        DeviceInfo.Current.Platform == DevicePlatform.Android &&
        DeviceInfo.Current.Idiom == DeviceIdiom.Phone;
}
