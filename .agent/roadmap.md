# Roadmap

## MVP

- [x] Local media library
- [x] Add, edit, and delete media items
- [x] Search and filters (including smart filters)
- [x] English and Russian localization
- [x] SQLite persistence
- [x] Windows support
- [x] Android core flow (build, navigation, dashboard, create form, keyboard focus, movie fields, and cancel flow verified on a Samsung device)

## Delivered Beyond MVP

- Statistics dashboard (Dashboard and Statistics pages)
- Import and export JSON
- Cover images (picker plus persistence to app storage)
- Tags and categories with dynamic filters
- Library sorting
- Dark/light theming with dynamic Windows title bar and accent colors
- Creator, Studio, and Cast modeled as real entities
- Phone-responsive layouts across the primary navigation, forms, detail, management, and account screens
- Type-specific movie details (runtime, languages, country, age rating) with SQLite persistence
- Touch-friendly cross-platform controls and lightweight library loading

## Next Versions

- Firebase Auth (foundation implemented; needs a real Firebase project and account UI — see `sync-plan.md`)
- Firestore synchronization (soft-delete tombstones already in the domain and repository)
- Conflict resolution (last-write-wins by `UpdatedAt`, documented in `sync-plan.md`)
- Calendar and progress history
- Custom lists

## Long-Term Ideas

- Multi-device sync history
- Advanced recommendations and smart views
- Custom media types
- Custom list templates
- Backup and restore workflows
- Rich progress analytics
- Optional cloud media metadata providers

## Release Checklist (MVP)

- Complete final visual verification of all responsive Android screens on a device
- Bump `ApplicationDisplayVersion` and `ApplicationVersion` in `PersonalCollectionShelf.App.csproj`
- Recheck SQLitePCLRaw advisory (latest is still 2.1.11 as of 2026-07-19; no patched release yet)
