using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Hosting;
using PersonalCollectionShelf.App.Services;
using System.Numerics;
using System.Runtime.InteropServices;
using WinRT.Interop;
using WinControls = Microsoft.UI.Xaml.Controls;
using WinMedia = Microsoft.UI.Xaml.Media;

namespace PersonalCollectionShelf.App.Platforms.Windows;

public static class WindowsWindowConfigurator
{
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmSystemBackdropNone = 1;

    // The slider goes 0-40; Gaussian blur stops reading as "more blurred" long before
    // 40 device pixels, so compress the scale to keep every slider step visible.
    private const double BlurAmountScale = 0.6;

    private static WinControls.Grid? _backgroundHost;
    private static SpriteVisual? _backgroundVisual;
    private static CompositionSurfaceBrush? _surfaceBrush;
    private static CompositionEffectBrush? _blurBrush;
    private static string? _loadedImagePath;

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
        appearanceService.BackgroundBlurChanged += (_, _) => UpdateBlurAmount(appearanceService);
    }

    public static void Refresh(global::Microsoft.UI.Xaml.Window nativeWindow, IAppearanceService appearanceService)
    {
        ApplyTitleBarColors(nativeWindow, appearanceService);
        ApplyBackground(nativeWindow, appearanceService);
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

    private static void ApplyBackground(global::Microsoft.UI.Xaml.Window nativeWindow, IAppearanceService appearanceService)
    {
        try
        {
            var imagePath = appearanceService.BackgroundImagePath;

            if (imagePath is null)
            {
                HideBackgroundVisual();
                SetBackgroundBrush(nativeWindow, new WinMedia.SolidColorBrush(ThemeBackgroundColor(appearanceService)));
                return;
            }

            SetBackgroundBrush(nativeWindow, new WinMedia.SolidColorBrush(ThemeBackgroundColor(appearanceService)));

            var host = EnsureBackgroundHost(nativeWindow);
            if (host is null)
            {
                return;
            }

            host.Visibility = global::Microsoft.UI.Xaml.Visibility.Visible;

            var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;

            if (_backgroundVisual is null)
            {
                _backgroundVisual = compositor.CreateSpriteVisual();
                _backgroundVisual.RelativeSizeAdjustment = Vector2.One;
                ElementCompositionPreview.SetElementChildVisual(host, _backgroundVisual);
            }

            if (_surfaceBrush is null || !string.Equals(_loadedImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
            {
                var surface = WinMedia.LoadedImageSurface.StartLoadFromUri(new Uri(imagePath));
                _surfaceBrush = compositor.CreateSurfaceBrush(surface);
                _surfaceBrush.Stretch = CompositionStretch.UniformToFill;
                _loadedImagePath = imagePath;
                _blurBrush = null;
            }

            if (_blurBrush is null)
            {
                var blurEffect = new GaussianBlurEffect
                {
                    Name = "Blur",
                    BlurAmount = 0f,
                    BorderMode = EffectBorderMode.Hard,
                    Optimization = EffectOptimization.Speed,
                    Source = new CompositionEffectSourceParameter("source")
                };

                var factory = compositor.CreateEffectFactory(blurEffect, ["Blur.BlurAmount"]);
                _blurBrush = factory.CreateBrush();
                _blurBrush.SetSourceParameter("source", _surfaceBrush);
                _backgroundVisual.Brush = _blurBrush;
            }

            UpdateBlurAmount(appearanceService);
        }
        catch
        {
            // Keep the last applied background if the OS or file system rejects the update.
        }
    }

    private static void UpdateBlurAmount(IAppearanceService appearanceService)
    {
        try
        {
            _blurBrush?.Properties.InsertScalar("Blur.BlurAmount", (float)(appearanceService.BackgroundBlur * BlurAmountScale));
        }
        catch
        {
            // A stale brush after a window teardown is harmless; it is rebuilt on the next refresh.
        }
    }

    private static void HideBackgroundVisual()
    {
        if (_backgroundHost is not null)
        {
            _backgroundHost.Visibility = global::Microsoft.UI.Xaml.Visibility.Collapsed;
        }
    }

    private static WinControls.Grid? EnsureBackgroundHost(global::Microsoft.UI.Xaml.Window nativeWindow)
    {
        if (_backgroundHost is not null)
        {
            return _backgroundHost;
        }

        switch (nativeWindow.Content)
        {
            case WinControls.Panel panel:
                var host = new WinControls.Grid();
                panel.Children.Insert(0, host);
                _backgroundHost = host;
                return host;
            case WinControls.Control:
            case WinControls.Border:
                // These roots cannot host an extra child; fall back to no image background.
                return null;
            case global::Microsoft.UI.Xaml.UIElement element:
                var backgroundLayer = new WinControls.Grid();
                var root = new WinControls.Grid();
                root.Children.Add(backgroundLayer);
                root.Children.Add(element);
                nativeWindow.Content = root;
                _backgroundHost = backgroundLayer;
                return backgroundLayer;
            default:
                return null;
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
