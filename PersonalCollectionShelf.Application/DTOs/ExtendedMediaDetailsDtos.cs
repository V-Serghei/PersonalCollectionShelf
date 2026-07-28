namespace PersonalCollectionShelf.Application.DTOs;

public sealed record EpisodicDetailsInput
{
    public int? SeasonCount { get; init; }
    public int? EpisodeCount { get; init; }
    public int? EpisodeRuntimeMinutes { get; init; }
    public string? Network { get; init; }
    public string? AiringStatus { get; init; }
    public string? SourceMaterial { get; init; }
    public string? OriginalLanguage { get; init; }
}

public sealed record GraphicPublicationDetailsInput
{
    public int? VolumeCount { get; init; }
    public int? ChapterOrIssueCount { get; init; }
    public string? ReadingDirection { get; init; }
    public bool? IsColor { get; init; }
    public string? PublicationStatus { get; init; }
    public string? Imprint { get; init; }
    public string? OriginalLanguage { get; init; }
}

public sealed record GameDetailsInput
{
    public string? Platform { get; init; }
    public decimal? MainStoryHours { get; init; }
    public decimal? CompletionistHours { get; init; }
    public string? GameMode { get; init; }
    public string? Engine { get; init; }
    public string? Region { get; init; }
}
