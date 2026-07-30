# PersonalCollectionShelf

Offline-first cross-platform personal collection tracker with cloud sync for movies, books, manga, comics, games, and more.

## Main Features

- Track movies, cartoons, series, animated series, anime, books, manga, comics, games, and other media.
- Store the library locally with SQLite for offline-first use.
- Add, edit, delete, search, and filter media items.
- Use touch-friendly Android forms with keyboard-aware focus, user-cancelled auto-positioning, and responsive controls.
- Browse large libraries, people, collections, and genre results through buffered native Android lists that keep already loaded rows in memory.
- Load library summaries and filters without repeatedly hydrating every detail graph.
- Track status, rating, notes, dates, favorites, and release year without manually maintaining percentages.
- Browse tags and genres, open all matching works, and filter or sort the result by media type.
- Group works into series, cycles, franchises, and universes with stacked-cover previews.
- Use English and Russian localization across navigation, dashboards, statistics, settings, and dynamic entity labels.
- Select light/dark themes, an accent color, grid density, and an optional aspect-preserving custom background.
- Sign in with Google or email/password and synchronize through Firebase.
- Keep original images on the device that added them while syncing 768px WebP copies to other devices.
- Create checksummed, versioned Google Drive backups containing portable data and cloud-optimized image copies.
- See progress and estimated time for sync, backup, and restore; cancel the active operation from Settings and follow long operations from an Android notification.
- Search TMDB and autofill screen titles; show IMDb and Kinopoisk ratings separately from the personal rating.
- Search Google Books and Open Library for books and manga, Comic Vine for comics, and RAWG for games.
- Reuse existing people by name during metadata import and create missing contributors automatically.
- Refresh online metadata and both external ratings only on demand from an icon in the detail card; opening the app or an item never triggers a provider refresh.
- Target Windows and Android with .NET MAUI.

## Tech Stack

- .NET 10 and C#
- .NET MAUI for Windows and Android
- MVVM with CommunityToolkit.Mvvm
- SQLite through sqlite-net-pcl
- xUnit for domain and application unit tests
- GitHub Actions for CI

## Architecture Overview

The solution uses clean layered architecture. The domain layer contains entities, enums, value objects, and repository abstractions. The application layer contains DTOs, service interfaces, validators, and use case orchestration. The infrastructure layer contains SQLite persistence, repository implementations, Firebase Auth, Firestore synchronization, image processing, and sync state. The MAUI app layer contains pages, view models, Google OAuth/Drive data and image storage, navigation, dependency injection setup, resources, and localization.

Dependencies flow inward: App depends on Application and Infrastructure, Infrastructure depends on Application and Domain, Application depends on Domain, and Domain does not depend on other project layers.

## Project Structure

- `PersonalCollectionShelf.App` - MAUI UI, pages, view models, navigation, app resources, and JSON localization.
- `PersonalCollectionShelf.Domain` - media entity, enums, value objects, and repository abstractions.
- `PersonalCollectionShelf.Application` - DTOs, media item service, interfaces, validators, and orchestration logic.
- `PersonalCollectionShelf.Infrastructure` - SQLite persistence, Firebase REST clients, incremental sync, cloud image compression/cache, and DI registration.
- `PersonalCollectionShelf.Tests` - unit tests for domain and application behavior.
- `.agent` - project context files for future AI coding sessions.

## Roadmap

MVP:

- Local media library
- Add, edit, and delete media items
- Search, smart filters, and sorting
- English and Russian localization
- SQLite persistence
- Windows support
- Android core flow verified on a physical Samsung device

Already delivered beyond MVP:

- Statistics dashboard
- Import and export JSON
- Cover images
- Tags and categories
- Dark/light theming
- Responsive phone layouts with an overlay navigation drawer
- Type-specific book and movie metadata
- Structured movie credits, actor characters, multi-studio roles, genres, franchises, and related works
- Type-specific series/anime episode data, manga/comic publication data, and game platform/playtime data
- Browsable people-to-work role links and a dedicated series/cycles/universes explorer
- Dedicated tag and genre explorers with direct navigation to linked items
- Buffered native Android renderers for Library, People, Series/Universes, and genre/tag result lists
- Compact expandable search across Library, People, Tags, Genres, and Series/Universes, plus route-aware sidebar and category highlighting
- Dedicated local/account profile page accessible from the sidebar footer
- Multi-tab statistics for status, category, time, release year, decade, genre, country, and ratings
- Aspect-preserving page backgrounds, theme/accent controls, and independent Library/People grid density

Cloud foundation now included:

- Firebase Auth with Google and email/password
- Incremental Firestore synchronization with a durable local change queue, tombstones, checkpoints, and last-write-wins conflict handling
- Google Drive WebP image copies with local-original preservation and SHA-256 deduplication
- Versioned Google Drive backup and restore with SHA-256 validation
- Progress, ETA, cancellation, Android foreground notifications, and continuation when the app is moved to the background

Next versions:

- Richer sync and backup history
- Calendar and progress history
- Custom lists

Long-term ideas:

- Multi-device sync history
- Advanced recommendation views
- Custom media types and field templates
- Rich progress analytics

## Setup Instructions

1. Install .NET 10 SDK.
2. Install MAUI workloads with `dotnet workload install maui`.
3. Restore dependencies with `dotnet restore PersonalCollectionShelf.sln`.
4. Run tests with `dotnet test PersonalCollectionShelf.Tests/PersonalCollectionShelf.Tests.csproj`.
5. Run the Windows desktop app with `.\scripts\run-windows.cmd`.

Cloud setup is optional. Without configuration the app remains fully local. To enable it:

1. Create a Firebase project; enable Google and/or Email/Password in Authentication.
2. Create the default Firestore database. Firebase Storage is not used, so the Spark plan is sufficient.
3. In Google Cloud, enable Google Drive API, configure the OAuth consent screen, and create a Desktop OAuth client for the installed-app PKCE flow.
4. Copy `firebase.example.json`, fill `apiKey`, `projectId`, and `googleClientId`, then run:

```powershell
.\scripts\configure-cloud.ps1 -ConfigPath C:\secure\firebase.json
```

5. Deploy the per-user Firestore rules:

```powershell
Set-Location firebase
firebase deploy --config firebase.deploy.json --only firestore:rules
```

6. Rebuild both targets. The uncommitted configuration is packaged into each build and copied to that device's private app-data directory on first launch. Remove `PersonalCollectionShelf.App/Resources/Raw/firebase.json` before sharing source or build artifacts with someone else.

Media autofill is optional and uses no-cost sources suitable for this personal, non-commercial app:

- TMDB supplies search results, localized titles, descriptions, posters, credits, genres, studios, and years. Its own rating is not shown. Create an account, open **Settings → API**, request a developer API key for personal/non-commercial use, and copy the **API Read Access Token** (the long bearer token, not the short API key).
- IMDb ratings come from the official non-commercial daily dataset and need no key. The app caches the compressed dataset for 24 hours and reads the selected title's rating.
- Kinopoisk ratings use a free Kinopoisk API Unofficial token. Without that token, TMDB autofill and IMDb still work, but the second requested rating cannot be displayed.

Copy `metadata.example.json` to a private location, replace the TMDB placeholder and optionally the Kinopoisk placeholder, then run:

```powershell
.\scripts\configure-metadata.ps1 -ConfigPath C:\secure\metadata.json
```

Rebuild Windows and Android. `metadata.json` is ignored by Git and bundled only into local builds. Do not publish either token or a build containing your personal tokens. The required TMDB attribution is displayed in the title picker.

Additional personal/non-commercial catalog providers use the same search, local-cover, saved-rating,
and manual-refresh flow:

- **Open Library (books and manga):** no key is required. Set `openLibraryContactEmail` so requests carry the application name and contact required by Open Library's API guidance.
- **Google Books (books and manga):** in Google Cloud Console select this project, open **APIs & Services → Library**, enable **Books API**, then open **Credentials → Create credentials → API key**. Restrict the key to Books API and put it in `googleBooksApiKey`.
- **Comic Vine (comics and supplementary manga results):** sign in at `https://comicvine.gamespot.com/api/`, copy the personal API key shown on that page, and put it in `comicVineApiKey`. Comic Vine is non-commercial only; the app stores and displays a link to the selected source.
- **RAWG (games):** create a free account at `https://rawg.io/`, open `https://rawg.io/apidocs`, choose **Get API Key**, and set `rawgApiKey`. The free personal plan allows up to 20,000 requests per month and requires a RAWG source link on pages using its data; the app stores and displays that link.

Open Library works immediately. The other providers appear automatically after their corresponding
configuration values are present and the app is rebuilt. No provider is queried on startup, item opening,
or Firestore/Drive synchronization; calls happen only after selecting an autofill candidate or pressing
the refresh icon for one linked item.

The app supports both Windows and Android. Windows builds are self-contained for the Windows App SDK runtime; Android builds require the Android workload, SDK, and a connected device or emulator only when installing or running.

Useful commands:

```powershell
.\scripts\run-windows.cmd
.\scripts\run-windows.ps1
.\scripts\run-android.cmd
.\scripts\run-android.ps1
dotnet build PersonalCollectionShelf.App\PersonalCollectionShelf.App.csproj -f net10.0-windows10.0.19041.0 -r win-x64
dotnet build PersonalCollectionShelf.App\PersonalCollectionShelf.App.csproj -f net10.0-android
```

Android builds require JDK 11 or newer, Android SDK tooling, and a configured emulator or device.

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

## Sync and Backup Strategy

SQLite remains the working database on every device. Local inserts, edits, and deletions are recorded in a durable sync queue. A normal sync uploads only queued changes, pulls Firestore documents newer than the saved checkpoint, applies last-write-wins by UTC change time, and advances the checkpoint only after a successful pass. Tombstones ensure that deletions also reach devices that were offline. A full scan remains available internally for recovery and migration instead of being repeated during every normal sync.

Device-local file paths are never written into Firestore. Covers and person photos are handled separately: the source device retains its original; a maximum-768px WebP copy (quality 78) is deduplicated by SHA-256 and uploaded to the app-owned `Personal Collection Shelf Assets` folder in Google Drive; another device downloads that copy into its own `cloud-cache`. A remote copy never replaces an existing local original.

Google Drive is the independent recovery layer. Optimized images are stored once under content-addressed names, while each small backup ZIP contains portable JSON, references to those images, and a SHA-256 checksum. Retention keeps the 30 newest backups, 12 monthly representatives, and one representative for every year. Pressing Sync also creates a Drive backup when the Google Drive grant is available; Settings also provides explicit backup and restore actions. Sync, backup, and restore expose progress, estimated remaining time, and one red cancellation action. On Android an active operation is represented by a foreground notification so it can continue while the app is in the background. Manual JSON export remains self-contained and keeps the locally available image quality.

Firebase refresh tokens and Google OAuth refresh tokens are stored in MAUI SecureStorage. Firestore data is scoped to `users/{Firebase uid}` by the checked-in security rules, while Drive uses the narrow `drive.file` scope and can access only files created by this app. See `.agent/sync-plan.md` for implementation details and limitations.

## Contribution Notes

Keep code readable, asynchronous where I/O is involved, and aligned with the layered architecture. Code comments should be in English and used only when they clarify non-obvious decisions. Before meaningful changes, read the `.agent` context files and update them when project direction or architecture changes.

## License

This project is licensed under the MIT License. See `LICENSE` for details.
