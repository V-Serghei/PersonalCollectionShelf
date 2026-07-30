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
        await PushOpaqueModalAsync(page);
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
        await PushOpaqueModalAsync(page);
        await page.LoadNavigationTargetAsync();
    }

    public static Task OpenPeopleAsync() => Shell.Current.GoToAsync("//People");

    public static Task OpenTagAsync(string tagName) =>
        Shell.Current.GoToAsync($"{nameof(TagDetailsPage)}?tag={Uri.EscapeDataString(tagName)}");

    public static async Task OpenPersonAsync(Guid personId)
    {
        if (!UsesModalNavigation || Shell.Current.Navigation.ModalStack.Count == 0)
        {
            await Shell.Current.GoToAsync($"{nameof(PersonDetailsPage)}?id={personId}");
            return;
        }

        var page = App.Services.GetRequiredService<PersonDetailsPage>();
        page.ViewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = personId });
        await PushOpaqueModalAsync(page);
        await page.ViewModel.LoadAsync();
    }

    public static async Task OpenMediaContributorsAsync(Guid mediaItemId)
    {
        if (!UsesModalNavigation)
        {
            await Shell.Current.GoToAsync($"{nameof(MediaContributorsPage)}?id={mediaItemId}");
            return;
        }

        var page = App.Services.GetRequiredService<MediaContributorsPage>();
        page.SetNavigationTarget(mediaItemId);
        await PushOpaqueModalAsync(page);
        await page.LoadNavigationTargetAsync();
    }

    public static Task OpenPersonEditorAsync(Guid? personId = null)
    {
        var route = personId.HasValue
            ? $"{nameof(PersonEditorPage)}?id={personId.Value}"
            : nameof(PersonEditorPage);
        return Shell.Current.GoToAsync(route);
    }

    public static Task OpenPersonGalleryAsync(Guid personId) =>
        Shell.Current.GoToAsync($"{nameof(PersonGalleryPage)}?id={personId}");

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
            await PushOpaqueModalAsync(pageFactory());
            return;
        }

        await Shell.Current.GoToAsync(route);
    }

    private static Task PushOpaqueModalAsync(Page page)
    {
        var background = Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue("Background", out var value) == true &&
                         value is Microsoft.Maui.Graphics.Color color
            ? color
            : Microsoft.Maui.Graphics.Color.FromArgb("#201C2D");

        page.BackgroundColor = background;
        NavigationPage.SetHasNavigationBar(page, false);
        var window = new NavigationPage(page)
        {
            BackgroundColor = background
        };
        return Shell.Current.Navigation.PushModalAsync(window, animated: true);
    }

    private static bool UsesModalNavigation =>
        DeviceInfo.Current.Platform == DevicePlatform.Android &&
        DeviceInfo.Current.Idiom == DeviceIdiom.Phone;
}
