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
- Added Dashboard and Statistics pages with category/status summaries, monthly activity, and crash reporting.

## 2026-07-07

- Added cover image selection with covers persisted to app storage.
- Added creator, publisher, serial number, release year, and richer progress tracking fields to the edit form.
- Customized the Windows window title bar.

## 2026-07-08

- Widened the edit form to a two-column layout.
- Added sorting to the Library page.
- Simplified date/favorite inputs and added a per-movie cast list.
- Modeled Creator, Studio, and Cast as real entities (Person and Studio) with their own repositories instead of free text.

## 2026-07-09

- Refactored Dashboard, Library, and MediaDetails layouts; added edit and delete buttons to MediaDetails.
- Implemented dynamic theming with an appearance service: dark/light mode support, dynamic background and title bar colors on Windows, and accent color handling across UI components.
- Made MediaItemRepositoryTests handle database cleanup more robustly.

## 2026-07-19

- Updated `.agent` docs and README roadmap to reflect completed work (statistics, covers, tags, categories, JSON export/import, sorting, theming).
- Rechecked the SQLitePCLRaw advisory: latest published `SQLitePCLRaw.bundle_green` is still `2.1.11`, so no patched release exists yet; keep monitoring NuGet. `sqlite-net-pcl 1.11.285` is available as a newer line to evaluate when touching SQLite packages.
- Added `.agent/sync-plan.md` with the phased Firebase Auth + Firestore synchronization design (soft-delete tombstones, last-write-wins conflict resolution).
- Implemented the Firebase Auth foundation without UI: REST client for the Identity Toolkit API, options loaded from an uncommitted `firebase.json` in app data, token store abstraction with a MAUI SecureStorage implementation, and an updated `FirebaseAuthService` with sign-in/sign-up/sign-out plus token refresh. Everything stays disabled until Firebase is configured.
- Remaining before MVP release: verify the Android target end to end and bump `ApplicationDisplayVersion`/`ApplicationVersion` in the App csproj.
- Created the real Firebase project `pcs-shelf-2691-a6785` (Spark plan) with Email/Password sign-in enabled; config lives in the uncommitted app-data `firebase.json`. Verified sign-up end to end against the live project.
- Added the account section to Settings and then reworked it per user feedback: sign-in now opens a modal `SignInPage` (email, password with show/hide toggle, sign in / create account / cancel) instead of an inline form; Settings shows a Sign in button when signed out and email plus Sign out when signed in.
- Replaced the fake downscale/upscale background "blur" with a real-time GPU Gaussian blur: the Windows window now renders the background through a composition SpriteVisual with a Win2D `GaussianBlurEffect`, and the slider updates `Blur.BlurAmount` instantly (no PNG regeneration at all). Slider value is compressed by 0.6 so the full 0-40 range stays visually distinct. Added `Microsoft.Graphics.Win2D` (Windows target only); a SkiaSharp CPU-blur attempt was tried and removed the same day.
