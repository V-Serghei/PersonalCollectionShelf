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

    string AccentColorHex { get; }

    string? BackgroundImagePath { get; }

    double BackgroundBlur { get; }

    event EventHandler? AppearanceChanged;

    event EventHandler? BackgroundBlurChanged;

    void Apply();

    void SetTheme(bool isDarkTheme);

    void SetAccentColor(string colorHex);

    void SetBackgroundBlur(double blur);

    Task<bool> PickBackgroundImageAsync();

    void ClearBackgroundImage();
}

public sealed class AppearanceService : IAppearanceService
{
    private const string ThemeKey = "appearance.theme";
    private const string AccentColorKey = "appearance.accentColor";
    private const string BackgroundImageKey = "appearance.backgroundImagePath";
    private const string BackgroundBlurKey = "appearance.backgroundBlur";
    private const string DarkThemeValue = "dark";
    private const string LightThemeValue = "light";
    private const string DefaultAccentColor = "#9D7FF4";
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

    public event EventHandler? AppearanceChanged;

    public event EventHandler? BackgroundBlurChanged;

    public bool IsDarkTheme => Preferences.Get(ThemeKey, DarkThemeValue) == DarkThemeValue;

    public string AccentColorHex => NormalizeColorHex(Preferences.Get(AccentColorKey, DefaultAccentColor));

    public string? BackgroundImagePath
    {
        get
        {
            var path = Preferences.Get(BackgroundImageKey, string.Empty);
            return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : path;
        }
    }

    private double? _backgroundBlur;
    private CancellationTokenSource? _blurPersistDebounce;

    public double BackgroundBlur => _backgroundBlur ??= Preferences.Get(BackgroundBlurKey, 0d);

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

    public void SetAccentColor(string colorHex)
    {
        var normalized = NormalizeColorHex(colorHex);
        if (string.Equals(AccentColorHex, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Preferences.Set(AccentColorKey, normalized);
        NotifyChanged();
    }

    public void SetBackgroundBlur(double blur)
    {
        var normalized = Math.Clamp(Math.Round(blur), 0, 40);
        if (Math.Abs(BackgroundBlur - normalized) < 0.1)
        {
            return;
        }

        _backgroundBlur = normalized;
        BackgroundBlurChanged?.Invoke(this, EventArgs.Empty);

        // Persisting hits the disk, so keep it off the slider-drag hot path.
        _blurPersistDebounce?.Cancel();
        var debounce = new CancellationTokenSource();
        _blurPersistDebounce = debounce;
        _ = PersistBlurAsync(normalized, debounce.Token);
    }

    private static async Task PersistBlurAsync(double value, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(400, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        Preferences.Set(BackgroundBlurKey, value);
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
        DeleteBackgroundImageFiles();
        var destination = Path.Combine(BackgroundsDirectory, $"custom-background-{DateTime.UtcNow:yyyyMMddHHmmssfff}.png");

        await using (var source = await result.OpenReadAsync())
        {
            using var image = PlatformImage.FromStream(source);
            using var resized = DownscaleToFit(image, MaxBackgroundDimension);
            await using var target = File.Create(destination);
            resized.Save(target, ImageFormat.Png);
        }

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
        DeleteBackgroundImageFiles();
        NotifyChanged();
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

    private static void DeleteBackgroundImageFiles()
    {
        try
        {
            if (!Directory.Exists(BackgroundsDirectory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(BackgroundsDirectory, "custom-background-*.png"))
            {
                DeleteFileIfExists(file);
            }

            DeleteFileIfExists(Path.Combine(BackgroundsDirectory, "custom-background.png"));
        }
        catch
        {
            // Best effort cleanup; a stale background file is ignored once preferences point elsewhere.
        }
    }

    private static string NormalizeColorHex(string colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return DefaultAccentColor;
        }

        var value = colorHex.Trim();
        if (!value.StartsWith('#'))
        {
            value = "#" + value;
        }

        return value.Length == 7 ? value.ToUpperInvariant() : DefaultAccentColor;
    }

    private static string Darken(string colorHex, double factor)
    {
        var color = Color.FromArgb(colorHex);
        var red = (int)Math.Clamp(Math.Round(color.Red * 255 * factor), 0, 255);
        var green = (int)Math.Clamp(Math.Round(color.Green * 255 * factor), 0, 255);
        var blue = (int)Math.Clamp(Math.Round(color.Blue * 255 * factor), 0, 255);
        return $"#{red:X2}{green:X2}{blue:X2}";
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

        application.Resources["Primary"] = Color.FromArgb(AccentColorHex);
        application.Resources["PrimaryPressed"] = Color.FromArgb(Darken(AccentColorHex, 0.88));
        application.Resources["PrimaryForeground"] = Color.FromArgb("#171421");

        application.Resources["PageBackground"] = BackgroundImagePath is null
            ? Color.FromArgb(palette["Background"])
            : Colors.Transparent;

        var backgroundPath = BackgroundImagePath;
        if (backgroundPath is null)
        {
            application.Resources.Remove("PageBackgroundImage");
        }
        else
        {
            application.Resources["PageBackgroundImage"] = new StreamImageSource
            {
                Stream = cancellationToken => Task.FromResult<Stream>(File.OpenRead(backgroundPath))
            };
        }
    }
}
