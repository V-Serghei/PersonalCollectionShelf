# Project Context

PersonalCollectionShelf is an offline-first cross-platform personal collection tracker for movies, series, books, manga, comics, games, anime, and other media. It targets Windows and Android through .NET MAUI.

The product helps users organize their media library, track progress, manage statuses, add ratings and notes, and keep enough metadata for future synchronization across desktop and mobile devices.

The current MVP direction is local-first: SQLite stores the user's library, and the app remains useful without an internet connection. Account and cloud sync boundaries are prepared for Firebase Auth and Firestore, but real Firebase credentials and configuration are intentionally not present.

Long-term direction includes robust cloud sync, conflict resolution, statistics, import/export, cover images, tags, custom lists, calendar views, and progress history.
