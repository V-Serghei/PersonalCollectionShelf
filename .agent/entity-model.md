# Entity Model and Relationships

Target data model for the deep-relations redesign requested on 2026-07-19. This document is the
source of truth for how entities relate; implement it incrementally (Book flow first) and keep it
updated as phases land.

## Core principles

1. **Minimal validity.** Every record deserves to exist with only its identity field filled:
   an item needs only Title + MediaType, a person needs only Name, a collection needs only Name.
   Everything else is optional enrichment.
2. **No comma-separated data.** Anything plural is a real table with real rows (tags, authors,
   relations). UI adds tags with Enter and removes them with a per-chip delete button.
3. **People, studios, tags, collections are first-class entities** shared across items and
   deduplicated by name search ("add author" opens a search-or-create picker).
4. **Per-type detail tables.** The common `MediaItem` stays lean; each media type gets its own
   optional 1:1 detail table (`BookDetails` first). This is how "each category is unique in
   content" is modeled without a 100-column table.
5. **Sync-ready.** New entities carry `CreatedAt`/`UpdatedAt`/`DeletedAt` (soft delete) so the
   Firestore sync engine can treat them uniformly.

## Entity map

```
MediaItem ────< MediaContribution >──── Person ────< PersonRelation >──── Person
    │                (role)                │
    │                                      └─ photo, years, tagline, bio
    ├────< MediaItemTag >──── Tag
    ├────< MediaRelation >──── MediaItem        (sequel/adaptation/... graph)
    ├────< MediaStudioCredit >──── Studio       (production/distribution/VFX/...)
    ├────< MediaCollectionEntry >──── MediaCollection ── ParentCollectionId (tree)
    ├──── BookDetails (1:1, only when MediaType = Book)
    ├──── MovieDetails (1:1, only when MediaType = Movie)
    ├──── EpisodicDetails (1:1, Series / Anime)
    ├──── GraphicPublicationDetails (1:1, Manga / Comic)
    ├──── GameDetails (1:1, Game)
    └──── (future) GameDetails / AnimeDetails ...
```

## Entities

### MediaItem (extended, existing)
- Required: `Title`, `MediaType`.
- `Rating` becomes `decimal?` in **0.0–10.0 with one decimal** (was int). UI offers a 10-star
  picker (whole stars) or manual numeric entry with one decimal (e.g. 7.5).
- `Tags` string column is **deprecated**: migrated on startup into `Tag`/`MediaItemTag` rows,
  then cleared. Never write it again.
- `CreatorId`/`StudioId` single links are **deprecated** in favor of `MediaContribution`
  (kept temporarily for backward compatibility; migrate then remove).
- Everything else (status, progress, dates, cover, notes, favorite) unchanged.

### Person (extended)
- Required: `Name`.
- Optional: `SortName`, `BirthYear`, `DeathYear`, `PhotoPath` (stored in app data like covers),
  `Tagline` (slogan/one-liner), `Description` (bio), `Notes`.
- Soft delete + timestamps for sync.
- A person has **no global role** — roles live on contributions (the same person can be an
  author on one item, a director on another, and just a relative of someone).

### PersonRelation (new)
Person ↔ Person edges: `PersonId`, `RelatedPersonId`, `Kind`, optional `Notes`.
`PersonRelationKind`: Spouse, Partner, Child, Parent, Sibling, Relative, Colleague, Other.
Store one row per direction pair; when adding Child, the inverse Parent edge is implied by
lookup (query both directions), not duplicated.

### MediaContribution (new)
Item ↔ Person edges with a role: `MediaItemId`, `PersonId`, `Role`, `SortOrder`,
optional `Details` (e.g. character name for actors — supersedes the current cast table).
`ContributionRole`: Author, Translator, Illustrator, Editor, Director, Screenwriter, Actor,
Composer, Artist, Developer, VoiceActor, Other.
Multiple authors = multiple rows with Role=Author ordered by `SortOrder`.

### Tag (new) + MediaItemTag (new)
- `Tag`: `Id`, `UserId`, `Name` (unique per user, case-insensitive).
- `MediaItemTag`: `MediaItemId` + `TagId`.
- Startup migration: split legacy comma string, trim, dedupe, create rows, clear the column.

### MediaCollection (new) + MediaCollectionEntry (new)
Series/sagas/cycles as a **tree**:
- `MediaCollection`: `Id`, `UserId`, `Name` (required), `Kind`
  (`Series`, `Saga`, `Cycle`, `Trilogy`, `Universe`, `Custom`), `Description`,
  `ParentCollectionId` (nullable → tree: a cycle can contain sub-series).
- `MediaCollectionEntry`: `CollectionId`, `MediaItemId`, `Position` (number within the series;
  nullable when unknown).

### MediaRelation (new)
Direct item ↔ item graph for links that aren't series membership:
`FromItemId`, `ToItemId`, `Kind`, optional `Notes`.
`MediaRelationKind`: Sequel, Prequel, Adaptation, Remake, SpinOff, SameUniverse, Translation,
Companion, Other. Stored one-directional; inverse is derived (Sequel ⇄ Prequel).

### BookDetails (new, 1:1 with MediaItem where MediaType = Book)
- `MediaItemId` (PK/FK), `Publisher` (StudioId link), `Edition`, `EditionYear` (this printing),
  `OriginalPublicationYear` (first published by the author), `TranslationYear`,
  `OriginalLanguage`, `Language`, `PageCount`, `Isbn10`, `Isbn13`, `Format`
  (Hardcover / Paperback / Ebook / Audiobook / Other).
- Authors and translators are contributions, not columns here.
- Series membership is a collection entry, not a column here.

### MovieDetails (implemented, 1:1 with MediaItem where MediaType = Movie)
- `MediaItemId` (PK/FK), `RuntimeMinutes`, `OriginalLanguage`, `Language`,
  `CountryOfOrigin`, and `AgeRating`.
- Directors, screenwriters, producers, cinematographers, composers, and actors are
  `MediaContribution` rows. Actor `Details` stores the character and `CreditedAs` stores the
  billing name.
- Studios are `MediaStudioCredit` rows with a role: ProductionCompany, Distributor,
  VisualEffects, AnimationStudio, Broadcaster, or Other. The legacy `StudioId` keeps the first
  production company for backward compatibility.
- Genres use shared `Tag` rows with `Kind=Genre`; franchises use `MediaCollection`; sequels,
  adaptations, remakes, and other links use `MediaRelation`.

### Studio (extended later)
Optional: `FoundedYear`, `Country`, `Description`, `LogoPath`, soft delete + timestamps.
Used as publisher for books, studio for movies/anime, developer for games.

### Other type details (implemented)
- `EpisodicDetails`: seasons, episodes, episode runtime, network/service, airing status,
  source material, and original language for Series and Anime.
- `GraphicPublicationDetails`: volumes, chapters/issues, reading direction, color flag,
  publication status, imprint, and original language for Manga and Comic.
- `GameDetails`: platform, main/completionist hours, game mode, engine, and region.
- All of these reuse tags/genres, ordered collection membership (series, cycles, universes),
  media relations, people contributions, and studio credits rather than string lists.

## Book add flow (the reference implementation — polish this first)

1. Required: Title, MediaType=Book. Save is allowed immediately after that.
2. Authors: "Add author" opens a picker window — search existing people by name as you type,
   or create a new person inline (Name only is enough). Added authors show as chips with a
   delete button and can be reordered. Same picker is reused for translators/illustrators.
3. Tags: text entry, Enter commits a chip, each chip has ✕. No commas anywhere.
4. Rating: 10-star row (tap = whole star) plus a numeric entry accepting one decimal (7.5);
   the two stay in sync (stars round to nearest whole).
5. Book details section: edition, years (edition / original publication / translation),
   languages, page count, ISBN, format, publisher (studio picker like the author picker).
6. Series: pick or create a MediaCollection (with kind), set position.
7. Related items: add MediaRelation rows via an item picker.

## Migration notes

- sqlite-net `CreateTableAsync` adds missing columns and creates new tables automatically.
- `Rating` int→double reuses the same column (SQLite storage classes make old int rows readable).
- Legacy `Tags` string → Tag rows: one-time migration in `LocalDatabaseService.InitializeAsync`.
- Legacy `MediaItemCastMemberRecord` → `MediaContribution` (Role=Actor, Details=character):
  one-time migration, then drop usage.
- Legacy `CreatorId`/`StudioId` → contributions/publisher link: migrate when the Book UI lands.

## Implementation phases

1. **P1 (data foundation):** rating double, Tag/MediaItemTag + migration, Person extensions,
   PersonRelation, MediaContribution, MediaCollection(+Entry), MediaRelation, BookDetails —
   entities, records, repositories, DI, tests. UI keeps working unchanged.
2. **P2 (Book UI):** Edit page for Book per the flow above (author picker window, tag chips,
   star+decimal rating, book details, series picker). Details page shows the new data.
3. **P3 (People UI):** person page (photo, years, bio, relations, their works across the library).
4. **P4:** migrate cast/creator/studio legacy fields fully onto contributions; remove dead columns.
5. **P5 (started):** MovieDetails persistence and phone-ready movie form implemented. Continue with
   Game/Anime/Manga details, collection tree UI, relation graphs,
   include everything in Firestore sync (each new table syncs like media items, soft deletes
   included).
