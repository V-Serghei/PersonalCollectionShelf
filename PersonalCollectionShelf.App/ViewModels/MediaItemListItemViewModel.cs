namespace PersonalCollectionShelf.App.ViewModels;

public sealed record MediaItemListItemViewModel(
    Guid Id,
    string Title,
    string Metadata,
    string CategoryLine,
    string TagsLine,
    string Progress,
    string Rating,
    string CoverUrl,
    bool HasCoverUrl,
    bool HasNoCoverUrl,
    string Initial,
    bool IsFavorite,
    string FavoriteMarker,
    string OpenButtonText);
