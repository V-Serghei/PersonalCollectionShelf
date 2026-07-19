# Firebase Auth and Firestore Sync Plan

Status: Phase 1 foundation implemented on 2026-07-19 (no UI yet). Phases 2+ are not started.

## Principles

- Offline-first stays: SQLite remains the source of truth on the device. Sync reconciles, it never gates local usage.
- No secrets in the repo. Firebase configuration lives in an uncommitted `firebase.json` file in the app data directory (`FileSystem.AppDataDirectory`). When the file is absent, all cloud features stay disabled and the app behaves exactly as today.
- No official Firebase SDK for .NET MAUI is used; Auth and Firestore are accessed through their public REST APIs with plain `HttpClient`.

## Configuration

`firebase.json` in the app data directory:

```json
{
  "apiKey": "<web API key>",
  "projectId": "<firebase project id>"
}
```

Loaded by `FirebaseOptions.Load` in Infrastructure. `FirebaseOptions.IsConfigured` drives every feature toggle.

## Phase 1 — Firebase Auth foundation (implemented)

- `IAuthService` extended with `SignInAsync`, `SignUpAsync`, `SignOutAsync` returning `AuthResultDto` with localization error keys (`Auth.Error.*`).
- `FirebaseAuthClient` (Infrastructure) calls the Identity Toolkit REST API:
  - Sign up: `POST https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=API_KEY`
  - Sign in: `POST https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=API_KEY`
  - Refresh: `POST https://securetoken.googleapis.com/v1/token?key=API_KEY`
- `IAuthTokenStore` abstraction stores the session (`userId`, `email`, `idToken`, `refreshToken`, `expiresAtUtc`). Infrastructure ships an in-memory store; the App layer registers `SecureStorageAuthTokenStore` backed by MAUI SecureStorage so refresh tokens never sit in plain files.
- `FirebaseAuthService` refreshes the id token on demand and reports signed-in state to `SyncService`.
- IMPORTANT: `GetCurrentUserIdAsync` intentionally keeps returning `local-user` even when signed in. All existing rows are owned by `local-user`; switching the id to the Firebase uid would make the local library invisible. The mapping from `local-user` to the Firebase uid happens at sync time (Phase 3), not at auth time.

## Phase 2 — Account UI (needs UX pass first, per development rules)

- Settings page: account section with email/password sign-in, sign-up, sign-out, and current-account display. Progressive disclosure: collapsed to a single row when signed out.
- All strings via `en.json`/`ru.json` (the `Auth.*` keys already exist).
- Requires a real Firebase project to test end to end.

## Phase 3 — Firestore synchronization

- Firestore REST API (`https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents/...`) with the id token as Bearer auth.
- Document layout: `users/{uid}/mediaItems/{itemId}`, plus `users/{uid}/people/{id}` and `users/{uid}/studios/{id}`.
- Push: upload local rows with `UpdatedAt` newer than `LastSyncedAt`, including soft-deleted rows (tombstones with `DeletedAt` set). Soft delete already exists end to end (`MediaItem.MarkDeleted`, repository filters on `DeletedAt == null`).
- Pull: query documents with server `updatedAt > LastSyncedAt`, upsert locally.
- Track `LastSyncedAt` per collection in a local `sync_state` table.

## Phase 4 — Conflict resolution

- Strategy: last-write-wins by `UpdatedAt` (UTC) per record. Deletion participates: a tombstone with the newest `UpdatedAt` wins over an edit.
- Tombstones are kept, not purged, so late-syncing devices converge; add a purge policy later (e.g. tombstones older than 90 days after all devices synced).
- Document the strategy in README when implemented.

## Open items

- Create the Firebase project and enable Email/Password auth (owner action; nothing to commit).
- Decide whether Person/Studio sync happens in the first sync release or later.
- Firestore security rules: restrict `users/{uid}/**` to `request.auth.uid == uid`.
