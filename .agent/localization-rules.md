# Localization Rules

PersonalCollectionShelf supports English and Russian from the beginning.

- English is the default language and is stored in `PersonalCollectionShelf.App/Localization/en.json`.
- Russian is stored in `PersonalCollectionShelf.App/Localization/ru.json`.
- The app uses `JsonLocalizationService` to load localized strings from embedded JSON resources.
- A language selector is available in Settings.
- Every visible UI text must be represented by a localization key.
- Every key must exist in both `en.json` and `ru.json`.
- Do not hardcode visible page titles, labels, button text, placeholders, messages, validation text, or descriptions in pages or view models.
- Localization keys should be descriptive and grouped by feature prefix, such as `Library`, `Details`, `Edit`, `Settings`, `Validation`, `MediaType`, and `MediaStatus`.
- When adding or renaming a key, update both JSON files in the same change.
