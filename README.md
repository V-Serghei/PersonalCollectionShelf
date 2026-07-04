# PersonalCollectionShelf

Offline-first cross-platform personal collection tracker with cloud sync for movies, books, manga, comics, games, and more.

## Main Features

- Track movies, series, books, manga, comics, games, anime, and other media.
- Store the library locally with SQLite for offline-first use.
- Add, edit, delete, search, and filter media items.
- Track status, progress, rating, notes, dates, favorites, and release year.
- Use English and Russian localization from the first version.
- Prepare account and synchronization boundaries for Firebase Auth and Firestore.
- Target Windows and Android with .NET MAUI.

## Tech Stack

- .NET 10 and C#
- .NET MAUI for Windows and Android
- MVVM with CommunityToolkit.Mvvm
- SQLite through sqlite-net-pcl
- xUnit for domain and application unit tests
- GitHub Actions for CI

## Architecture Overview

The solution uses clean layered architecture. The domain layer contains entities, enums, value objects, and repository abstractions. The application layer contains DTOs, service interfaces, validators, and use case orchestration. The infrastructure layer contains SQLite persistence, repository implementations, local storage services, and placeholders for Firebase Auth and Firestore synchronization. The MAUI app layer contains pages, view models, navigation, dependency injection setup, resources, and localization.

Dependencies flow inward: App depends on Application and Infrastructure, Infrastructure depends on Application and Domain, Application depends on Domain, and Domain does not depend on other project layers.

## Project Structure

- `PersonalCollectionShelf.App` - MAUI UI, pages, view models, navigation, app resources, and JSON localization.
- `PersonalCollectionShelf.Domain` - media entity, enums, value objects, and repository abstractions.
- `PersonalCollectionShelf.Application` - DTOs, media item service, interfaces, validators, and orchestration logic.
- `PersonalCollectionShelf.Infrastructure` - SQLite database service, media repository, Firebase placeholders, sync placeholder, and DI registration.
- `PersonalCollectionShelf.Tests` - unit tests for domain and application behavior.
- `.agent` - project context files for future AI coding sessions.

## Roadmap

MVP:

- Local media library
- Add, edit, and delete media items
- Search and filters
- English and Russian localization
- SQLite persistence
- Windows and Android support

Next versions:

- Firebase Auth
- Firestore synchronization
- Conflict resolution
- Statistics dashboard
- Import and export JSON
- Cover images
- Tags and custom lists
- Calendar and progress history

Long-term ideas:

- Multi-device sync history
- Advanced recommendation views
- Custom media types and field templates
- Backup and restore workflows
- Rich progress analytics

## Setup Instructions

1. Install .NET 10 SDK.
2. Install MAUI workloads with `dotnet workload install maui`.
3. Restore dependencies with `dotnet restore PersonalCollectionShelf.sln`.
4. Run tests with `dotnet test PersonalCollectionShelf.Tests/PersonalCollectionShelf.Tests.csproj`.
5. Build the Windows app with `dotnet build PersonalCollectionShelf.App/PersonalCollectionShelf.App.csproj -f net10.0-windows10.0.19041.0`.

Android builds require the Android tooling installed by the MAUI workload and a configured emulator or device.

## Development Workflow

- Use `develop` as the integration branch.
- Create feature branches from `develop`.
- Keep domain rules out of the UI layer.
- Keep view models thin and delegate orchestration to application services.
- Add or update tests when changing domain or application behavior.
- GitHub Actions builds Windows and Android targets and runs the test project.
- Update this README when architecture, setup, roadmap, or public features change.
- Do not commit secrets, Firebase credentials, local databases, build outputs, or user-specific IDE files.

## Localization Rules

All visible UI text must be loaded from JSON localization files in `PersonalCollectionShelf.App/Localization`.

- English is the default language in `en.json`.
- Russian is available in `ru.json`.
- Every UI key must exist in both files.
- Pages and view models must not hardcode visible UI text.
- Validation messages, placeholders, buttons, page titles, labels, descriptions, and status messages must be localized.

## Sync Strategy

The app is offline-first. SQLite is the source of truth for the current MVP. Firebase Auth and Firestore classes exist only as placeholders and do not contain credentials or real configuration. Future synchronization should authenticate the user, upload and download changed records, preserve deleted records through soft-delete metadata, and resolve conflicts with a documented strategy.

## Contribution Notes

Keep code readable, asynchronous where I/O is involved, and aligned with the layered architecture. Code comments should be in English and used only when they clarify non-obvious decisions. Before meaningful changes, read the `.agent` context files and update them when project direction or architecture changes.

## License

This project is licensed under the MIT License. See `LICENSE` for details.
