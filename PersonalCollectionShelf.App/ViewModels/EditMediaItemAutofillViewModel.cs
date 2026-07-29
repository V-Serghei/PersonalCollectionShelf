using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class EditMediaItemViewModel
{
    private bool _isMetadataPickerOpen;
    private bool _isMetadataSearching;
    private string _metadataStatusMessage = string.Empty;

    public ObservableCollection<MediaMetadataCandidate> MetadataCandidates { get; } = [];

    public int? TmdbId { get; private set; }
    public string? ImdbId { get; private set; }
    public int? KinopoiskId { get; private set; }
    public decimal? TmdbRating { get; private set; }
    public int? TmdbVoteCount { get; private set; }
    public decimal? ImdbRating { get; private set; }
    public int? ImdbVoteCount { get; private set; }
    public decimal? KinopoiskRating { get; private set; }
    public int? KinopoiskVoteCount { get; private set; }
    public DateTime? ExternalRatingsUpdatedAt { get; private set; }

    public bool IsAutofillSupported => SelectedMediaType?.Value is MediaType.Movie or MediaType.Cartoon or
        MediaType.Series or MediaType.AnimatedSeries or MediaType.Anime;

    public bool IsMetadataPickerOpen
    {
        get => _isMetadataPickerOpen;
        set => SetProperty(ref _isMetadataPickerOpen, value);
    }

    public bool IsMetadataSearching
    {
        get => _isMetadataSearching;
        set
        {
            if (SetProperty(ref _isMetadataSearching, value))
            {
                OnPropertyChanged(nameof(IsNotMetadataSearching));
            }
        }
    }

    public bool IsNotMetadataSearching => !IsMetadataSearching;

    public string MetadataStatusMessage
    {
        get => _metadataStatusMessage;
        set => SetProperty(ref _metadataStatusMessage, value);
    }

    public bool HasExternalRatings => ImdbRating.HasValue || KinopoiskRating.HasValue;

    public string ExternalRatingsSummary
    {
        get
        {
            var values = new List<string>();
            if (ImdbRating.HasValue) values.Add($"IMDb {ImdbRating:0.0} ({ImdbVoteCount ?? 0:N0})");
            if (KinopoiskRating.HasValue) values.Add($"{T("Metadata.Kinopoisk")} {KinopoiskRating:0.0} ({KinopoiskVoteCount ?? 0:N0})");
            return string.Join("  ·  ", values);
        }
    }

    public string AutofillButtonText => T("Metadata.Action.Search");
    public string MetadataPickerTitle => T("Metadata.Title.SearchResults");
    public string MetadataPickerHint => T("Metadata.Hint.Choose");

    public string TmdbAttributionText => T("Metadata.TmdbAttribution");
    public string MetadataCloseText => T("Common.Cancel");
    public string ExternalRatingsLabel => T("Metadata.Label.ExternalRatings");

    [RelayCommand]
    private async Task SearchMetadataAsync()
    {
        if (!IsAutofillSupported)
        {
            ErrorMessage = T("Metadata.Error.UnsupportedType");
            return;
        }

        if (string.IsNullOrWhiteSpace(ItemTitle))
        {
            ErrorMessage = T("Metadata.Error.TitleRequired");
            return;
        }

        if (!_mediaMetadataService.IsConfigured)
        {
            ErrorMessage = T("Metadata.Error.NotConfigured");
            return;
        }

        IsMetadataPickerOpen = true;
        IsMetadataSearching = true;
        MetadataStatusMessage = T("Metadata.Status.Searching");
        MetadataCandidates.Clear();
        ErrorMessage = string.Empty;
        try
        {
            var results = await _mediaMetadataService.SearchAsync(
                ItemTitle,
                SelectedMediaType?.Value ?? MediaType.Movie);
            foreach (var result in results)
            {
                MetadataCandidates.Add(result);
            }

            MetadataStatusMessage = results.Count == 0 ? T("Metadata.Status.NoResults") : string.Empty;
        }
        catch
        {
            MetadataStatusMessage = T("Metadata.Error.SearchFailed");
        }
        finally
        {
            IsMetadataSearching = false;
        }
    }

    [RelayCommand]
    private async Task SelectMetadataCandidateAsync(MediaMetadataCandidate? candidate)
    {
        if (candidate is null || IsMetadataSearching)
        {
            return;
        }

        IsMetadataSearching = true;
        MetadataStatusMessage = T("Metadata.Status.Loading");
        try
        {
            var details = await _mediaMetadataService.GetDetailsAsync(candidate);
            ApplyMetadata(details);
            CoverUrl = await _mediaMetadataService.DownloadPosterAsync(candidate) ?? CoverUrl;
            IsMetadataPickerOpen = false;
            ErrorMessage = string.Empty;
        }
        catch
        {
            MetadataStatusMessage = T("Metadata.Error.LoadFailed");
        }
        finally
        {
            IsMetadataSearching = false;
        }
    }

    [RelayCommand]
    private void CloseMetadataPicker()
    {
        IsMetadataPickerOpen = false;
        MetadataStatusMessage = string.Empty;
    }

    private void ApplyMetadata(MediaMetadataDetails details)
    {
        var candidate = details.Candidate;
        ItemTitle = LocalizationService.CurrentLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
            ? candidate.Title
            : candidate.EnglishTitle;
        if (string.IsNullOrWhiteSpace(ItemTitle)) ItemTitle = candidate.OriginalTitle;
        OriginalTitle = candidate.OriginalTitle;
        Description = details.Description ?? Description;
        ReleaseYear = candidate.Year?.ToString() ?? ReleaseYear;

        GenreChips.Clear();
        foreach (var genre in details.Genres) GenreChips.Add(genre);

        Directors.Clear();
        Screenwriters.Clear();
        Producers.Clear();
        Cinematographers.Clear();
        Composers.Clear();
        CastingDirectors.Clear();
        ProductionDesigners.Clear();
        Actors.Clear();
        VoiceActors.Clear();
        foreach (var person in details.People)
        {
            AddContributor(new ContributorChipViewModel(Guid.Empty, person.Name, person.Role, person.Details));
        }

        MovieStudioCredits.Clear();
        foreach (var studio in details.Studios)
        {
            MovieStudioCredits.Add(new StudioCreditChipViewModel(null, studio.Name, studio.Role, T($"StudioRole.{studio.Role}")));
        }

        if (ShowMovieFields)
        {
            MovieRuntimeMinutes = details.RuntimeMinutes?.ToString() ?? string.Empty;
            MovieOriginalLanguage = details.OriginalLanguage ?? string.Empty;
            MovieCountryOfOrigin = details.Country ?? string.Empty;
            MovieAgeRating = details.AgeRating ?? string.Empty;
        }
        else if (ShowEpisodicFields)
        {
            SeasonCount = details.SeasonCount?.ToString() ?? string.Empty;
            EpisodeCount = details.EpisodeCount?.ToString() ?? string.Empty;
            EpisodeRuntime = details.RuntimeMinutes?.ToString() ?? string.Empty;
            Network = details.Network ?? string.Empty;
            AiringStatus = details.AiringStatus ?? string.Empty;
            EpisodicOriginalLanguage = details.OriginalLanguage ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(details.CollectionName)) CollectionName = details.CollectionName;

        TmdbId = candidate.TmdbId;
        ImdbId = details.ImdbId;
        KinopoiskId = details.KinopoiskId;
        TmdbRating = candidate.TmdbRating;
        TmdbVoteCount = candidate.TmdbVoteCount;
        ImdbRating = details.ImdbRating;
        ImdbVoteCount = details.ImdbVoteCount;
        KinopoiskRating = details.KinopoiskRating;
        KinopoiskVoteCount = details.KinopoiskVoteCount;
        ExternalRatingsUpdatedAt = details.RatingsUpdatedAtUtc;
        NotifyExternalMetadataChanged();
    }

    private void LoadExternalMetadata(PersonalCollectionShelf.Application.DTOs.MediaItemDto item)
    {
        TmdbId = item.TmdbId;
        ImdbId = item.ImdbId;
        KinopoiskId = item.KinopoiskId;
        TmdbRating = item.TmdbRating;
        TmdbVoteCount = item.TmdbVoteCount;
        ImdbRating = item.ImdbRating;
        ImdbVoteCount = item.ImdbVoteCount;
        KinopoiskRating = item.KinopoiskRating;
        KinopoiskVoteCount = item.KinopoiskVoteCount;
        ExternalRatingsUpdatedAt = item.ExternalRatingsUpdatedAt;
        NotifyExternalMetadataChanged();
    }

    private void ResetExternalMetadata()
    {
        TmdbId = null;
        ImdbId = null;
        KinopoiskId = null;
        TmdbRating = null;
        TmdbVoteCount = null;
        ImdbRating = null;
        ImdbVoteCount = null;
        KinopoiskRating = null;
        KinopoiskVoteCount = null;
        ExternalRatingsUpdatedAt = null;
        MetadataCandidates.Clear();
        IsMetadataPickerOpen = false;
        NotifyExternalMetadataChanged();
    }

    private void NotifyExternalMetadataChanged()
    {
        OnPropertyChanged(nameof(HasExternalRatings));
        OnPropertyChanged(nameof(ExternalRatingsSummary));
    }
}
