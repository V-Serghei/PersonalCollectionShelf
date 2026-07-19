# Roadmap

## MVP

- [x] Local media library
- [x] Add, edit, and delete media items
- [x] Search and filters (including smart filters)
- [x] English and Russian localization
- [x] SQLite persistence
- [x] Windows support
- [ ] Android support (target builds are opt-in; UI has not been verified on a device/emulator since the July 6+ redesign)

## Delivered Beyond MVP

- Statistics dashboard (Dashboard and Statistics pages)
- Import and export JSON
- Cover images (picker plus persistence to app storage)
- Tags and categories with dynamic filters
- Library sorting
- Dark/light theming with dynamic Windows title bar and accent colors
- Creator, Studio, and Cast modeled as real entities

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

- Verify Android build and UI on emulator/device
- Bump `ApplicationDisplayVersion` and `ApplicationVersion` in `PersonalCollectionShelf.App.csproj`
- Recheck SQLitePCLRaw advisory (latest is still 2.1.11 as of 2026-07-19; no patched release yet)
