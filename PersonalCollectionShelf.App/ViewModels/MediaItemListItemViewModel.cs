using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed class MediaItemListItemViewModel : ObservableObject
{
    private string _coverUrl;
    private bool _hasCoverUrl;
    private bool _hasNoCoverUrl;
    private bool _coverNotificationPending;

    public MediaItemListItemViewModel(
        Guid id,
        string title,
        string mediaTypeLabel,
        Color mediaTypeColor,
        string statusLabel,
        string statusIcon,
        Color statusForegroundColor,
        Color statusBackgroundColor,
        string metadata,
        string categoryLine,
        string tagsLine,
        string progress,
        double progressPercent,
        bool hasProgressBar,
        string rating,
        string ratingShort,
        bool hasRating,
        string releaseYearText,
        bool hasReleaseYear,
        string originalCoverUrl,
        string coverUrl,
        bool hasCoverUrl,
        bool hasNoCoverUrl,
        string initial,
        bool isFavorite,
        string favoriteMarker,
        string openButtonText)
    {
        Id = id;
        Title = title;
        MediaTypeLabel = mediaTypeLabel;
        MediaTypeColor = mediaTypeColor;
        StatusLabel = statusLabel;
        StatusIcon = statusIcon;
        StatusForegroundColor = statusForegroundColor;
        StatusBackgroundColor = statusBackgroundColor;
        Metadata = metadata;
        CategoryLine = categoryLine;
        TagsLine = tagsLine;
        Progress = progress;
        ProgressPercent = progressPercent;
        HasProgressBar = hasProgressBar;
        Rating = rating;
        RatingShort = ratingShort;
        HasRating = hasRating;
        ReleaseYearText = releaseYearText;
        HasReleaseYear = hasReleaseYear;
        OriginalCoverUrl = originalCoverUrl;
        _coverUrl = coverUrl;
        _hasCoverUrl = hasCoverUrl;
        _hasNoCoverUrl = hasNoCoverUrl;
        Initial = initial;
        IsFavorite = isFavorite;
        FavoriteMarker = favoriteMarker;
        OpenButtonText = openButtonText;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string MediaTypeLabel { get; }
    public Color MediaTypeColor { get; }
    public string StatusLabel { get; }
    public string StatusIcon { get; }
    public Color StatusForegroundColor { get; }
    public Color StatusBackgroundColor { get; }
    public string Metadata { get; }
    public string CategoryLine { get; }
    public string TagsLine { get; }
    public string Progress { get; }
    public double ProgressPercent { get; }
    public bool HasProgressBar { get; }
    public string Rating { get; }
    public string RatingShort { get; }
    public bool HasRating { get; }
    public string ReleaseYearText { get; }
    public bool HasReleaseYear { get; }
    public string OriginalCoverUrl { get; }
    public string CoverUrl => _coverUrl;
    public bool HasCoverUrl => _hasCoverUrl;
    public bool HasNoCoverUrl => _hasNoCoverUrl;
    public string Initial { get; }
    public bool IsFavorite { get; }
    public string FavoriteMarker { get; }
    public string OpenButtonText { get; }

    public void SetCoverSource(string source, bool notify = true)
    {
        if (string.Equals(_coverUrl, source, StringComparison.Ordinal))
        {
            if (notify && _coverNotificationPending) NotifyCoverChanged();
            return;
        }
        _coverUrl = source;
        _hasCoverUrl = !string.IsNullOrWhiteSpace(source);
        _hasNoCoverUrl = !_hasCoverUrl;
        _coverNotificationPending = !notify;
        if (notify) NotifyCoverChanged();
    }

    private void NotifyCoverChanged()
    {
        _coverNotificationPending = false;
        OnPropertyChanged(nameof(CoverUrl));
        OnPropertyChanged(nameof(HasCoverUrl));
        OnPropertyChanged(nameof(HasNoCoverUrl));
    }

    public bool HasSameContent(MediaItemListItemViewModel other) =>
        Id == other.Id &&
        Title == other.Title &&
        MediaTypeLabel == other.MediaTypeLabel &&
        StatusLabel == other.StatusLabel &&
        CategoryLine == other.CategoryLine &&
        TagsLine == other.TagsLine &&
        Progress == other.Progress &&
        RatingShort == other.RatingShort &&
        ReleaseYearText == other.ReleaseYearText &&
        CoverUrl == other.CoverUrl &&
        IsFavorite == other.IsFavorite;
}
