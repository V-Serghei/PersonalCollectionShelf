using System.Globalization;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Domain.Enums;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class EditMediaItemViewModel
{
    private string _seasonCount = string.Empty;
    private string _episodeCount = string.Empty;
    private string _episodeRuntime = string.Empty;
    private string _network = string.Empty;
    private string _airingStatus = string.Empty;
    private string _sourceMaterial = string.Empty;
    private string _episodicOriginalLanguage = string.Empty;
    private string _volumeCount = string.Empty;
    private string _chapterOrIssueCount = string.Empty;
    private string _readingDirection = string.Empty;
    private bool _isGraphicColor;
    private string _publicationStatus = string.Empty;
    private string _imprint = string.Empty;
    private string _graphicOriginalLanguage = string.Empty;
    private string _gamePlatform = string.Empty;
    private string _mainStoryHours = string.Empty;
    private string _completionistHours = string.Empty;
    private string _gameMode = string.Empty;
    private string _gameEngine = string.Empty;
    private string _gameRegion = string.Empty;

    public bool ShowEpisodicFields => SelectedMediaType?.Value is MediaType.Series or MediaType.Anime or MediaType.AnimatedSeries;
    public bool ShowGraphicPublicationFields => SelectedMediaType?.Value is MediaType.Manga or MediaType.Comic;
    public bool ShowGameFields => SelectedMediaType?.Value == MediaType.Game;

    public string SeasonCount { get => _seasonCount; set => SetProperty(ref _seasonCount, value); }
    public string EpisodeCount { get => _episodeCount; set => SetProperty(ref _episodeCount, value); }
    public string EpisodeRuntime { get => _episodeRuntime; set => SetProperty(ref _episodeRuntime, value); }
    public string Network { get => _network; set => SetProperty(ref _network, value); }
    public string AiringStatus { get => _airingStatus; set => SetProperty(ref _airingStatus, value); }
    public string SourceMaterial { get => _sourceMaterial; set => SetProperty(ref _sourceMaterial, value); }
    public string EpisodicOriginalLanguage { get => _episodicOriginalLanguage; set => SetProperty(ref _episodicOriginalLanguage, value); }
    public string VolumeCount { get => _volumeCount; set => SetProperty(ref _volumeCount, value); }
    public string ChapterOrIssueCount { get => _chapterOrIssueCount; set => SetProperty(ref _chapterOrIssueCount, value); }
    public string ReadingDirection { get => _readingDirection; set => SetProperty(ref _readingDirection, value); }
    public bool IsGraphicColor { get => _isGraphicColor; set => SetProperty(ref _isGraphicColor, value); }
    public string PublicationStatus { get => _publicationStatus; set => SetProperty(ref _publicationStatus, value); }
    public string Imprint { get => _imprint; set => SetProperty(ref _imprint, value); }
    public string GraphicOriginalLanguage { get => _graphicOriginalLanguage; set => SetProperty(ref _graphicOriginalLanguage, value); }
    public string GamePlatform { get => _gamePlatform; set => SetProperty(ref _gamePlatform, value); }
    public string MainStoryHours { get => _mainStoryHours; set => SetProperty(ref _mainStoryHours, value); }
    public string CompletionistHours { get => _completionistHours; set => SetProperty(ref _completionistHours, value); }
    public string GameMode { get => _gameMode; set => SetProperty(ref _gameMode, value); }
    public string GameEngine { get => _gameEngine; set => SetProperty(ref _gameEngine, value); }
    public string GameRegion { get => _gameRegion; set => SetProperty(ref _gameRegion, value); }

    public string EpisodicSectionTitle => T("Edit.Section.EpisodicDetails");
    public string GraphicSectionTitle => T("Edit.Section.GraphicDetails");
    public string GameSectionTitle => T("Edit.Section.GameDetails");
    public string SeasonCountLabel => T("Edit.Label.SeasonCount");
    public string EpisodeCountLabel => T("Edit.Label.EpisodeCount");
    public string EpisodeRuntimeLabel => T("Edit.Label.EpisodeRuntime");
    public string NetworkLabel => T("Edit.Label.Network");
    public string AiringStatusLabel => T("Edit.Label.AiringStatus");
    public string SourceMaterialLabel => T("Edit.Label.SourceMaterial");
    public string VolumeCountLabel => T("Edit.Label.VolumeCount");
    public string ChapterOrIssueCountLabel => T("Edit.Label.ChapterOrIssueCount");
    public string ReadingDirectionLabel => T("Edit.Label.ReadingDirection");
    public string ColorEditionLabel => T("Edit.Label.ColorEdition");
    public string PublicationStatusLabel => T("Edit.Label.PublicationStatus");
    public string ImprintLabel => T("Edit.Label.Imprint");
    public string GamePlatformLabel => T("Edit.Label.GamePlatform");
    public string MainStoryHoursLabel => T("Edit.Label.MainStoryHours");
    public string CompletionistHoursLabel => T("Edit.Label.CompletionistHours");
    public string GameModeLabel => T("Edit.Label.GameMode");
    public string GameEngineLabel => T("Edit.Label.GameEngine");
    public string GameRegionLabel => T("Edit.Label.GameRegion");
    public string DevelopersLabel => T("Edit.Label.Developers");

    private bool TryBuildExtendedTypeInputs(
        out EpisodicDetailsInput? episodic,
        out GraphicPublicationDetailsInput? graphic,
        out GameDetailsInput? game)
    {
        episodic = null; graphic = null; game = null;
        if (ShowEpisodicFields)
        {
            if (!TryParseOptionalInt(SeasonCount, out var seasons) || !TryParseOptionalInt(EpisodeCount, out var episodes) || !TryParseOptionalInt(EpisodeRuntime, out var runtime)) return false;
            episodic = new EpisodicDetailsInput { SeasonCount = seasons, EpisodeCount = episodes, EpisodeRuntimeMinutes = runtime, Network = Network, AiringStatus = AiringStatus, SourceMaterial = SourceMaterial, OriginalLanguage = EpisodicOriginalLanguage };
        }
        if (ShowGraphicPublicationFields)
        {
            if (!TryParseOptionalInt(VolumeCount, out var volumes) || !TryParseOptionalInt(ChapterOrIssueCount, out var chapters)) return false;
            graphic = new GraphicPublicationDetailsInput { VolumeCount = volumes, ChapterOrIssueCount = chapters, ReadingDirection = ReadingDirection, IsColor = IsGraphicColor, PublicationStatus = PublicationStatus, Imprint = Imprint, OriginalLanguage = GraphicOriginalLanguage };
        }
        if (ShowGameFields)
        {
            if (!TryParseOptionalDecimal(MainStoryHours, out var mainHours) || !TryParseOptionalDecimal(CompletionistHours, out var completionHours)) return false;
            game = new GameDetailsInput { Platform = GamePlatform, MainStoryHours = mainHours, CompletionistHours = completionHours, GameMode = GameMode, Engine = GameEngine, Region = GameRegion };
        }
        return true;
    }

    private void LoadExtendedTypeFields(MediaItemDto item)
    {
        var episodic = item.EpisodicDetails;
        SeasonCount = FormatNumber(episodic?.SeasonCount); EpisodeCount = FormatNumber(episodic?.EpisodeCount); EpisodeRuntime = FormatNumber(episodic?.EpisodeRuntimeMinutes);
        Network = episodic?.Network ?? string.Empty; AiringStatus = episodic?.AiringStatus ?? string.Empty; SourceMaterial = episodic?.SourceMaterial ?? string.Empty; EpisodicOriginalLanguage = episodic?.OriginalLanguage ?? string.Empty;
        var graphic = item.GraphicPublicationDetails;
        VolumeCount = FormatNumber(graphic?.VolumeCount); ChapterOrIssueCount = FormatNumber(graphic?.ChapterOrIssueCount); ReadingDirection = graphic?.ReadingDirection ?? string.Empty;
        IsGraphicColor = graphic?.IsColor ?? false; PublicationStatus = graphic?.PublicationStatus ?? string.Empty; Imprint = graphic?.Imprint ?? string.Empty; GraphicOriginalLanguage = graphic?.OriginalLanguage ?? string.Empty;
        var game = item.GameDetails;
        GamePlatform = game?.Platform ?? string.Empty; MainStoryHours = game?.MainStoryHours?.ToString(CultureInfo.InvariantCulture) ?? string.Empty; CompletionistHours = game?.CompletionistHours?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        GameMode = game?.GameMode ?? string.Empty; GameEngine = game?.Engine ?? string.Empty; GameRegion = game?.Region ?? string.Empty;
        foreach (var contribution in item.Contributions.Where(value => value.Role == ContributionRole.Developer)) AddContributor(new ContributorChipViewModel(contribution.PersonId, contribution.PersonName, contribution.Role));
    }

    private void ResetExtendedTypeFields()
    {
        SeasonCount = EpisodeCount = EpisodeRuntime = Network = AiringStatus = SourceMaterial = EpisodicOriginalLanguage = string.Empty;
        VolumeCount = ChapterOrIssueCount = ReadingDirection = PublicationStatus = Imprint = GraphicOriginalLanguage = string.Empty; IsGraphicColor = false;
        GamePlatform = MainStoryHours = CompletionistHours = GameMode = GameEngine = GameRegion = string.Empty;
    }
}
