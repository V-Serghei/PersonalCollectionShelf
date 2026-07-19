using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record ContributorChipViewModel(Guid PersonId, string Name, ContributionRole Role);

public sealed record RatingStarViewModel(int Value, string Glyph);

public sealed record RelatedItemChipViewModel(Guid ItemId, string Title, MediaRelationKind Kind);

public partial class EditMediaItemViewModel
{
    private ContributionRole _pendingContributionRole = ContributionRole.Author;
    private int _personSearchVersion;
    private int _relatedSearchVersion;
    private bool _isUpdatingRating;
    private string _newGenreText = string.Empty;
    private bool _isPersonPickerOpen;
    private string _personSearchText = string.Empty;
    private string _subtitle = string.Empty;
    private string _edition = string.Empty;
    private string _editionNumber = string.Empty;
    private string _editionYear = string.Empty;
    private string _originalPublicationYear = string.Empty;
    private string _translationYear = string.Empty;
    private string _originalLanguage = string.Empty;
    private string _bookLanguage = string.Empty;
    private string _pageCount = string.Empty;
    private string _isbn10 = string.Empty;
    private string _isbn13 = string.Empty;
    private string _binding = string.Empty;
    private string _countryOfOrigin = string.Empty;
    private string _ageRating = string.Empty;
    private LocalizedOption<BookFormat>? _selectedBookFormat;
    private string _collectionName = string.Empty;
    private string _collectionPosition = string.Empty;
    private LocalizedOption<MediaCollectionKind>? _selectedCollectionKind;
    private string _relatedSearchText = string.Empty;
    private LocalizedOption<MediaRelationKind>? _selectedRelationKind;

    public ObservableCollection<ContributorChipViewModel> Authors { get; } = [];
    public ObservableCollection<ContributorChipViewModel> Translators { get; } = [];
    public ObservableCollection<ContributorChipViewModel> Illustrators { get; } = [];
    public ObservableCollection<ContributorChipViewModel> Editors { get; } = [];
    public ObservableCollection<PersonDto> PersonSearchResults { get; } = [];
    public ObservableCollection<string> GenreChips { get; } = [];
    public ObservableCollection<RatingStarViewModel> RatingStars { get; } = [];
    public ObservableCollection<LocalizedOption<BookFormat>> BookFormats { get; } = [];
    public ObservableCollection<LocalizedOption<MediaCollectionKind>> CollectionKinds { get; } = [];
    public ObservableCollection<LocalizedOption<MediaRelationKind>> RelationKinds { get; } = [];
    public ObservableCollection<MediaItemDto> RelatedSearchResults { get; } = [];
    public ObservableCollection<RelatedItemChipViewModel> RelatedItems { get; } = [];

    public bool ShowBookFields => SelectedMediaType?.Value == MediaType.Book;

    public string NewGenreText { get => _newGenreText; set => SetProperty(ref _newGenreText, value); }
    public bool IsPersonPickerOpen { get => _isPersonPickerOpen; set => SetProperty(ref _isPersonPickerOpen, value); }
    public string PersonPickerTitle => T($"Edit.Contributor.{_pendingContributionRole}");
    public string PersonSearchText
    {
        get => _personSearchText;
        set
        {
            if (SetProperty(ref _personSearchText, value))
            {
                _ = RefreshPersonSearchAsync(++_personSearchVersion);
            }
        }
    }

    public string Subtitle { get => _subtitle; set => SetProperty(ref _subtitle, value); }
    public string Edition { get => _edition; set => SetProperty(ref _edition, value); }
    public string EditionNumber { get => _editionNumber; set => SetProperty(ref _editionNumber, value); }
    public string EditionYear { get => _editionYear; set => SetProperty(ref _editionYear, value); }
    public string OriginalPublicationYear { get => _originalPublicationYear; set => SetProperty(ref _originalPublicationYear, value); }
    public string TranslationYear { get => _translationYear; set => SetProperty(ref _translationYear, value); }
    public string OriginalLanguage { get => _originalLanguage; set => SetProperty(ref _originalLanguage, value); }
    public string BookLanguage { get => _bookLanguage; set => SetProperty(ref _bookLanguage, value); }
    public string PageCount { get => _pageCount; set => SetProperty(ref _pageCount, value); }
    public string Isbn10 { get => _isbn10; set => SetProperty(ref _isbn10, value); }
    public string Isbn13 { get => _isbn13; set => SetProperty(ref _isbn13, value); }
    public string Binding { get => _binding; set => SetProperty(ref _binding, value); }
    public string CountryOfOrigin { get => _countryOfOrigin; set => SetProperty(ref _countryOfOrigin, value); }
    public string AgeRating { get => _ageRating; set => SetProperty(ref _ageRating, value); }
    public LocalizedOption<BookFormat>? SelectedBookFormat { get => _selectedBookFormat; set => SetProperty(ref _selectedBookFormat, value); }
    public string CollectionName { get => _collectionName; set => SetProperty(ref _collectionName, value); }
    public string CollectionPosition { get => _collectionPosition; set => SetProperty(ref _collectionPosition, value); }
    public LocalizedOption<MediaCollectionKind>? SelectedCollectionKind { get => _selectedCollectionKind; set => SetProperty(ref _selectedCollectionKind, value); }
    public LocalizedOption<MediaRelationKind>? SelectedRelationKind { get => _selectedRelationKind; set => SetProperty(ref _selectedRelationKind, value); }
    public string RelatedSearchText
    {
        get => _relatedSearchText;
        set
        {
            if (SetProperty(ref _relatedSearchText, value))
            {
                _ = RefreshRelatedItemsAsync(++_relatedSearchVersion);
            }
        }
    }

    public string AuthorsLabel => T("Edit.Label.Authors");
    public string TranslatorsLabel => T("Edit.Label.Translators");
    public string IllustratorsLabel => T("Edit.Label.Illustrators");
    public string EditorsLabel => T("Edit.Label.Editors");
    public string AddPersonText => T("Edit.Action.AddPerson");
    public string CreatePersonText => T("Edit.Action.CreatePerson");
    public string SearchPersonPlaceholder => T("Edit.Placeholder.SearchPerson");
    public string GenresLabel => T("Edit.Label.Genres");
    public string GenresPlaceholder => T("Edit.Placeholder.Genres");
    public string BookDetailsSectionTitle => T("Edit.Section.BookDetails");
    public string PublicationSectionTitle => T("Edit.Section.Publication");
    public string CollectionsSectionTitle => T("Edit.Section.Collections");
    public string RelationsSectionTitle => T("Edit.Section.Relations");
    public string SubtitleLabel => T("Edit.Label.Subtitle");
    public string EditionLabel => T("Edit.Label.Edition");
    public string EditionNumberLabel => T("Edit.Label.EditionNumber");
    public string BookFormatLabel => T("Edit.Label.BookFormat");
    public string OriginalPublicationYearLabel => T("Edit.Label.OriginalPublicationYear");
    public string EditionYearLabel => T("Edit.Label.EditionYear");
    public string TranslationYearLabel => T("Edit.Label.TranslationYear");
    public string OriginalLanguageLabel => T("Edit.Label.OriginalLanguage");
    public string BookLanguageLabel => T("Edit.Label.BookLanguage");
    public string PageCountLabel => T("Edit.Label.PageCount");
    public string Isbn10Label => T("Edit.Label.Isbn10");
    public string Isbn13Label => T("Edit.Label.Isbn13");
    public string BindingLabel => T("Edit.Label.Binding");
    public string CountryOfOriginLabel => T("Edit.Label.CountryOfOrigin");
    public string AgeRatingLabel => T("Edit.Label.AgeRating");
    public string CollectionNamePlaceholder => T("Edit.Placeholder.CollectionName");
    public string RelatedSearchPlaceholder => T("Edit.Placeholder.RelatedItem");

    private void InitializeBookFields()
    {
        ReloadBookOptions();
        UpdateRatingStars(null);
    }

    private void ReloadBookOptions()
    {
        var selectedFormat = SelectedBookFormat?.Value;
        var selectedCollectionKind = SelectedCollectionKind?.Value ?? MediaCollectionKind.Series;
        var selectedRelationKind = SelectedRelationKind?.Value ?? MediaRelationKind.Other;

        BookFormats.Clear();
        foreach (var value in Enum.GetValues<BookFormat>())
        {
            BookFormats.Add(new LocalizedOption<BookFormat>(value, T($"BookFormat.{value}")));
        }

        CollectionKinds.Clear();
        foreach (var value in Enum.GetValues<MediaCollectionKind>())
        {
            CollectionKinds.Add(new LocalizedOption<MediaCollectionKind>(value, T($"CollectionKind.{value}")));
        }

        RelationKinds.Clear();
        foreach (var value in Enum.GetValues<MediaRelationKind>())
        {
            RelationKinds.Add(new LocalizedOption<MediaRelationKind>(value, T($"RelationKind.{value}")));
        }

        SelectedBookFormat = selectedFormat.HasValue ? BookFormats.First(option => option.Value == selectedFormat.Value) : null;
        SelectedCollectionKind = CollectionKinds.First(option => option.Value == selectedCollectionKind);
        SelectedRelationKind = RelationKinds.First(option => option.Value == selectedRelationKind);
    }

    private void OnRatingTextChanged()
    {
        if (_isUpdatingRating)
        {
            return;
        }

        if (decimal.TryParse(Rating.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 10)
        {
            UpdateRatingStars(value);
        }
        else
        {
            UpdateRatingStars(null);
        }
    }

    private void UpdateRatingStars(decimal? rating)
    {
        RatingStars.Clear();
        var rounded = rating.HasValue ? (int)Math.Round(rating.Value, MidpointRounding.AwayFromZero) : 0;
        for (var value = 1; value <= 10; value++)
        {
            RatingStars.Add(new RatingStarViewModel(value, value <= rounded ? "★" : "☆"));
        }
    }

    [RelayCommand]
    private void SetRating(int value)
    {
        _isUpdatingRating = true;
        Rating = Math.Clamp(value, 0, 10).ToString(CultureInfo.InvariantCulture);
        _isUpdatingRating = false;
        UpdateRatingStars(value);
    }

    [RelayCommand]
    private void ClearRating()
    {
        Rating = string.Empty;
        UpdateRatingStars(null);
    }

    [RelayCommand]
    private void AddGenre()
    {
        var value = NewGenreText.Trim();
        NewGenreText = string.Empty;
        if (value.Length > 0 && !GenreChips.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
        {
            GenreChips.Add(value);
        }
    }

    [RelayCommand]
    private void RemoveGenre(string? value)
    {
        if (value is not null)
        {
            GenreChips.Remove(value);
        }
    }

    [RelayCommand]
    private async Task OpenPersonPickerAsync(string? role)
    {
        _pendingContributionRole = Enum.TryParse<ContributionRole>(role, out var parsed) ? parsed : ContributionRole.Author;
        OnPropertyChanged(nameof(PersonPickerTitle));
        PersonSearchText = string.Empty;
        IsPersonPickerOpen = true;
        await RefreshPersonSearchAsync(++_personSearchVersion);
    }

    [RelayCommand]
    private void ClosePersonPicker()
    {
        IsPersonPickerOpen = false;
    }

    [RelayCommand]
    private void SelectPerson(PersonDto? person)
    {
        if (person is null)
        {
            return;
        }

        AddContributor(new ContributorChipViewModel(person.Id, person.Name, _pendingContributionRole));
        IsPersonPickerOpen = false;
    }

    [RelayCommand]
    private async Task CreatePersonAsync()
    {
        var name = PersonSearchText.Trim();
        if (name.Length == 0)
        {
            return;
        }

        var userId = await GetCurrentUserIdAsync();
        var person = await _personService.CreateAsync(userId, name);
        SelectPerson(person);
    }

    [RelayCommand]
    private void RemoveContributor(ContributorChipViewModel? contributor)
    {
        if (contributor is null)
        {
            return;
        }

        GetContributorCollection(contributor.Role).Remove(contributor);
    }

    [RelayCommand]
    private void MoveContributorUp(ContributorChipViewModel? contributor)
    {
        MoveContributor(contributor, -1);
    }

    [RelayCommand]
    private void MoveContributorDown(ContributorChipViewModel? contributor)
    {
        MoveContributor(contributor, 1);
    }

    [RelayCommand]
    private void AddRelatedItem(MediaItemDto? item)
    {
        if (item is null || item.Id == MediaItemId || RelatedItems.Any(value => value.ItemId == item.Id))
        {
            return;
        }

        RelatedItems.Add(new RelatedItemChipViewModel(item.Id, item.Title, SelectedRelationKind?.Value ?? MediaRelationKind.Other));
        RelatedSearchText = string.Empty;
        RelatedSearchResults.Clear();
    }

    [RelayCommand]
    private void RemoveRelatedItem(RelatedItemChipViewModel? item)
    {
        if (item is not null)
        {
            RelatedItems.Remove(item);
        }
    }

    private async Task RefreshPersonSearchAsync(int version)
    {
        await Task.Delay(180);
        if (version != _personSearchVersion)
        {
            return;
        }

        var userId = await GetCurrentUserIdAsync();
        var results = await _personService.SearchAsync(userId, PersonSearchText, 20);
        if (version != _personSearchVersion)
        {
            return;
        }

        PersonSearchResults.Clear();
        foreach (var person in results)
        {
            PersonSearchResults.Add(person);
        }
    }

    private async Task RefreshRelatedItemsAsync(int version)
    {
        await Task.Delay(180);
        if (version != _relatedSearchVersion || string.IsNullOrWhiteSpace(RelatedSearchText))
        {
            RelatedSearchResults.Clear();
            return;
        }

        var userId = await GetCurrentUserIdAsync();
        var results = await _mediaItemService.SearchMediaItemsAsync(new MediaItemSearchCriteria
        {
            UserId = userId,
            SearchTerm = RelatedSearchText
        });
        if (version != _relatedSearchVersion)
        {
            return;
        }

        RelatedSearchResults.Clear();
        foreach (var item in results.Where(item => item.Id != MediaItemId).Take(10))
        {
            RelatedSearchResults.Add(item);
        }
    }

    private void AddContributor(ContributorChipViewModel value)
    {
        var collection = GetContributorCollection(value.Role);
        if (!collection.Any(existing => existing.PersonId == value.PersonId))
        {
            collection.Add(value);
        }
    }

    private ObservableCollection<ContributorChipViewModel> GetContributorCollection(ContributionRole role) => role switch
    {
        ContributionRole.Translator => Translators,
        ContributionRole.Illustrator => Illustrators,
        ContributionRole.Editor => Editors,
        _ => Authors
    };

    private void MoveContributor(ContributorChipViewModel? contributor, int offset)
    {
        if (contributor is null)
        {
            return;
        }

        var collection = GetContributorCollection(contributor.Role);
        var currentIndex = collection.IndexOf(contributor);
        var targetIndex = currentIndex + offset;
        if (currentIndex >= 0 && targetIndex >= 0 && targetIndex < collection.Count)
        {
            collection.Move(currentIndex, targetIndex);
        }
    }

    private IReadOnlyList<PersonCreditInput> BuildContributions()
    {
        return Authors.Select((value, index) => ToInput(value, index))
            .Concat(Translators.Select((value, index) => ToInput(value, index)))
            .Concat(Illustrators.Select((value, index) => ToInput(value, index)))
            .Concat(Editors.Select((value, index) => ToInput(value, index)))
            .ToList();
    }

    private static PersonCreditInput ToInput(ContributorChipViewModel value, int index) => new()
    {
        PersonId = value.PersonId,
        Name = value.Name,
        Role = value.Role,
        SortOrder = index
    };

    private IReadOnlyList<MediaRelationInput> BuildRelations() => RelatedItems.Select(value => new MediaRelationInput
    {
        RelatedItemId = value.ItemId,
        Kind = value.Kind
    }).ToList();

    private bool TryBuildBookInputs(out BookDetailsInput? details, out CollectionMembershipInput? collection)
    {
        details = null;
        collection = null;
        if (!ShowBookFields)
        {
            return true;
        }

        if (!TryParseOptionalInt(EditionNumber, out var editionNumber) ||
            !TryParseOptionalInt(EditionYear, out var editionYear) ||
            !TryParseOptionalInt(OriginalPublicationYear, out var originalPublicationYear) ||
            !TryParseOptionalInt(TranslationYear, out var translationYear) ||
            !TryParseOptionalInt(PageCount, out var pageCount) ||
            !TryParseOptionalDouble(CollectionPosition, out var collectionPosition))
        {
            return false;
        }

        details = new BookDetailsInput
        {
            Subtitle = Subtitle,
            Publisher = Publisher,
            Edition = Edition,
            EditionNumber = editionNumber,
            EditionYear = editionYear,
            OriginalPublicationYear = originalPublicationYear,
            TranslationYear = translationYear,
            OriginalLanguage = OriginalLanguage,
            Language = BookLanguage,
            PageCount = pageCount,
            Isbn10 = Isbn10,
            Isbn13 = Isbn13,
            Format = SelectedBookFormat?.Value,
            Binding = Binding,
            CountryOfOrigin = CountryOfOrigin,
            AgeRating = AgeRating
        };

        if (!string.IsNullOrWhiteSpace(CollectionName))
        {
            collection = new CollectionMembershipInput
            {
                Name = CollectionName,
                Kind = SelectedCollectionKind?.Value ?? MediaCollectionKind.Series,
                Position = collectionPosition
            };
        }

        return true;
    }

    private void LoadBookFields(MediaItemDto item)
    {
        Authors.Clear();
        Translators.Clear();
        Illustrators.Clear();
        Editors.Clear();
        foreach (var contribution in item.Contributions)
        {
            if (contribution.Role is ContributionRole.Author or ContributionRole.Translator or ContributionRole.Illustrator or ContributionRole.Editor)
            {
                AddContributor(new ContributorChipViewModel(contribution.PersonId, contribution.PersonName, contribution.Role));
            }
        }

        GenreChips.Clear();
        foreach (var genre in item.Genres)
        {
            GenreChips.Add(genre);
        }

        var book = item.BookDetails;
        Subtitle = book?.Subtitle ?? string.Empty;
        Edition = book?.Edition ?? string.Empty;
        EditionNumber = FormatNumber(book?.EditionNumber);
        EditionYear = FormatNumber(book?.EditionYear);
        OriginalPublicationYear = FormatNumber(book?.OriginalPublicationYear);
        TranslationYear = FormatNumber(book?.TranslationYear);
        OriginalLanguage = book?.OriginalLanguage ?? string.Empty;
        BookLanguage = book?.Language ?? string.Empty;
        PageCount = FormatNumber(book?.PageCount);
        Isbn10 = book?.Isbn10 ?? string.Empty;
        Isbn13 = book?.Isbn13 ?? string.Empty;
        Binding = book?.Binding ?? string.Empty;
        CountryOfOrigin = book?.CountryOfOrigin ?? string.Empty;
        AgeRating = book?.AgeRating ?? string.Empty;
        SelectedBookFormat = book?.Format is { } format ? BookFormats.First(option => option.Value == format) : null;
        CollectionName = item.Collection?.Name ?? string.Empty;
        CollectionPosition = item.Collection?.Position?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        SelectedCollectionKind = CollectionKinds.First(option => option.Value == (item.Collection?.Kind ?? MediaCollectionKind.Series));

        RelatedItems.Clear();
        foreach (var relation in item.Relations.Where(value => value.IsOutgoing))
        {
            RelatedItems.Add(new RelatedItemChipViewModel(relation.RelatedItemId, relation.RelatedItemTitle, relation.Kind));
        }
    }

    private void ResetBookFields()
    {
        Authors.Clear();
        Translators.Clear();
        Illustrators.Clear();
        Editors.Clear();
        PersonSearchResults.Clear();
        GenreChips.Clear();
        RelatedItems.Clear();
        RelatedSearchResults.Clear();
        NewGenreText = string.Empty;
        Subtitle = Edition = EditionNumber = EditionYear = OriginalPublicationYear = TranslationYear = string.Empty;
        OriginalLanguage = BookLanguage = PageCount = Isbn10 = Isbn13 = Binding = CountryOfOrigin = AgeRating = string.Empty;
        CollectionName = CollectionPosition = RelatedSearchText = string.Empty;
        SelectedBookFormat = null;
        SelectedCollectionKind = CollectionKinds.First(option => option.Value == MediaCollectionKind.Series);
        SelectedRelationKind = RelationKinds.First(option => option.Value == MediaRelationKind.Other);
        IsPersonPickerOpen = false;
    }

    private static string FormatNumber(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool TryParseOptionalDouble(string value, out double? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (double.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }
}
