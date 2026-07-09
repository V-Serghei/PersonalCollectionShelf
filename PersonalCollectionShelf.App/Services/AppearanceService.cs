using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
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
}

public sealed class AppearanceService : IAppearanceService
{
    private const string ThemeKey = "appearance.theme";
    private const string BackgroundImageKey = "appearance.backgroundImagePath";
    private const string BackgroundBlurKey = "appearance.backgroundBlur";
    private const string DarkThemeValue = "dark";
    private const string LightThemeValue = "light";

    private static readonly FilePickerFileType BackgroundImageFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.WinUI, [".png", ".jpg", ".jpeg", ".webp", ".bmp"] },
        { DevicePlatform.Android, ["image/png", "image/jpeg", "image/webp"] },
        { DevicePlatform.iOS, ["public.png", "public.jpeg"] },
        { DevicePlatform.MacCatalyst, ["public.png", "public.jpeg"] }
    });

    private static readonly IReadOnlyDictionary<string, string> DarkPalette = new Dictionary<string, string>
    {
        ["Background"] = "#0D0B14",
        ["Foreground"] = "#EDE9F8",
        ["Card"] = "#1A1726",
        ["CardElevated"] = "#201C2D",
        ["Sidebar"] = "#100E1A",
        ["Muted"] = "#1E1A2E",
        ["MutedSoft"] = "#231F30",
        ["MutedForeground"] = "#8179A3",
        ["Border"] = "#2B2440",
        ["BorderSoft"] = "#252036",
        ["Primary"] = "#9D7FF4",
        ["PrimaryPressed"] = "#8B6FE0",
        ["PrimaryForeground"] = "#0D0B14"
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

    public event EventHandler? AppearanceChanged;

    public bool IsDarkTheme => Preferences.Get(ThemeKey, DarkThemeValue) == DarkThemeValue;

    public string? BackgroundImagePath
    {
        get
        {
            var path = Preferences.Get(BackgroundImageKey, string.Empty);
            return string.IsNullOrWhiteSpace(path) ? null : path;
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

        var extension = Path.GetExtension(result.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        var directory = Path.Combine(FileSystem.AppDataDirectory, "backgrounds");
        Directory.CreateDirectory(directory);

        var destination = Path.Combine(directory, $"custom-background{extension}");
        await using (var source = await result.OpenReadAsync())
        await using (var target = File.Create(destination))
        {
            await source.CopyToAsync(target);
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
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        Apply();
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyOnMainThread()
    {
        var application = Application.Current;
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

        var backgroundImagePath = BackgroundImagePath;
        if (!string.IsNullOrWhiteSpace(backgroundImagePath) && File.Exists(backgroundImagePath))
        {
            application.Resources["CustomBackgroundImageSource"] = ImageSource.FromFile(backgroundImagePath);
            application.Resources["CustomBackgroundImageOpacity"] = Math.Clamp(0.36 - (BackgroundBlur / 180), 0.12, 0.36);
            application.Resources["CustomBackgroundOverlayOpacity"] = Math.Clamp(0.16 + (BackgroundBlur / 60), 0.16, 0.78);
        }
        else
        {
            if (application.Resources.ContainsKey("CustomBackgroundImageSource"))
            {
                application.Resources.Remove("CustomBackgroundImageSource");
            }

            application.Resources["CustomBackgroundImageOpacity"] = 0d;
            application.Resources["CustomBackgroundOverlayOpacity"] = 0d;
        }

        application.Resources["CustomBackgroundBlur"] = BackgroundBlur;
    }
}
