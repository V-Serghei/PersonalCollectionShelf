# Architecture

The solution uses clean layered architecture with these projects:

- `PersonalCollectionShelf.Domain` contains entities, enums, value objects, and domain abstractions.
- `PersonalCollectionShelf.Application` contains DTOs, use case services, interfaces, validators, and orchestration logic.
- `PersonalCollectionShelf.Infrastructure` contains SQLite persistence, repository implementations, local storage services, dependency injection registration, and Firebase placeholders.
- `PersonalCollectionShelf.App` contains the .NET MAUI UI, pages, view models, navigation, resources, localization, and app startup.
- `PersonalCollectionShelf.Tests` contains unit tests for domain and application logic.

Dependency direction:

- Domain depends on no project layer.
- Application depends on Domain.
- Infrastructure depends on Application and Domain.
- App depends on Application, Domain, and Infrastructure.
- Tests depend on Application and Domain.

Important decisions:

- SQLite is the local source of truth for the MVP.
- Deletes are represented as soft deletes through `DeletedAt`, which supports future sync.
- Repository abstractions live in Domain so persistence can be swapped later.
- Firebase classes are placeholders only and must not include credentials or real project configuration yet.
- View models use CommunityToolkit.Mvvm and should stay thin.
- Visible UI text is loaded from JSON localization files through `JsonLocalizationService`.
- Book- and movie-specific metadata use separate 1:1 detail entities instead of expanding the shared `MediaItem` table.
- Series/anime use `EpisodicDetails`, manga/comics use `GraphicPublicationDetails`, and games use
  `GameDetails`; cross-type people, studio, tag, collection, and relation graphs remain shared.
- MAUI pages use compact layouts below 700 device-independent pixels; phones use an overlay Shell flyout while wider devices keep the locked sidebar.
- Android phones open detail and editor pages modally through `AppNavigation` because the MAUI 10 Shell fragment renderer can fail when pushing global routes after activity recreation; desktop keeps Shell routes.
- Library pages use lightweight media summaries with batched tag loading. Full contribution, relation, collection, and type-detail graphs load only when a single item is opened.
