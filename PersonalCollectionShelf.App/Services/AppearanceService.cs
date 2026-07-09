using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Storage;

namespace PersonalCollectionShelf.App.Services;

public interface IAppearanceService
{
    bool IsDarkTheme { get; }

    string? BackgroundImagePath { get; }

    double BackgroundBlur { get; }

    event EventHandler? AppearanceChanged;

    void Apply();

    void SetTheme(bool isDarkTheme);

    void SetBackgroundBlur(double blur);

    Task<bool> PickBackgroundImageAsync();

    void ClearBackgroundImage();

    Task<string?> GetRenderableBackgroundImageAsync();
}

public sealed class AppearanceService : IAppearanceService
{
    private const string ThemeKey = "appearance.theme";
    private const string BackgroundImageKey = "appearance.backgroundImagePath";
    private const string BackgroundBlurKey = "appearance.backgroundBlur";
    private const string DarkThemeValue = "dark";
    private const string LightThemeValue = "light";
    private const int MaxBackgroundDimension = 1920;

    private static readonly FilePickerFileType BackgroundImageFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.WinUI, [".png", ".jpg", ".jpeg", ".webp", ".bmp"] },
        { DevicePlatform.Android, ["image/png", "image/jpeg", "image/webp"] },
        { DevicePlatform.iOS, ["public.png", "public.jpeg"] },
        { DevicePlatform.MacCatalyst, ["public.png", "public.jpeg"] }
    });

    private static readonly IReadOnlyDictionary<string, string> DarkPalette = new Dictionary<string, string>
    {
        ["Background"] = "#201C2D",
        ["Foreground"] = "#EDE9F8",
        ["Card"] = "#272238",
        ["CardElevated"] = "#2D2740",
        ["Sidebar"] = "#1A1726",
        ["Muted"] = "#2A253A",
        ["MutedSoft"] = "#322C44",
        ["MutedForeground"] = "#8179A3",
        ["Border"] = "#3A3350",
        ["BorderSoft"] = "#332D47",
        ["Primary"] = "#9D7FF4",
        ["PrimaryPressed"] = "#8B6FE0",
        ["PrimaryForeground"] = "#171421"
    };

    private static readonly IReadOnlyDictionary<string, string> LightPalette = new Dictionary<string, string>
    {
        ["Background"] = "#F7F4FF",
        ["Foreground"] = "#1A1728",
        ["Card"] = "#FFFFFF",
        ["CardElevated"] = "#FBF9FF",
        ["Sidebar"] = "#EFEAFB",
        ["Muted"] = "#EEE9FA",
        ["MutedSoft"] = "#E7E0F4",
        ["MutedForeground"] = "#6F638A",
        ["Border"] = "#D8CEEE",
        ["BorderSoft"] = "#E6DDF7",
        ["Primary"] = "#7C5CE6",
        ["PrimaryPressed"] = "#6A4FD6",
        ["PrimaryForeground"] = "#FFFFFF"
    };

    private static string BackgroundsDirectory => Path.Combine(FileSystem.AppDataDirectory, "backgrounds");

    private static string BlurredBackgroundCachePath => Path.Combine(BackgroundsDirectory, "custom-background-blurred.png");

    public event EventHandler? AppearanceChanged;

    public bool IsDarkTheme => Preferences.Get(ThemeKey, DarkThemeValue) == DarkThemeValue;

    public string? BackgroundImagePath
    {
        get
        {
            var path = Preferences.Get(BackgroundImageKey, string.Empty);
            return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : path;
        }
    }

    public double BackgroundBlur => Preferences.Get(BackgroundBlurKey, 0d);

    public void Apply()
    {
        if (MainThread.IsMainThread)
        {
            ApplyOnMainThread();
            return;
        }

        MainThread.BeginInvokeOnMainThread(ApplyOnMainThread);
    }

    public void SetTheme(bool isDarkTheme)
    {
        var value = isDarkTheme ? DarkThemeValue : LightThemeValue;
        if (Preferences.Get(ThemeKey, DarkThemeValue) == value)
        {
            return;
        }

        Preferences.Set(ThemeKey, value);
        NotifyChanged();
    }

    public void SetBackgroundBlur(double blur)
    {
        var normalized = Math.Clamp(Math.Round(blur), 0, 40);
        if (Math.Abs(Preferences.Get(BackgroundBlurKey, 0d) - normalized) < 0.1)
        {
            return;
        }

        Preferences.Set(BackgroundBlurKey, normalized);
        NotifyChanged();
    }

    public async Task<bool> PickBackgroundImageAsync()
    {
        var result = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = "Choose background image",
            FileTypes = BackgroundImageFileType
        });

        if (result is null)
        {
            return false;
        }

        Directory.CreateDirectory(BackgroundsDirectory);
        var destination = Path.Combine(BackgroundsDirectory, "custom-background.png");

        await using (var source = await result.OpenReadAsync())
        {
            using var image = PlatformImage.FromStream(source);
            using var resized = DownscaleToFit(image, MaxBackgroundDimension);
            await using var target = File.Create(destination);
            resized.Save(target, ImageFormat.Png);
        }

        DeleteFileIfExists(BlurredBackgroundCachePath);
        Preferences.Set(BackgroundImageKey, destination);
        NotifyChanged();
        return true;
    }

    public void ClearBackgroundImage()
    {
        if (string.IsNullOrWhiteSpace(BackgroundImagePath))
        {
            return;
        }

        Preferences.Remove(BackgroundImageKey);
        DeleteFileIfExists(BlurredBackgroundCachePath);
        NotifyChanged();
    }

    public async Task<string?> GetRenderableBackgroundImageAsync()
    {
        var sourcePath = BackgroundImagePath;
        if (sourcePath is null)
        {
            return null;
        }

        var blur = BackgroundBlur;
        if (blur <= 0)
        {
            return sourcePath;
        }

        try
        {
            return await Task.Run(() => CreateBlurredBackground(sourcePath, blur));
        }
        catch
        {
            return sourcePath;
        }
    }

    private static string CreateBlurredBackground(string sourcePath, double blur)
    {
        using var source = File.OpenRead(sourcePath);
        using var image = PlatformImage.FromStream(source);

        var downscale = Math.Max(0.03, 1.0 / (1.0 + blur / 3.0));
        var smallWidth = Math.Max(1, (int)Math.Round(image.Width * downscale));
        var smallHeight = Math.Max(1, (int)Math.Round(image.Height * downscale));

        using var shrunk = image.Resize(smallWidth, smallHeight, ResizeMode.Stretch);
        using var blurred = shrunk.Resize(image.Width, image.Height, ResizeMode.Stretch);

        Directory.CreateDirectory(BackgroundsDirectory);
        using var destination = File.Create(BlurredBackgroundCachePath);
        blurred.Save(destination, ImageFormat.Png);

        return BlurredBackgroundCachePath;
    }

    private static Microsoft.Maui.Graphics.IImage DownscaleToFit(Microsoft.Maui.Graphics.IImage image, int maxDimension)
    {
        if (image.Width <= maxDimension && image.Height <= maxDimension)
        {
            return image;
        }

        var scale = Math.Min((double)maxDimension / image.Width, (double)maxDimension / image.Height);
        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));
        return image.Resize(width, height, ResizeMode.Stretch);
    }

    private static void DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup; a stale cache file will simply be regenerated on next apply.
        }
    }

    private void NotifyChanged()
    {
        Apply();
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyOnMainThread()
    {
        var application = Microsoft.Maui.Controls.Application.Current;
        if (application is null)
        {
            return;
        }

        application.UserAppTheme = IsDarkTheme ? AppTheme.Dark : AppTheme.Light;

        var palette = IsDarkTheme ? DarkPalette : LightPalette;
        foreach (var item in palette)
        {
            application.Resources[item.Key] = Color.FromArgb(item.Value);
        }

        application.Resources["PageBackground"] = BackgroundImagePath is null
            ? Color.FromArgb(palette["Background"])
            : Colors.Transparent;
    }
}
