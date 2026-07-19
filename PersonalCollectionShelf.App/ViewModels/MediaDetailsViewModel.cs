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
    private Guid? _currentItemId;

    public MediaDetailsViewModel(
        IMediaItemService mediaItemService,
        IAuthService authService,
        ILocalizationService localizationService)
        : base(localizationService)
    {
        _mediaItemService = mediaItemService;
        _authService = authService;
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

    public string PageTitle => Item?.Title ?? T("Details.Title");

    public string TypeLabel => T("Details.TypeLabel");

    public string StatusLabel => T("Details.StatusLabel");

    public string ProgressLabel => T("Details.ProgressLabel");

    public string RatingLabel => T("Details.RatingLabel");

    public string NotesLabel => T("Details.NotesLabel");

    public string OriginalTitleLabel => T("Details.OriginalTitleLabel");

    public string DescriptionLabel => T("Details.DescriptionLabel");

    public string CategoryLabel => T("Details.CategoryLabel");

    public string TagsLabel => T("Details.TagsLabel");

    public string CreatorLabel => T("Details.CreatorLabel");

    public string PublisherLabel => T("Details.PublisherLabel");

    public string SerialNumberLabel => T("Details.SerialNumberLabel");

    public string CastLabel => T("Details.CastLabel");

    public bool ShowCast => Item is not null && Item.MediaType is MediaType.Movie or MediaType.Series;

    public bool ShowBookDetails => Item?.MediaType == MediaType.Book;

    public string BookDetailsSectionTitle => T("Edit.Section.BookDetails");

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
    private async Task EditAsync()
    {
        if (Item is null)
        {
            return;
        }

        try
        {
            await Shell.Current.GoToAsync($"{nameof(EditMediaItemPage)}?id={Item.Id}");
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
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception exception)
        {
            await CrashReporter.ReportAsync(exception, $"MediaDetailsViewModel.DeleteAsync navigation id={Item.Id}");
        }
    }

    private void RefreshItemProperties()
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(OriginalTitleValue));
        OnPropertyChanged(nameof(DescriptionValue));
        OnPropertyChanged(nameof(CategoryValue));
        OnPropertyChanged(nameof(TagsValue));
        OnPropertyChanged(nameof(CreatorValue));
        OnPropertyChanged(nameof(PublisherValue));
        OnPropertyChanged(nameof(SerialNumberValue));
        OnPropertyChanged(nameof(CastValue));
        OnPropertyChanged(nameof(ShowCast));
        OnPropertyChanged(nameof(ShowBookDetails));
        OnPropertyChanged(nameof(AuthorsValue));
        OnPropertyChanged(nameof(TranslatorsValue));
        OnPropertyChanged(nameof(GenresValue));
        OnPropertyChanged(nameof(BookMetadataValue));
        OnPropertyChanged(nameof(CollectionValue));
        OnPropertyChanged(nameof(RelationsValue));
        OnPropertyChanged(nameof(TypeValue));
        OnPropertyChanged(nameof(StatusValue));
        OnPropertyChanged(nameof(MediaTypeColor));
        OnPropertyChanged(nameof(StatusForegroundColor));
        OnPropertyChanged(nameof(StatusBackgroundColor));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(RatingValue));
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

    private static UpdateMediaItemRequest ToUpdateRequest(MediaItemDto item, string userId, bool isFavorite)
    {
        return new UpdateMediaItemRequest
        {
            Id = item.Id,
            UserId = userId,
            Title = item.Title,
            OriginalTitle = item.OriginalTitle,
            Description = item.Description,
            Category = item.Category,
            MediaCategoryId = item.MediaCategoryId,
            Tags = item.Tags,
            TagNames = item.TagNames,
            Genres = item.Genres,
            Contributions = item.Contributions.Select(value => new PersonCreditInput
            {
                PersonId = value.PersonId,
                CreditRoleId = value.CreditRoleId,
                Name = value.PersonName,
                Role = value.Role,
                SortOrder = value.SortOrder,
                Details = value.Details,
                CreditedAs = value.CreditedAs
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
            ReleaseYear = item.ReleaseYear,
            CoverUrl = item.CoverUrl,
            Notes = item.Notes,
            IsFavorite = isFavorite
        };
    }
}
