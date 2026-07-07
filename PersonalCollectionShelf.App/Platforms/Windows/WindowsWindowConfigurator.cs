using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace PersonalCollectionShelf.App.Platforms.Windows;

public static class WindowsWindowConfigurator
{
    public static void Configure(global::Microsoft.UI.Xaml.Window nativeWindow)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(nativeWindow);
            var windowId = global::Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow?.TitleBar is { } titleBar)
            {
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.BackgroundColor = global::Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.InactiveBackgroundColor = global::Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.ButtonBackgroundColor = global::Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.ButtonInactiveBackgroundColor = global::Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.ButtonForegroundColor = global::Windows.UI.Color.FromArgb(255, 0xED, 0xE9, 0xF8);
                titleBar.ButtonHoverBackgroundColor = global::Windows.UI.Color.FromArgb(255, 0x2B, 0x24, 0x40);
                titleBar.ButtonHoverForegroundColor = global::Windows.UI.Color.FromArgb(255, 0xED, 0xE9, 0xF8);
                titleBar.ButtonPressedBackgroundColor = global::Windows.UI.Color.FromArgb(255, 0x1F, 0x1A, 0x31);
                titleBar.ButtonPressedForegroundColor = global::Windows.UI.Color.FromArgb(255, 0x9D, 0x7F, 0xF4);
            }
        }
        catch
        {
            // Title bar customization is best-effort; fall back to the default chrome if the host OS rejects it.
        }

        try
        {
            nativeWindow.SystemBackdrop = new DesktopAcrylicBackdrop();
        }
        catch
        {
            // Acrylic requires Windows 11; older hosts keep the solid window background.
        }
    }
}
