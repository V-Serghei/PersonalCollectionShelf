namespace PersonalCollectionShelf.App.Services;

internal static class UserNotification
{
    public static void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
#if ANDROID
            Android.Widget.Toast.MakeText(
                Android.App.Application.Context,
                message,
                Android.Widget.ToastLength.Short)?.Show();
#endif
        });
    }
}
