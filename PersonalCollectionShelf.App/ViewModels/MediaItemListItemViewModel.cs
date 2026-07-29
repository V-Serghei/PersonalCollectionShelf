using Microsoft.Maui.Graphics;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record MediaItemListItemViewModel(
    Guid Id,
    string Title,
    string MediaTypeLabel,
    Color MediaTypeColor,
    string StatusLabel,
    string StatusIcon,
    Color StatusForegroundColor,
    Color StatusBackgroundColor,
    string Metadata,
    string CategoryLine,
    string TagsLine,
    string Progress,
    double ProgressPercent,
    bool HasProgressBar,
    string Rating,
    string RatingShort,
    bool HasRating,
    string ReleaseYearText,
    bool HasReleaseYear,
    string CoverUrl,
    bool HasCoverUrl,
    bool HasNoCoverUrl,
    string Initial,
    bool IsFavorite,
    string FavoriteMarker,
    string OpenButtonText);
