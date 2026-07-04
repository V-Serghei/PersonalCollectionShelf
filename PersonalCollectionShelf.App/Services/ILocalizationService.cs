namespace PersonalCollectionShelf.App.Services;

public interface ILocalizationService
{
    event EventHandler? LanguageChanged;

    string CurrentLanguage { get; }

    IReadOnlyList<string> SupportedLanguages { get; }

    string GetString(string key);

    void SetLanguage(string languageCode);
}
