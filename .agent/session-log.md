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
- Dead-UI cleanup pass: fixed the clipped media-type filter chips (the row was 34px tall and cut the pills; also the untyped DataTemplate broke `DisplayName` bindings so the chips rendered as empty ovals), implemented the grid/list view toggle on Library (persisted in preferences, active state highlighted, new list row template), made the sidebar category rows navigate to Library pre-filtered by media type via `IQueryAttributable`, replaced the mock "Alex Morgan / 14 items collected" sidebar profile with the real signed-in email (or localized "Local profile") plus a live item count, localized the sidebar strings, and marked the Letterboxd/Goodreads/Steam/MyAnimeList import rows as localized "Coming soon" placeholders instead of dead arrow buttons.
- Replaced the fake downscale/upscale background "blur" with a real-time GPU Gaussian blur: the Windows window now renders the background through a composition SpriteVisual with a Win2D `GaussianBlurEffect`, and the slider updates `Blur.BlurAmount` instantly (no PNG regeneration at all). Slider value is compressed by 0.6 so the full 0-40 range stays visually distinct. Added `Microsoft.Graphics.Win2D` (Windows target only); a SkiaSharp CPU-blur attempt was tried and removed the same day.

## 2026-07-27

- Added phone-responsive navigation: the desktop sidebar remains locked on larger devices, while phones use the native Shell flyout with a navigation bar and automatically close the drawer after selection.
- Made the Dashboard responsive by removing its fixed 920px width, allowing the search header to fill available space, stacking summary cards into a 2x2 phone grid, and reducing category cards to one column on narrow screens.
- Completed a phone-layout pass across Dashboard, Library, Statistics, Settings, media details, media editing, sign-in, people management, and category management.
- Fixed localized Picker options rendering their generated record representation instead of their display text.
- Added first-class MovieDetails persistence, validation, edit fields, and detail presentation for runtime, original/viewing languages, country, and age rating. Directors, studios, and actors continue to use the shared person/studio contribution graph.
- Built, installed, and visually checked the responsive Dashboard on a Samsung Android phone; made the edit form's compact layout also key off the phone idiom because some devices report allocated width in physical pixels.
- Replaced Windows-only Segoe MDL2 glyphs and placeholder question marks with cross-platform symbols, enlarged touch targets, made dashboard summary/category/favorite cards actionable, and removed the dead Settings search field.
- Fixed Android form input by using resize-on-keyboard plus focus-aware scrolling, and verified text entry stays visible above the Samsung keyboard.
- Worked around a verified MAUI Shell Android fragment failure by routing editor/detail/management pages through modal navigation on Android phones; verified Add Item, Picker selection, Movie fields, Cancel, and repeat Add Item taps on device.
- Made library loading lightweight: one media query plus batched tag links, in-memory filtering/debounced search, and a direct count query for the flyout profile instead of fully hydrating every item multiple times.
- Expanded movie creation into a structured graph: separate person credits for directors, screenwriters, producers, cinematographers, composers, and actors; actor character and billing metadata; many-to-many studio credits with production, distribution, VFX, animation, and broadcast roles; plus movie genres, franchises, and related-work links in the phone form and detail page.
- Added SQLite persistence and legacy movie-studio migration for `MediaStudioCredit`. Per the user's request, these latest changes were not launched or test-run locally.
- Extended the common graph UI to Series and Anime (crew, actors/voice actors, studios, genres,
  franchises, relations) and to Manga/Comic (authors, translators, illustrators, editors, genres,
  cycles/universes, relations). Added persisted episodic, graphic-publication, and game detail tables.
- Added game developer selection through the shared person picker, game studio roles, a person's
  reverse work/role list, and a dedicated series/cycles/universes explorer page.
- Added a dedicated tag explorer that groups library items by tag and opens their detail pages.
- Strengthened Android keyboard handling by attaching focus scrolling to dynamically created form
  inputs and repeating the scroll after IME resize animation. Changes remain intentionally unrun.
- Fixed the remaining Android IME overlay case by adding focus-driven scroll extents to both the
  main media form and the person-picker overlay. The spacer appears only while a field is focused,
  so description, notes, save actions, and picker actions can scroll fully above overlay keyboards.
- Changed keyboard-aware focus scrolling to run exactly once per newly focused input. Manual
  scrolling is no longer overridden while the same field remains selected.
- Reworked navigation state to use Shell routes instead of obsolete fixed menu indexes; added
  independently highlighted category rows and automatic scrolling to the selected library type.
- Redesigned Tags and Series/Universes as searchable card lists. Library, People, Tags, and
  Series/Universes now expose search from a compact button and collapse it when not needed.
- Added People to the primary sidebar and a clickable profile footer backed by a dedicated profile
  page with account/library summary and links to contributors and account settings.

## 2026-07-29

- Shortened the Android launcher label to `Shelf` and explicitly connected the generated regular
  and round launcher icon resources in the Android manifest.
- Made the person picker's create-and-add action report missing names and failures, show progress,
  and reliably attach the created person to the pending contribution role.
- Reworked Android focus-aware form positioning so a manual scroll cancels a pending automatic
  move; entering text can request positioning again without an animated scroll fighting the user.
- Removed hardcoded English from Dashboard, Statistics, and appearance settings; localized month
  labels, collection kinds, people roles, media types, and relationship kinds.
- Kept Firebase behavior unchanged: configuration is still device-local, Google sign-in is not yet
  implemented, and Firestore synchronization remains a placeholder.
- Added first-class Cartoon and AnimatedSeries media types while preserving existing enum values;
  cartoons reuse movie metadata and credits, animated series reuse episodic metadata and screen
  production credits, and both appear with Anime in navigation, filters, statistics, and localization.
- Replaced the Firestore placeholder with an incremental row sync journal, SHA-256 change detection,
  UTC last-write-wins merging, deletion tombstones, Firebase REST transport, and per-user security rules.
- Added Google installed-app OAuth with PKCE, Firebase Google token exchange, SecureStorage-backed Google
  refresh tokens, and the `drive.file` grant shared by Windows and Android builds.
- Added separate image synchronization: local originals are never modified, while 768px quality-78 WebP
  copies are deduplicated, uploaded to Firebase Storage, and downloaded into a distinct cloud cache.
- Upgraded portable JSON to version 3 with embedded media covers and added checksummed ZIP backups to
  Google Drive, latest-backup restore, and 30-recent/12-monthly/yearly retention.
- Added ignored build-time cloud configuration packaging, a configuration helper script, example config,
  Firebase deployment files, synchronized English/Russian cloud UI, and the missing Settings sync controls.
- Verified both Windows and Android targets compile and all 38 automated tests pass after the cloud
  implementation; no app or phone was launched.
- Copied the existing device-local Firebase config into the ignored MAUI Raw asset so future Windows and
  Android builds share it. The legacy file contains only `apiKey` and `projectId`; add `googleClientId`
  before Google sign-in, Drive image sync, and Drive backup can be enabled.
- Removed the Blaze dependency: Firebase Storage and its rules/configuration were removed. Firebase Spark
  now handles only Auth and Firestore, while content-addressed compressed images are stored in the app-owned
  Google Drive assets folder through the narrow `drive.file` scope.
- Added no-cost, non-commercial screen-title autofill: TMDB search/details/posters, official IMDb daily
  ratings with a 24-hour local cache, and optional Kinopoisk API Unofficial ratings. Candidate cards show
  localized/original titles, year, leading cast, and rating before filling the structured editor.
- Persisted provider IDs, rating snapshots, vote counts, and refresh timestamps through SQLite, DTOs,
  JSON import/export, Firestore row synchronization, and details UI. Personal ratings remain separate.
- Added ignored `metadata.json` packaging, an example file, setup script, English/Russian UI, Android
  internet permission, and required TMDB attribution. Windows compilation completes with zero warnings.
- Simplified personal tracking around status: removed manual current/total progress inputs and percentage
  bars from editor, library, dashboard, and details. Saving derives an internal complete/not-complete value
  from the selected status. Dashboard status cards open live filtered library views, the library title now
  reflects the selected status, and resetting the library also clears a previously selected status filter.
- Limited user-visible external scores to the requested pair: IMDb and Kinopoisk. TMDB remains the
  metadata/poster/search provider, but its rating is no longer shown in candidate, editor, or detail UI.
- Added an unlabeled refresh icon to TMDB-linked detail cards. The command refreshes exactly one item's
  TMDB description/year/genres/credits/type details and IMDb/Kinopoisk rating snapshots, downloading a
  poster only when none exists. Personal rating, status, notes, dates, favorite state, and existing cover
  remain untouched; no provider calls occur on startup, item opening, or cloud synchronization.
- Extended the same workflow to books, manga, comics, and games. Open Library works keylessly with a
  contact-aware User-Agent; Google Books, Comic Vine, and RAWG are optional API-key sources; RAWG replaced IGDB to avoid Twitch 2FA,
  automatically renewed Twitch client-credentials tokens. Added combined candidate UI, provider-specific
  parsers, cover download, structured field mapping, source attribution links, generic two-rating storage,
  import/export and Firestore-compatible persistence, and one-item manual refresh.

## 2026-07-30

- Fixed the Settings language picker regression by keeping its option collection stable during localization
  refresh, preventing a reentrant Picker update from freezing the UI before the preference can persist.
- Consolidated media descriptions into the hero card: long text stays at a four-line preview with localized
  show-all/show-less controls, and the duplicate full-description section was removed.
- Hid the original-title line in the media hero card when no original title is stored instead of showing a
  localized "not set" placeholder.
