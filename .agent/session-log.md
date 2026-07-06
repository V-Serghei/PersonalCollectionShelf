# Session Log

## 2026-07-04

- Created the initial `develop` branch with an empty root commit.
- Created `feature/initial-project-setup` for the initial project setup work.
- Added the solution structure with App, Domain, Application, Infrastructure, and Tests projects.
- Added the initial media domain model, media enums, progress value object, repository abstraction, application DTOs, validator, and media item service.
- Added SQLite infrastructure with local database service, repository implementation, sync placeholder, Firebase Auth placeholder, Firestore placeholder, and dependency injection registration.
- Added the .NET MAUI app skeleton with Library, Details, Edit, and Settings pages plus MVVM view models.
- Added English and Russian JSON localization and a JSON localization service.
- Added README, MIT license, `.gitignore`, `.editorconfig`, `Directory.Build.props`, `global.json`, and a GitHub Actions build workflow.
- Added basic domain and application unit tests.
- Targeted .NET 10 so MAUI Android and Windows builds use the current SDK/workload support line.
- Verified local Windows and Android app builds and synchronized localization keys.
- NuGet currently reports `SQLitePCLRaw.lib.e_sqlite3` and `SQLitePCLRaw.lib.e_sqlite3.android` vulnerability warnings even at latest available `2.1.11`; revisit SQLite package choices when patched packages are available.

## 2026-07-06

- Created and pushed `feature/windows-mvp` for product UI and Windows MVP work.
- Added category, tags, cover URL presentation, dynamic category/tag filters, and richer library/detail UI.
- Added JSON export/import for the local library from Settings.
- Added smart library filters for favorites, missing category, missing cover, completed, and in-progress items.
- Removed WinRT MVVM generator warnings by replacing `[ObservableProperty]` fields with manual `SetProperty` properties.
- Added UX-first development rules requiring senior product/UX analysis before new screens and progressive disclosure over large forms.
- Fixed language switching behavior so localization refresh does not reload library data or recreate the language picker options.
