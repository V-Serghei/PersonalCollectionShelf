using System.Reflection;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace PersonalCollectionShelf.App.Services;

public sealed class JsonLocalizationService : ILocalizationService
{
    private const string DefaultLanguage = "en";
    private const string LanguagePreferenceKey = "SelectedLanguage";
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _languages = new(StringComparer.OrdinalIgnoreCase);

    public JsonLocalizationService()
    {
        SupportedLanguages = ["en", "ru"];

        foreach (var language in SupportedLanguages)
        {
            _languages[language] = LoadLanguage(language);
        }

        var storedLanguage = Preferences.Get(LanguagePreferenceKey, DefaultLanguage);
        CurrentLanguage = SupportedLanguages.Contains(storedLanguage, StringComparer.OrdinalIgnoreCase)
            ? storedLanguage
            : DefaultLanguage;
    }

    public event EventHandler? LanguageChanged;

    public string CurrentLanguage { get; private set; }

    public IReadOnlyList<string> SupportedLanguages { get; }

    public string GetString(string key)
    {
        if (_languages.TryGetValue(CurrentLanguage, out var currentLanguage) &&
            currentLanguage.TryGetValue(key, out var value))
        {
            return value;
        }

        if (_languages.TryGetValue(DefaultLanguage, out var defaultLanguage) &&
            defaultLanguage.TryGetValue(key, out var defaultValue))
        {
            return defaultValue;
        }

        return key;
    }

    public void SetLanguage(string languageCode)
    {
        if (!SupportedLanguages.Contains(languageCode, StringComparer.OrdinalIgnoreCase) ||
            string.Equals(CurrentLanguage, languageCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentLanguage = languageCode;
        Preferences.Set(LanguagePreferenceKey, languageCode);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyDictionary<string, string> LoadLanguage(string languageCode)
    {
        var resourceName = $"PersonalCollectionShelf.App.Localization.{languageCode}.json";
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Localization resource '{resourceName}' was not found.");

        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        return values ?? new Dictionary<string, string>();
    }
}
