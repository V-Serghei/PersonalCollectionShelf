using Microsoft.UI.Windowing;
using PersonalCollectionShelf.App.Services;
using System.Runtime.InteropServices;
using WinRT.Interop;
using WinControls = Microsoft.UI.Xaml.Controls;
using WinMedia = Microsoft.UI.Xaml.Media;
using WinImaging = Microsoft.UI.Xaml.Media.Imaging;

namespace PersonalCollectionShelf.App.Platforms.Windows;

public static class WindowsWindowConfigurator
{
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmSystemBackdropNone = 1;

    public static void Configure(global::Microsoft.UI.Xaml.Window nativeWindow, IAppearanceService appearanceService)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(nativeWindow);
            DisableDwmBackdrop(hwnd);
        }
        catch
        {
            // Older Windows builds may not support DWMWA_SYSTEMBACKDROP_TYPE.
        }

        try
        {
            nativeWindow.SystemBackdrop = null;
        }
        catch
        {
            // Keep the default solid host background if the OS rejects direct window styling.
        }

        Refresh(nativeWindow, appearanceService);

        nativeWindow.Activated += (_, _) => Refresh(nativeWindow, appearanceService);
    }

    public static void Refresh(global::Microsoft.UI.Xaml.Window nativeWindow, IAppearanceService appearanceService)
    {
        ApplyTitleBarColors(nativeWindow, appearanceService);
        _ = ApplyBackgroundAsync(nativeWindow, appearanceService);
    }

    private static void ApplyTitleBarColors(global::Microsoft.UI.Xaml.Window nativeWindow, IAppearanceService appearanceService)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(nativeWindow);
            var windowId = global::Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow?.TitleBar is not { } titleBar)
            {
                return;
            }

            var background = ThemeBackgroundColor(appearanceService);
            var foreground = ThemeForegroundColor(appearanceService);
            var hover = ThemeHoverColor(appearanceService);
            var pressed = ThemePressedColor(appearanceService);
            var accent = ToWindowsColor(appearanceService.AccentColorHex);

            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.BackgroundColor = background;
            titleBar.InactiveBackgroundColor = background;
            titleBar.ButtonBackgroundColor = background;
            titleBar.ButtonInactiveBackgroundColor = background;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverBackgroundColor = hover;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedBackgroundColor = pressed;
            titleBar.ButtonPressedForegroundColor = accent;
        }
        catch
        {
            // Title bar customization is best-effort; fall back to the default chrome if the host OS rejects it.
        }
    }

    private static async Task ApplyBackgroundAsync(global::Microsoft.UI.Xaml.Window nativeWindow, IAppearanceService appearanceService)
    {
        try
        {
            var imagePath = await appearanceService.GetRenderableBackgroundImageAsync();

            WinMedia.Brush brush;
            if (imagePath is not null)
            {
                brush = new WinMedia.ImageBrush
                {
                    ImageSource = new WinImaging.BitmapImage(new Uri(imagePath)),
                    Stretch = WinMedia.Stretch.UniformToFill
                };
            }
            else
            {
                brush = new WinMedia.SolidColorBrush(ThemeBackgroundColor(appearanceService));
            }

            SetBackgroundBrush(nativeWindow, brush);
        }
        catch
        {
            // Keep the last applied background if the OS or file system rejects the update.
        }
    }

    private static void SetBackgroundBrush(global::Microsoft.UI.Xaml.Window nativeWindow, WinMedia.Brush brush)
    {
        switch (nativeWindow.Content)
        {
            case WinControls.Panel panel:
                panel.Background = brush;
                break;
            case WinControls.Control control:
                control.Background = brush;
                break;
            case WinControls.Border border:
                border.Background = brush;
                break;
            case global::Microsoft.UI.Xaml.UIElement element:
                var root = new WinControls.Grid
                {
                    Background = brush
                };
                root.Children.Add(element);
                nativeWindow.Content = root;
                break;
        }
    }

    private static global::Windows.UI.Color ThemeBackgroundColor(IAppearanceService appearanceService) =>
        appearanceService.IsDarkTheme
            ? global::Windows.UI.Color.FromArgb(255, 0x20, 0x1C, 0x2D)
            : global::Windows.UI.Color.FromArgb(255, 0xF7, 0xF4, 0xFF);

    private static global::Windows.UI.Color ThemeForegroundColor(IAppearanceService appearanceService) =>
        appearanceService.IsDarkTheme
            ? global::Windows.UI.Color.FromArgb(255, 0xED, 0xE9, 0xF8)
            : global::Windows.UI.Color.FromArgb(255, 0x1A, 0x17, 0x28);

    private static global::Windows.UI.Color ThemeHoverColor(IAppearanceService appearanceService) =>
        appearanceService.IsDarkTheme
            ? global::Windows.UI.Color.FromArgb(255, 0x32, 0x2C, 0x44)
            : global::Windows.UI.Color.FromArgb(255, 0xE7, 0xE0, 0xF4);

    private static global::Windows.UI.Color ThemePressedColor(IAppearanceService appearanceService) =>
        appearanceService.IsDarkTheme
            ? global::Windows.UI.Color.FromArgb(255, 0x2A, 0x25, 0x3A)
            : global::Windows.UI.Color.FromArgb(255, 0xEE, 0xE9, 0xFA);

    private static global::Windows.UI.Color ToWindowsColor(string colorHex)
    {
        var color = Microsoft.Maui.Graphics.Color.FromArgb(colorHex);
        return global::Windows.UI.Color.FromArgb(
            255,
            (byte)Math.Clamp(Math.Round(color.Red * 255), 0, 255),
            (byte)Math.Clamp(Math.Round(color.Green * 255), 0, 255),
            (byte)Math.Clamp(Math.Round(color.Blue * 255), 0, 255));
    }

    private static void DisableDwmBackdrop(nint hwnd)
    {
        try
        {
            var backdrop = DwmSystemBackdropNone;
            _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, Marshal.SizeOf<int>());
        }
        catch
        {
            // Older Windows builds may not support DWMWA_SYSTEMBACKDROP_TYPE.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
