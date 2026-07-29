using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.App.Services;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class EditMediaItemViewModel
{
    private bool _isCatalogMetadataPickerOpen;

    public ObservableCollection<ExternalCatalogCandidate> CatalogMetadataCandidates { get; } = [];
    public string? CatalogProvider { get; private set; }
    public string? CatalogItemId { get; private set; }
    public string? CatalogSourceUrl { get; private set; }
    public string? CatalogRatingPrimarySource { get; private set; }
    public decimal? CatalogRatingPrimary { get; private set; }
    public int? CatalogRatingPrimaryCount { get; private set; }
    public string? CatalogRatingSecondarySource { get; private set; }
    public decimal? CatalogRatingSecondary { get; private set; }
    public int? CatalogRatingSecondaryCount { get; private set; }

    public bool IsCatalogMetadataPickerOpen
    {
        get => _isCatalogMetadataPickerOpen;
        set => SetProperty(ref _isCatalogMetadataPickerOpen, value);
    }

    public string CatalogAttributionText => T("Metadata.CatalogAttribution");

    private async Task SearchCatalogMetadataAsync()
    {
        var type = SelectedMediaType?.Value ?? MediaType.Other;
        if (!_externalCatalogMetadataService.IsConfigured(type))
        {
            ErrorMessage = T("Metadata.Error.CatalogNotConfigured");
            return;
        }

        IsCatalogMetadataPickerOpen = true;
        IsMetadataSearching = true;
        MetadataStatusMessage = T("Metadata.Status.Searching");
        CatalogMetadataCandidates.Clear();
        ErrorMessage = string.Empty;
        try
        {
            var results = await _externalCatalogMetadataService.SearchAsync(ItemTitle, type);
            foreach (var result in results) CatalogMetadataCandidates.Add(result);
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
    private async Task SelectCatalogMetadataCandidateAsync(ExternalCatalogCandidate? candidate)
    {
        if (candidate is null || IsMetadataSearching) return;
        IsMetadataSearching = true;
        MetadataStatusMessage = T("Metadata.Status.Loading");
        try
        {
            var details = await _externalCatalogMetadataService.GetDetailsAsync(candidate);
            ApplyCatalogMetadata(details);
            CoverUrl = await _externalCatalogMetadataService.DownloadImageAsync(candidate) ?? CoverUrl;
            IsCatalogMetadataPickerOpen = false;
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
    private void CloseCatalogMetadataPicker()
    {
        IsCatalogMetadataPickerOpen = false;
        MetadataStatusMessage = string.Empty;
    }

    private void ApplyCatalogMetadata(ExternalCatalogDetails details)
    {
        var candidate = details.Candidate;
        ItemTitle = candidate.Title;
        Subtitle = candidate.Subtitle;
        Description = details.Description ?? Description;
        ReleaseYear = candidate.Year?.ToString() ?? ReleaseYear;
        GenreChips.Clear();
        foreach (var genre in details.Genres) GenreChips.Add(genre);

        Authors.Clear();
        foreach (var author in details.Authors)
            AddContributor(new ContributorChipViewModel(Guid.Empty, author, ContributionRole.Author));

        GameDevelopers.Clear();
        foreach (var developer in details.Developers)
            AddContributor(new ContributorChipViewModel(Guid.Empty, developer, ContributionRole.Developer));

        Publisher = details.Publisher ?? Publisher;
        Isbn10 = details.Isbn10 ?? Isbn10;
        Isbn13 = details.Isbn13 ?? Isbn13;
        if (candidate.MediaType is MediaType.Manga or MediaType.Comic)
            SerialNumber = candidate.Isbn ?? candidate.EditionSummary;
        PageCount = details.PageCount?.ToString() ?? PageCount;
        BookLanguage = details.Language ?? BookLanguage;
        GraphicOriginalLanguage = details.Language ?? GraphicOriginalLanguage;
        VolumeCount = details.VolumeCount?.ToString() ?? VolumeCount;
        ChapterOrIssueCount = details.IssueCount?.ToString() ?? ChapterOrIssueCount;
        GamePlatform = details.Platform ?? GamePlatform;
        GameMode = details.GameMode ?? GameMode;
        GameEngine = details.Engine ?? GameEngine;

        CatalogProvider = candidate.Provider.ToString();
        CatalogItemId = candidate.ExternalId;
        CatalogSourceUrl = candidate.SourceUrl;
        CatalogRatingPrimarySource = details.PrimaryRatingSource;
        CatalogRatingPrimary = details.PrimaryRating;
        CatalogRatingPrimaryCount = details.PrimaryRatingCount;
        CatalogRatingSecondarySource = details.SecondaryRatingSource;
        CatalogRatingSecondary = details.SecondaryRating;
        CatalogRatingSecondaryCount = details.SecondaryRatingCount;
        ExternalRatingsUpdatedAt = details.UpdatedAtUtc;
        NotifyExternalMetadataChanged();
    }

    private void LoadCatalogMetadata(PersonalCollectionShelf.Application.DTOs.MediaItemDto item)
    {
        CatalogProvider = item.CatalogProvider;
        CatalogItemId = item.CatalogItemId;
        CatalogSourceUrl = item.CatalogSourceUrl;
        CatalogRatingPrimarySource = item.CatalogRatingPrimarySource;
        CatalogRatingPrimary = item.CatalogRatingPrimary;
        CatalogRatingPrimaryCount = item.CatalogRatingPrimaryCount;
        CatalogRatingSecondarySource = item.CatalogRatingSecondarySource;
        CatalogRatingSecondary = item.CatalogRatingSecondary;
        CatalogRatingSecondaryCount = item.CatalogRatingSecondaryCount;
    }

    private void ResetCatalogMetadata()
    {
        CatalogProvider = CatalogItemId = CatalogSourceUrl = null;
        CatalogRatingPrimarySource = CatalogRatingSecondarySource = null;
        CatalogRatingPrimary = CatalogRatingSecondary = null;
        CatalogRatingPrimaryCount = CatalogRatingSecondaryCount = null;
        CatalogMetadataCandidates.Clear();
        IsCatalogMetadataPickerOpen = false;
    }
}
