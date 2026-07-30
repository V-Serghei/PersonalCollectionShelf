using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Core.View;
using System.Runtime.Versioning;

namespace PersonalCollectionShelf.App;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplyImmersiveStatusBar();
        RequestNotificationPermission();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ApplyImmersiveStatusBar();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus)
        {
            ApplyImmersiveStatusBar();
        }
    }

    private void ApplyImmersiveStatusBar()
    {
        if (Window is null)
        {
            return;
        }

        WindowCompat.SetDecorFitsSystemWindows(Window, false);
        var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
        if (controller is null)
        {
            return;
        }

        controller.Hide(WindowInsetsCompat.Type.StatusBars());
        controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
    }

    private void RequestNotificationPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return;
        }

        RequestNotificationPermissionApi33();
    }

    [SupportedOSPlatform("android33.0")]
    private void RequestNotificationPermissionApi33()
    {
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.PostNotifications) ==
            Permission.Granted) return;

        ActivityCompat.RequestPermissions(
            this,
            [Android.Manifest.Permission.PostNotifications],
            4101);
    }
}
