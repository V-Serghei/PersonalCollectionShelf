# Firebase, Google Drive, and Image Sync

Status: implementation completed 2026-07-29; real-project and two-device behavior still require owner testing.

## Local-first rules

- SQLite is the working store and the app stays usable without an account or network.
- Existing local rows always use `local-user`; the Firebase UID scopes the remote path and never replaces the local owner id.
- Signing in cannot hide or replace the current local library.

## Configuration

Runtime configuration is `firebase.json` in `FileSystem.AppDataDirectory`. A build may package an ignored
`PersonalCollectionShelf.App/Resources/Raw/firebase.json`; first launch copies it into private app data.
Use `scripts/configure-cloud.ps1` with a config based on `firebase.example.json`.

Required values: Firebase Web API key, project id, and a Google installed-app OAuth client id.
No refresh tokens, service-account files, client secrets, or user data may be committed.

## Authentication

- Email/password uses Firebase Identity Toolkit REST.
- Google uses system-browser OAuth 2.0 Authorization Code + PKCE with a loopback callback and scopes
  `openid email profile drive.file`; the resulting Google ID/access tokens are exchanged through Firebase
  `signInWithIdp`.
- Firebase and Google refresh tokens live in MAUI SecureStorage.
- The loopback flow is intentionally backend-free for a personal installation. It needs owner verification on
  the target Android browser/firmware; no embedded web view is used.

## Firestore

- Remote path: `users/{uid}/libraryData/{sha256(entityType:entityKey)}`.
- Documents contain portable JSON payload, content hash, UTC changed time, tombstone flag, and schema version.
- Local `CloudEntityStates` compares canonical SHA-256 hashes; unchanged rows do not generate writes.
- Pull is incremental by `changedAt`; first sync reads the collection. Merge is last-write-wins by UTC changed
  time, followed by push of remaining local changes.
- Physical removals detected after a previously synced row become tombstones. Existing domain soft deletes also
  become tombstones. Tombstones are retained so late devices converge.
- Device-local image paths are stripped from Firestore payloads.

## Images

- Covers, person portraits, and gallery photos are scanned separately.
- The source file remains untouched and is tracked as `OriginalPath` only on that device.
- Cloud copies use WebP, quality 78, maximum long edge 768px, and Google Drive filenames based on source SHA-256.
- Images live in the app-created `Personal Collection Shelf Assets` Drive folder. Firebase Storage is not used,
  so Firebase can remain on the Spark plan.
- Remote files download into `cloud-cache/{cloudHash}.webp` only when that device has no local original.
- A later remote image never overwrites a valid local original. A locally changed file is detected by content hash.

## Google Drive backup

- `drive.file` limits access to files/folders created by this app.
- Backups are ZIP files under `Personal Collection Shelf Backups` and contain `library.json` plus `sha256.txt`.
- Drive snapshot JSON stores references to content-addressed assets instead of embedding duplicate image bytes;
  restore resolves those references from `Personal Collection Shelf Assets`.
- Manual JSON exports remain self-contained and embed locally available images at local quality.
- Retention: 30 newest files, 12 monthly representatives, and one representative per year.
- Restore downloads the latest archive, validates SHA-256, asks for confirmation, and then uses normal import.

## Security and deployment

- `firebase/firestore.rules` allows only an authenticated matching UID.
- Google Drive access uses `drive.file`, so the app cannot browse unrelated user files.
- Deploy from `firebase/` with `firebase deploy --config firebase.deploy.json --only firestore:rules`.

## Known follow-ups

- Validate Google loopback authorization on the owner's Android device and Windows browser.
- Add OS background scheduling if automatic sync while the app is closed becomes necessary.
- Add server-time conflict metadata if users later operate devices with badly incorrect system clocks.
- Add an optional tombstone compaction protocol only after per-device acknowledgements exist.
