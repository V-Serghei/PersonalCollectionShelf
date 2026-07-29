using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Pages;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class MediaDetailsViewModel : BaseViewModel
{
    private readonly IMediaItemService _mediaItemService;
    private readonly IAuthService _authService;
    private readonly IMediaMetadataService _mediaMetadataService;
    private Guid? _currentItemId;

    public MediaDetailsViewModel(
        IMediaItemService mediaItemService,
        IAuthService authService,
        IMediaMetadataService mediaMetadataService,
        ILocalizationService localizationService)
        : base(localizationService)
    {
        _mediaItemService = mediaItemService;
        _authService = authService;
        _mediaMetadataService = mediaMetadataService;
    }

    private MediaItemDto? _item;

    private string _errorMessage = string.Empty;

    public MediaItemDto? Item
    {
        get => _item;
        set
        {
            if (SetProperty(ref _item, value))
            {
                RefreshItemProperties();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanRefreshOnlineMetadata => Item?.TmdbId.HasValue == true &&
        Item.MediaType is MediaType.Movie or MediaType.Cartoon or MediaType.Series or MediaType.AnimatedSeries or MediaType.Anime;

    public string RefreshMetadataTooltip => T("Metadata.Action.Refresh");

    public string PageTitle => Item?.Title ?? T("Details.Title");

    public string TypeLabel => T("Details.TypeLabel");

    public string StatusLabel => T("Details.StatusLabel");

    public string ProgressLabel => T("Details.ProgressLabel");

    public string RatingLabel => T("Details.RatingLabel");

    public string ExternalRatingsLabel => T("Metadata.Label.ExternalRatings");

    public bool HasExternalRatings => Item?.ImdbRating.HasValue == true ||
                                      Item?.KinopoiskRating.HasValue == true;

    public string ExternalRatingsValue
    {
        get
        {
            if (Item is null)
            {
                return T("Common.NotSet");
            }

            var values = new List<string>();
            AddExternalRating(values, "IMDb", Item.ImdbRating, Item.ImdbVoteCount);
            AddExternalRating(values, T("Metadata.Kinopoisk"), Item.KinopoiskRating, Item.KinopoiskVoteCount);
            return values.Count == 0 ? T("Common.NotSet") : string.Join("   ·   ", values);
        }
    }

    public string NotesLabel => T("Details.NotesLabel");

    public string OriginalTitleLabel => T("Details.OriginalTitleLabel");

    public string DescriptionLabel => T("Details.DescriptionLabel");

    public string CategoryLabel => T("Details.CategoryLabel");

    public string TagsLabel => T("Details.TagsLabel");

    public string CreatorLabel => Item?.MediaType is MediaType.Movie or MediaType.Series or MediaType.Cartoon or MediaType.AnimatedSeries
        ? T("Edit.Label.Creator.Director")
        : T("Details.CreatorLabel");

    public string PublisherLabel => Item?.MediaType is MediaType.Movie or MediaType.Series or MediaType.Anime or MediaType.Cartoon or MediaType.AnimatedSeries
        ? T("Edit.Label.Studio")
        : T("Details.PublisherLabel");

    public string SerialNumberLabel => T("Details.SerialNumberLabel");

    public string CastLabel => T("Details.CastLabel");

    public bool ShowCast => Item is not null && Item.MediaType is MediaType.Movie or MediaType.Series or MediaType.Cartoon or MediaType.AnimatedSeries;

    public bool ShowBookDetails => Item?.MediaType == MediaType.Book;

    public bool ShowMovieDetails => Item?.MediaType is MediaType.Movie or MediaType.Cartoon;

    public bool ShowScreenProductionDetails => Item?.MediaType is MediaType.Movie or MediaType.Series or MediaType.Anime or MediaType.Cartoon or MediaType.AnimatedSeries;

    public bool ShowEpisodicDetails => Item?.MediaType is MediaType.Series or MediaType.Anime or MediaType.AnimatedSeries;

    public bool ShowGraphicPublicationDetails => Item?.MediaType is MediaType.Manga or MediaType.Comic;

    public bool ShowGameDetails => Item?.MediaType == MediaType.Game;

    public string BookDetailsSectionTitle => T("Edit.Section.BookDetails");

    public string MovieDetailsSectionTitle => T("Edit.Section.MovieDetails");

    public string ScreenProductionSectionTitle => Item?.MediaType switch
    {
        MediaType.Movie => MovieDetailsSectionTitle,
        MediaType.Cartoon => T("Edit.Section.CartoonDetails"),
        _ => T("Edit.Section.MoviePeople")
    };

    public string EpisodicDetailsSectionTitle => T("Edit.Section.EpisodicDetails");

    public string GraphicDetailsSectionTitle => T("Edit.Section.GraphicDetails");

    public string GameDetailsSectionTitle => T("Edit.Section.GameDetails");

    public string AuthorsLabel => T("Edit.Label.Authors");

    public string TranslatorsLabel => T("Edit.Label.Translators");

    public string GenresLabel => T("Edit.Label.Genres");

    public string CollectionLabel => T("Edit.Section.Collections");

    public string RelationsLabel => T("Edit.Section.Relations");

    public string StartDateLabel => T("Details.StartDateLabel");

    public string FinishDateLabel => T("Details.FinishDateLabel");

    public string ReleaseYearLabel => T("Details.ReleaseYearLabel");

    public string FavoriteLabel => T("Details.FavoriteLabel");

    public string CreatedAtLabel => T("Details.CreatedAtLabel");

    public string UpdatedAtLabel => T("Details.UpdatedAtLabel");

    public string BackButtonText => T("Common.Back");

    public string EditButtonText => T("Details.EditButton");

    public string DeleteButtonText => T("Details.DeleteButton");

    public string DeleteConfirmTitle => T("Details.DeleteConfirmTitle");

    public string DeleteConfirmMessage => T("Details.DeleteConfirmMessage");

    public string DeleteConfirmButtonText => T("Details.DeleteConfirmButton");

    public string DeleteCancelButtonText => T("Common.Cancel");

    public string DetailsSectionTitle => T("Details.Title");

    public string OriginalTitleValue => Item?.OriginalTitle ?? T("Common.NotSet");

    public string DescriptionValue => Item?.Description ?? T("Common.NotSet");

    public string CategoryValue => Item?.Category ?? T("Common.NotSet");

    public string TagsValue => Item?.Tags ?? T("Common.NotSet");

    public string CreatorValue => Item?.Creator ?? T("Common.NotSet");

    public string PublisherValue => Item?.Publisher ?? T("Common.NotSet");

    public string SerialNumberValue => Item?.SerialNumber ?? T("Common.NotSet");

    public string CastValue => Item is null || Item.Cast.Count == 0 ? T("Common.NotSet") : string.Join(", ", Item.Cast);

    public string AuthorsValue => JoinContributors(ContributionRole.Author);

    public string TranslatorsValue => JoinContributors(ContributionRole.Translator);

    public string DirectorsLabel => T("Edit.Label.Directors");

    public string ScreenwritersLabel => T("Edit.Label.Screenwriters");

    public string ProducersLabel => T("Edit.Label.Producers");

    public string CinematographersLabel => T("Edit.Label.Cinematographers");

    public string ComposersLabel => T("Edit.Label.Composers");

    public string CastingDirectorsLabel => T("Edit.Label.CastingDirectors");

    public string ProductionDesignersLabel => T("Edit.Label.ProductionDesigners");

    public string ActorsLabel => T("Edit.Label.Actors");

    public string MovieStudiosLabel => T("Edit.Section.MovieStudios");

    public string DirectorsValue => JoinContributors(ContributionRole.Director);

    public string ScreenwritersValue => JoinContributors(ContributionRole.Screenwriter);

    public string ProducersValue => JoinContributors(ContributionRole.Producer);

    public string CinematographersValue => JoinContributors(ContributionRole.Cinematographer);

    public string ComposersValue => JoinContributors(ContributionRole.Composer);

    public string CastingDirectorsValue => JoinContributors(ContributionRole.CastingDirector);

    public string ProductionDesignersValue => JoinContributors(ContributionRole.ProductionDesigner);

    public string ActorsValue
    {
        get
        {
            var actors = Item?.Contributions
                .Where(value => value.Role is ContributionRole.Actor or ContributionRole.VoiceActor)
                .OrderBy(value => value.SortOrder)
                .Select(value =>
                {
                    var character = string.IsNullOrWhiteSpace(value.Details) ? null : value.Details;
                    var creditedAs = string.IsNullOrWhiteSpace(value.CreditedAs) ||
                                     string.Equals(value.CreditedAs, value.PersonName, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : value.CreditedAs;
                    return character is null && creditedAs is null
                        ? value.PersonName
                        : creditedAs is null
                            ? $"{value.PersonName} — {character}"
                            : character is null
                                ? $"{value.PersonName} ({creditedAs})"
                                : $"{value.PersonName} — {character} ({creditedAs})";
                })
                .ToList();
            return actors is null || actors.Count == 0 ? T("Common.NotSet") : string.Join(Environment.NewLine, actors);
        }
    }

    public string MovieStudiosValue
    {
        get
        {
            var studios = Item?.StudioCredits
                .OrderBy(value => value.Role)
                .ThenBy(value => value.SortOrder)
                .Select(value => $"{value.StudioName} — {T($"StudioRole.{value.Role}")}")
                .ToList();
            return studios is null || studios.Count == 0 ? T("Common.NotSet") : string.Join(Environment.NewLine, studios);
        }
    }

    public string GenresValue => Item is null || Item.Genres.Count == 0 ? T("Common.NotSet") : string.Join(", ", Item.Genres);

    public string BookMetadataValue
    {
        get
        {
            var book = Item?.BookDetails;
            if (book is null)
            {
                return T("Common.NotSet");
            }

            var values = new[]
            {
                book.Subtitle,
                book.Edition,
                book.EditionYear?.ToString(CultureInfo.InvariantCulture),
                book.Format.HasValue ? T($"BookFormat.{book.Format.Value}") : null,
                book.PageCount.HasValue ? $"{book.PageCount.Value} p." : null,
                book.Isbn13 ?? book.Isbn10,
                book.Language
            }.Where(value => !string.IsNullOrWhiteSpace(value));
            var result = string.Join(" · ", values);
            return result.Length == 0 ? T("Common.NotSet") : result;
        }
    }

    public string CollectionValue => Item?.Collection is null
        ? T("Common.NotSet")
        : Item.Collection.Position.HasValue
            ? $"{Item.Collection.Name} #{Item.Collection.Position.Value.ToString(CultureInfo.InvariantCulture)}"
            : Item.Collection.Name;

    public string RelationsValue => Item is null || Item.Relations.Count == 0
        ? T("Common.NotSet")
        : string.Join(", ", Item.Relations.Select(value => value.RelatedItemTitle));

    public string MovieMetadataValue
    {
        get
        {
            var movie = Item?.MovieDetails;
            if (movie is null)
            {
                return T("Common.NotSet");
            }

            var values = new[]
            {
                movie.RuntimeMinutes.HasValue ? string.Format(T("Details.MinutesFormat"), movie.RuntimeMinutes.Value) : null,
                movie.CountryOfOrigin,
                movie.OriginalLanguage,
                movie.Language,
                movie.AgeRating
            }.Where(value => !string.IsNullOrWhiteSpace(value));
            var result = string.Join(" · ", values);
            return result.Length == 0 ? T("Common.NotSet") : result;
        }
    }

    public string ExtendedTypeMetadataValue
    {
        get
        {
            if (Item?.EpisodicDetails is { } episodic)
            {
                return JoinSetValues(episodic.SeasonCount, episodic.EpisodeCount, episodic.EpisodeRuntimeMinutes, episodic.Network, episodic.AiringStatus, episodic.SourceMaterial, episodic.OriginalLanguage);
            }
            if (Item?.GraphicPublicationDetails is { } graphic)
            {
                return JoinSetValues(graphic.VolumeCount, graphic.ChapterOrIssueCount, null, graphic.ReadingDirection, graphic.PublicationStatus, graphic.Imprint, graphic.OriginalLanguage);
            }
            if (Item?.GameDetails is { } game)
            {
                var values = new[] { game.Platform, game.MainStoryHours?.ToString(CultureInfo.InvariantCulture), game.CompletionistHours?.ToString(CultureInfo.InvariantCulture), game.GameMode, game.Engine, game.Region };
                var result = string.Join(" · ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
                return result.Length == 0 ? T("Common.NotSet") : result;
            }
            return T("Common.NotSet");
        }
    }

    public string TypeValue => Item is null ? T("Common.NotSet") : T($"MediaType.{Item.MediaType}");

    public string StatusValue => Item is null ? T("Common.NotSet") : T($"MediaStatus.{Item.Status}");

    public Color MediaTypeColor => Item is null
        ? Color.FromArgb("#9D7FF4")
        : MediaPresentation.GetMediaTypeColor(Item.MediaType);

    public Color StatusForegroundColor => Item is null
        ? Color.FromArgb("#9D7FF4")
        : MediaPresentation.GetStatusForegroundColor(Item.Status);

    public Color StatusBackgroundColor => Item is null
        ? Color.FromArgb("#1A9D7FF4")
        : MediaPresentation.GetStatusBackgroundColor(Item.Status);

    public string ProgressValue => Item is null
        ? T("Common.NotSet")
        : Item.ProgressTotal.HasValue
            ? string.Format(T("Library.ProgressWithTotalFormat"), Item.ProgressCurrent, Item.ProgressTotal.Value)
            : string.Format(T("Library.ProgressFormat"), Item.ProgressCurrent);

    public string RatingValue => Item?.Rating is null
        ? T("Library.NoRating")
        : string.Format(T("Library.RatingFormat"), Item.Rating.Value);

    public string RatingShort => Item?.Rating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public bool HasRating => Item?.Rating.HasValue == true;

    public double ProgressPercent => Item is null
        ? 0d
        : MediaPresentation.GetProgressPercent(Item.ProgressCurrent, Item.ProgressTotal, Item.Status);

    public bool HasProgressBar => Item is not null && MediaPresentation.HasProgressBar(Item.Status);

    public string NotesValue => Item?.Notes ?? T("Common.NotSet");

    public string StartDateValue => FormatDate(Item?.StartDate);

    public string FinishDateValue => FormatDate(Item?.FinishDate);

    public string ReleaseYearValue => Item?.ReleaseYear?.ToString(CultureInfo.InvariantCulture) ?? T("Common.NotSet");

    public string FavoriteValue => Item?.IsFavorite == true ? T("Common.Yes") : T("Common.No");

    public string FavoriteIcon => Item?.IsFavorite == true ? "♥" : "♡";

    public string FavoriteActionText => Item?.IsFavorite == true
        ? T("Details.RemoveFavoriteButton")
        : T("Details.AddFavoriteButton");

    public string HeroMetaValue
    {
        get
        {
            var values = new[] { CreatorValue == T("Common.NotSet") ? null : CreatorValue, ReleaseYearValue == T("Common.NotSet") ? null : ReleaseYearValue };
            return string.Join(" · ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }

    public string DatesValue => $"{StartDateValue} → {FinishDateValue}";

    public string AuditValue => $"{CreatedAtValue} · {UpdatedAtValue}";

    public string CreatedAtValue => FormatDate(Item?.CreatedAt);

    public string UpdatedAtValue => FormatDate(Item?.UpdatedAt);

    public string CoverUrl => HasCoverUrl ? Item?.CoverUrl ?? string.Empty : string.Empty;

    public bool HasCoverUrl => MediaPresentation.HasValidCoverUrl(Item?.CoverUrl);

    public bool HasNoCoverUrl => !HasCoverUrl;

    public string Initial => Item is null || string.IsNullOrWhiteSpace(Item.Title)
        ? "?"
        : Item.Title.Trim()[0].ToString().ToUpperInvariant();

    public async Task LoadAsync(Guid itemId)
    {
        _currentItemId = itemId;
        var userId = await GetCurrentUserIdAsync();
        Item = await _mediaItemService.GetMediaItemAsync(itemId, userId);
        ErrorMessage = Item is null ? T("Details.NotFound") : string.Empty;
        RefreshItemProperties();
    }

    public async Task ReloadAsync()
    {
        if (_currentItemId.HasValue)
        {
            await LoadAsync(_currentItemId.Value);
        }
    }

    protected override void RefreshLocalizedProperties()
    {
        base.RefreshLocalizedProperties();
        RefreshItemProperties();
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        try
        {
            await AppNavigation.CloseAsync();
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, "MediaDetailsViewModel.BackAsync");
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (Item is null)
        {
            return;
        }

        try
        {
            await AppNavigation.OpenEditMediaItemAsync(Item.Id);
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, $"MediaDetailsViewModel.EditAsync id={Item.Id}");
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (Item is null)
        {
            return;
        }

        try
        {
            var userId = await GetCurrentUserIdAsync();
            Item = await _mediaItemService.UpdateMediaItemAsync(ToUpdateRequest(Item, userId, !Item.IsFavorite));
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, $"MediaDetailsViewModel.ToggleFavoriteAsync id={Item.Id}");
        }
    }

    [RelayCommand]
    private async Task RefreshMetadataAsync()
    {
        if (Item?.TmdbId is not { } tmdbId || !CanRefreshOnlineMetadata || IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var candidate = await _mediaMetadataService.GetCandidateAsync(tmdbId, Item.MediaType);
            var metadata = await _mediaMetadataService.GetDetailsAsync(candidate);
            var coverUrl = Item.CoverUrl;
            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                coverUrl = await _mediaMetadataService.DownloadPosterAsync(candidate) ?? coverUrl;
            }

            var userId = await GetCurrentUserIdAsync();
            Item = await _mediaItemService.UpdateMediaItemAsync(
                ToUpdateRequest(Item, userId, Item.IsFavorite, metadata, coverUrl));
        }
        catch (Exception exception)
        {
            ErrorMessage = T("Metadata.Error.RefreshFailed");
            await CrashReporter.ReportAsync(exception, $"MediaDetailsViewModel.RefreshMetadataAsync id={Item.Id}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Item is null)
        {
            return;
        }

        var shouldDelete = await Shell.Current.DisplayAlertAsync(
            DeleteConfirmTitle,
            string.Format(DeleteConfirmMessage, Item.Title),
            DeleteConfirmButtonText,
            DeleteCancelButtonText);

        if (!shouldDelete)
        {
            return;
        }

        var userId = await GetCurrentUserIdAsync();
        await _mediaItemService.DeleteMediaItemAsync(Item.Id, userId);
        try
        {
            await AppNavigation.CloseAsync();
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, $"MediaDetailsViewModel.DeleteAsync navigation id={Item.Id}");
        }
    }

    private void RefreshItemProperties()
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(CanRefreshOnlineMetadata));
        OnPropertyChanged(nameof(RefreshMetadataTooltip));
        OnPropertyChanged(nameof(OriginalTitleValue));
        OnPropertyChanged(nameof(DescriptionValue));
        OnPropertyChanged(nameof(CategoryValue));
        OnPropertyChanged(nameof(TagsValue));
        OnPropertyChanged(nameof(CreatorValue));
        OnPropertyChanged(nameof(CreatorLabel));
        OnPropertyChanged(nameof(PublisherValue));
        OnPropertyChanged(nameof(PublisherLabel));
        OnPropertyChanged(nameof(SerialNumberValue));
        OnPropertyChanged(nameof(CastValue));
        OnPropertyChanged(nameof(ShowCast));
        OnPropertyChanged(nameof(ShowBookDetails));
        OnPropertyChanged(nameof(ShowMovieDetails));
        OnPropertyChanged(nameof(ShowScreenProductionDetails));
        OnPropertyChanged(nameof(ScreenProductionSectionTitle));
        OnPropertyChanged(nameof(ShowEpisodicDetails));
        OnPropertyChanged(nameof(ShowGraphicPublicationDetails));
        OnPropertyChanged(nameof(ShowGameDetails));
        OnPropertyChanged(nameof(AuthorsValue));
        OnPropertyChanged(nameof(TranslatorsValue));
        OnPropertyChanged(nameof(DirectorsValue));
        OnPropertyChanged(nameof(ScreenwritersValue));
        OnPropertyChanged(nameof(ProducersValue));
        OnPropertyChanged(nameof(CinematographersValue));
        OnPropertyChanged(nameof(ComposersValue));
        OnPropertyChanged(nameof(CastingDirectorsValue));
        OnPropertyChanged(nameof(ProductionDesignersValue));
        OnPropertyChanged(nameof(ActorsValue));
        OnPropertyChanged(nameof(MovieStudiosValue));
        OnPropertyChanged(nameof(GenresValue));
        OnPropertyChanged(nameof(BookMetadataValue));
        OnPropertyChanged(nameof(MovieMetadataValue));
        OnPropertyChanged(nameof(ExtendedTypeMetadataValue));
        OnPropertyChanged(nameof(CollectionValue));
        OnPropertyChanged(nameof(RelationsValue));
        OnPropertyChanged(nameof(TypeValue));
        OnPropertyChanged(nameof(StatusValue));
        OnPropertyChanged(nameof(MediaTypeColor));
        OnPropertyChanged(nameof(StatusForegroundColor));
        OnPropertyChanged(nameof(StatusBackgroundColor));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(RatingValue));
        OnPropertyChanged(nameof(ExternalRatingsLabel));
        OnPropertyChanged(nameof(ExternalRatingsValue));
        OnPropertyChanged(nameof(HasExternalRatings));
        OnPropertyChanged(nameof(RatingShort));
        OnPropertyChanged(nameof(HasRating));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(HasProgressBar));
        OnPropertyChanged(nameof(NotesValue));
        OnPropertyChanged(nameof(StartDateValue));
        OnPropertyChanged(nameof(FinishDateValue));
        OnPropertyChanged(nameof(ReleaseYearValue));
        OnPropertyChanged(nameof(FavoriteValue));
        OnPropertyChanged(nameof(FavoriteIcon));
        OnPropertyChanged(nameof(FavoriteActionText));
        OnPropertyChanged(nameof(HeroMetaValue));
        OnPropertyChanged(nameof(DatesValue));
        OnPropertyChanged(nameof(AuditValue));
        OnPropertyChanged(nameof(CreatedAtValue));
        OnPropertyChanged(nameof(UpdatedAtValue));
        OnPropertyChanged(nameof(CoverUrl));
        OnPropertyChanged(nameof(HasCoverUrl));
        OnPropertyChanged(nameof(HasNoCoverUrl));
        OnPropertyChanged(nameof(Initial));
    }

    private string FormatDate(DateTime? value)
    {
        return value?.ToString("d", CultureInfo.CurrentCulture) ?? T("Common.NotSet");
    }

    private string JoinContributors(ContributionRole role)
    {
        var values = Item?.Contributions.Where(value => value.Role == role).OrderBy(value => value.SortOrder).Select(value => value.PersonName).ToList();
        return values is null || values.Count == 0 ? T("Common.NotSet") : string.Join(", ", values);
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        return await _authService.GetCurrentUserIdAsync() ?? "local-user";
    }

    private static UpdateMediaItemRequest ToUpdateRequest(
        MediaItemDto item,
        string userId,
        bool isFavorite,
        MediaMetadataDetails? metadata = null,
        string? refreshedCoverUrl = null)
    {
        return new UpdateMediaItemRequest
        {
            Id = item.Id,
            UserId = userId,
            Title = item.Title,
            OriginalTitle = metadata?.Candidate.OriginalTitle ?? item.OriginalTitle,
            Description = metadata?.Description ?? item.Description,
            Category = item.Category,
            MediaCategoryId = item.MediaCategoryId,
            Tags = item.Tags,
            TagNames = item.TagNames,
            Genres = metadata?.Genres ?? item.Genres,
            Contributions = metadata is null
                ? item.Contributions.Select(value => new PersonCreditInput
            {
                PersonId = value.PersonId,
                CreditRoleId = value.CreditRoleId,
                Name = value.PersonName,
                Role = value.Role,
                SortOrder = value.SortOrder,
                Details = value.Details,
                CreditedAs = value.CreditedAs
            }).ToList()
                : metadata.People.Select((value, index) => new PersonCreditInput
                {
                    Name = value.Name,
                    Role = value.Role,
                    SortOrder = index,
                    Details = value.Details
                }).ToList(),
            StudioCredits = metadata is null
                ? item.StudioCredits.Select(value => new StudioCreditInput
            {
                StudioId = value.StudioId,
                Name = value.StudioName,
                Role = value.Role,
                SortOrder = value.SortOrder
            }).ToList()
                : metadata.Studios.Select((value, index) => new StudioCreditInput
                {
                    Name = value.Name,
                    Role = value.Role,
                    SortOrder = index
                }).ToList(),
            BookDetails = item.BookDetails is null ? null : new BookDetailsInput
            {
                Subtitle = item.BookDetails.Subtitle,
                Publisher = item.BookDetails.Publisher,
                Edition = item.BookDetails.Edition,
                EditionNumber = item.BookDetails.EditionNumber,
                EditionYear = item.BookDetails.EditionYear,
                OriginalPublicationYear = item.BookDetails.OriginalPublicationYear,
                TranslationYear = item.BookDetails.TranslationYear,
                OriginalLanguage = item.BookDetails.OriginalLanguage,
                Language = item.BookDetails.Language,
                PageCount = item.BookDetails.PageCount,
                Isbn10 = item.BookDetails.Isbn10,
                Isbn13 = item.BookDetails.Isbn13,
                Format = item.BookDetails.Format,
                Binding = item.BookDetails.Binding,
                CountryOfOrigin = item.BookDetails.CountryOfOrigin,
                AgeRating = item.BookDetails.AgeRating
            },
            MovieDetails = metadata is not null && item.MediaType is MediaType.Movie or MediaType.Cartoon
                ? new MovieDetailsInput
                {
                    RuntimeMinutes = metadata.RuntimeMinutes,
                    OriginalLanguage = metadata.OriginalLanguage,
                    CountryOfOrigin = metadata.Country,
                    AgeRating = metadata.AgeRating
                }
                : item.MovieDetails is null ? null : new MovieDetailsInput
            {
                RuntimeMinutes = item.MovieDetails.RuntimeMinutes,
                OriginalLanguage = item.MovieDetails.OriginalLanguage,
                Language = item.MovieDetails.Language,
                CountryOfOrigin = item.MovieDetails.CountryOfOrigin,
                AgeRating = item.MovieDetails.AgeRating
            },
            EpisodicDetails = metadata is not null && item.MediaType is MediaType.Series or MediaType.AnimatedSeries or MediaType.Anime
                ? new EpisodicDetailsInput
                {
                    SeasonCount = metadata.SeasonCount,
                    EpisodeCount = metadata.EpisodeCount,
                    EpisodeRuntimeMinutes = metadata.RuntimeMinutes,
                    Network = metadata.Network,
                    AiringStatus = metadata.AiringStatus,
                    SourceMaterial = item.EpisodicDetails?.SourceMaterial,
                    OriginalLanguage = metadata.OriginalLanguage
                }
                : item.EpisodicDetails,
            GraphicPublicationDetails = item.GraphicPublicationDetails,
            GameDetails = item.GameDetails,
            Collection = item.Collection is null ? null : new CollectionMembershipInput
            {
                CollectionId = item.Collection.CollectionId,
                Name = item.Collection.Name,
                Kind = item.Collection.Kind,
                Position = item.Collection.Position
            },
            Relations = item.Relations.Where(value => value.IsOutgoing).Select(value => new MediaRelationInput
            {
                RelatedItemId = value.RelatedItemId,
                Kind = value.Kind,
                Notes = value.Notes
            }).ToList(),
            Creator = item.Creator,
            Publisher = item.Publisher,
            SerialNumber = item.SerialNumber,
            Cast = item.Cast,
            MediaType = item.MediaType,
            Status = item.Status,
            Rating = item.Rating,
            ProgressCurrent = item.ProgressCurrent,
            ProgressTotal = item.ProgressTotal,
            StartDate = item.StartDate,
            FinishDate = item.FinishDate,
            ReleaseYear = metadata?.Candidate.Year ?? item.ReleaseYear,
            CoverUrl = refreshedCoverUrl ?? item.CoverUrl,
            TmdbId = item.TmdbId,
            ImdbId = metadata?.ImdbId ?? item.ImdbId,
            KinopoiskId = metadata?.KinopoiskId ?? item.KinopoiskId,
            TmdbRating = metadata?.Candidate.TmdbRating ?? item.TmdbRating,
            TmdbVoteCount = metadata?.Candidate.TmdbVoteCount ?? item.TmdbVoteCount,
            ImdbRating = metadata?.ImdbRating ?? item.ImdbRating,
            ImdbVoteCount = metadata?.ImdbVoteCount ?? item.ImdbVoteCount,
            KinopoiskRating = metadata?.KinopoiskRating ?? item.KinopoiskRating,
            KinopoiskVoteCount = metadata?.KinopoiskVoteCount ?? item.KinopoiskVoteCount,
            ExternalRatingsUpdatedAt = metadata?.RatingsUpdatedAtUtc ?? item.ExternalRatingsUpdatedAt,
            Notes = item.Notes,
            IsFavorite = isFavorite
        };
    }

    private string JoinSetValues(int? first, int? second, int? third, params string?[] textValues)
    {
        var values = new List<string>();
        if (first.HasValue) values.Add(first.Value.ToString(CultureInfo.InvariantCulture));
        if (second.HasValue) values.Add(second.Value.ToString(CultureInfo.InvariantCulture));
        if (third.HasValue) values.Add(third.Value.ToString(CultureInfo.InvariantCulture));
        values.AddRange(textValues.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!));
        return values.Count == 0 ? T("Common.NotSet") : string.Join(" · ", values);
    }

    private static void AddExternalRating(List<string> values, string source, decimal? rating, int? voteCount)
    {
        if (!rating.HasValue)
        {
            return;
        }

        var votes = voteCount.HasValue ? $" ({voteCount.Value:N0})" : string.Empty;
        values.Add($"{source} {rating.Value:0.0}{votes}");
    }
}
