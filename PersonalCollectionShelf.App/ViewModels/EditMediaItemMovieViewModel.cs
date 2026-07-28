using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.App.Models;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record StudioCreditChipViewModel(
    Guid? StudioId,
    string Name,
    StudioRole Role,
    string RoleDisplayName);

public partial class EditMediaItemViewModel
{
    private string _movieRuntimeMinutes = string.Empty;
    private string _movieOriginalLanguage = string.Empty;
    private string _movieLanguage = string.Empty;
    private string _movieCountryOfOrigin = string.Empty;
    private string _movieAgeRating = string.Empty;
    private string _newMovieStudioName = string.Empty;
    private LocalizedOption<StudioRole>? _selectedMovieStudioRole;

    public ObservableCollection<StudioCreditChipViewModel> MovieStudioCredits { get; } = [];
    public ObservableCollection<LocalizedOption<StudioRole>> MovieStudioRoles { get; } = [];

    public bool ShowMovieFields => SelectedMediaType?.Value == MediaType.Movie;

    public bool ShowScreenProductionFields => SelectedMediaType?.Value is MediaType.Movie or MediaType.Series or MediaType.Anime;

    public string MovieRuntimeMinutes
    {
        get => _movieRuntimeMinutes;
        set => SetProperty(ref _movieRuntimeMinutes, value);
    }

    public string MovieOriginalLanguage
    {
        get => _movieOriginalLanguage;
        set => SetProperty(ref _movieOriginalLanguage, value);
    }

    public string MovieLanguage
    {
        get => _movieLanguage;
        set => SetProperty(ref _movieLanguage, value);
    }

    public string MovieCountryOfOrigin
    {
        get => _movieCountryOfOrigin;
        set => SetProperty(ref _movieCountryOfOrigin, value);
    }

    public string MovieAgeRating
    {
        get => _movieAgeRating;
        set => SetProperty(ref _movieAgeRating, value);
    }

    public string NewMovieStudioName
    {
        get => _newMovieStudioName;
        set => SetProperty(ref _newMovieStudioName, value);
    }

    public LocalizedOption<StudioRole>? SelectedMovieStudioRole
    {
        get => _selectedMovieStudioRole;
        set => SetProperty(ref _selectedMovieStudioRole, value);
    }

    public string MovieDetailsSectionTitle => T("Edit.Section.MovieDetails");

    public string MovieRuntimeLabel => T("Edit.Label.MovieRuntime");

    public string MovieOriginalLanguageLabel => T("Edit.Label.MovieOriginalLanguage");

    public string MovieLanguageLabel => T("Edit.Label.MovieLanguage");

    public string MovieCountryLabel => T("Edit.Label.MovieCountry");

    public string MovieAgeRatingLabel => T("Edit.Label.MovieAgeRating");

    public string MoviePeopleSectionTitle => T("Edit.Section.MoviePeople");

    public string DirectorsLabel => T("Edit.Label.Directors");

    public string ScreenwritersLabel => T("Edit.Label.Screenwriters");

    public string ProducersLabel => T("Edit.Label.Producers");

    public string CinematographersLabel => T("Edit.Label.Cinematographers");

    public string ComposersLabel => T("Edit.Label.Composers");

    public string CastingDirectorsLabel => T("Edit.Label.CastingDirectors");

    public string ProductionDesignersLabel => T("Edit.Label.ProductionDesigners");

    public string ActorsLabel => T("Edit.Label.Actors");

    public string VoiceActorsLabel => T("Edit.Label.VoiceActors");

    public string CharacterNameLabel => T("Edit.Label.CharacterName");

    public string CreditedAsLabel => T("Edit.Label.CreditedAs");

    public string MovieStudiosSectionTitle => T("Edit.Section.MovieStudios");

    public string MovieStudioNamePlaceholder => T("Edit.Placeholder.MovieStudio");

    public string AddStudioText => T("Edit.Action.AddStudio");

    private void InitializeMovieFields() => ReloadMovieOptions();

    private void ReloadMovieOptions()
    {
        var selectedRole = SelectedMovieStudioRole?.Value ?? StudioRole.ProductionCompany;
        MovieStudioRoles.Clear();
        foreach (var value in Enum.GetValues<StudioRole>())
        {
            MovieStudioRoles.Add(new LocalizedOption<StudioRole>(value, T($"StudioRole.{value}")));
        }

        SelectedMovieStudioRole = MovieStudioRoles.First(option => option.Value == selectedRole);
    }

    [RelayCommand]
    private void AddMovieStudio()
    {
        var name = NewMovieStudioName.Trim();
        var role = SelectedMovieStudioRole?.Value ?? StudioRole.ProductionCompany;
        if (name.Length == 0 ||
            MovieStudioCredits.Any(value =>
                value.Role == role &&
                string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        MovieStudioCredits.Add(new StudioCreditChipViewModel(
            null,
            name,
            role,
            T($"StudioRole.{role}")));
        NewMovieStudioName = string.Empty;
    }

    [RelayCommand]
    private void RemoveMovieStudio(StudioCreditChipViewModel? studio)
    {
        if (studio is not null)
        {
            MovieStudioCredits.Remove(studio);
        }
    }

    private IReadOnlyList<StudioCreditInput> BuildStudioCredits() =>
        ShowScreenProductionFields || ShowGameFields
            ? MovieStudioCredits
                .GroupBy(value => value.Role)
                .SelectMany(group => group.Select((value, index) => new StudioCreditInput
                {
                    StudioId = value.StudioId,
                    Name = value.Name,
                    Role = value.Role,
                    SortOrder = index
                }))
                .ToList()
            : [];

    private bool TryBuildMovieInput(out MovieDetailsInput? details)
    {
        details = null;
        if (!ShowMovieFields)
        {
            return true;
        }

        if (!TryParseOptionalInt(MovieRuntimeMinutes, out var runtimeMinutes))
        {
            return false;
        }

        details = new MovieDetailsInput
        {
            RuntimeMinutes = runtimeMinutes,
            OriginalLanguage = MovieOriginalLanguage,
            Language = MovieLanguage,
            CountryOfOrigin = MovieCountryOfOrigin,
            AgeRating = MovieAgeRating
        };
        return true;
    }

    private void LoadMovieFields(MediaItemDto item)
    {
        foreach (var contribution in item.Contributions)
        {
            if (contribution.Role is ContributionRole.Director or
                ContributionRole.Screenwriter or
                ContributionRole.Producer or
                ContributionRole.Cinematographer or
                ContributionRole.Composer or
                ContributionRole.CastingDirector or
                ContributionRole.ProductionDesigner or
                ContributionRole.Actor or
                ContributionRole.VoiceActor)
            {
                AddContributor(new ContributorChipViewModel(
                    contribution.PersonId,
                    contribution.PersonName,
                    contribution.Role,
                    contribution.Details,
                    contribution.CreditedAs));
            }
        }

        MovieStudioCredits.Clear();
        foreach (var studio in item.StudioCredits.OrderBy(value => value.Role).ThenBy(value => value.SortOrder))
        {
            MovieStudioCredits.Add(new StudioCreditChipViewModel(
                studio.StudioId,
                studio.StudioName,
                studio.Role,
                T($"StudioRole.{studio.Role}")));
        }

        var movie = item.MovieDetails;
        MovieRuntimeMinutes = movie?.RuntimeMinutes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        MovieOriginalLanguage = movie?.OriginalLanguage ?? string.Empty;
        MovieLanguage = movie?.Language ?? string.Empty;
        MovieCountryOfOrigin = movie?.CountryOfOrigin ?? string.Empty;
        MovieAgeRating = movie?.AgeRating ?? string.Empty;
    }

    private void ResetMovieFields()
    {
        MovieRuntimeMinutes = string.Empty;
        MovieOriginalLanguage = string.Empty;
        MovieLanguage = string.Empty;
        MovieCountryOfOrigin = string.Empty;
        MovieAgeRating = string.Empty;
        Directors.Clear();
        Screenwriters.Clear();
        Producers.Clear();
        Cinematographers.Clear();
        Composers.Clear();
        CastingDirectors.Clear();
        ProductionDesigners.Clear();
        Actors.Clear();
        VoiceActors.Clear();
        GameDevelopers.Clear();
        MovieStudioCredits.Clear();
        NewMovieStudioName = string.Empty;
        SelectedMovieStudioRole = MovieStudioRoles.FirstOrDefault(option => option.Value == StudioRole.ProductionCompany);
    }
}
