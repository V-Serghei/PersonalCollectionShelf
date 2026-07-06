namespace PersonalCollectionShelf.App.ViewModels;

public sealed record MediaItemListItemViewModel(
    Guid Id,
    string Title,
    string Metadata,
    string Progress,
    string Rating,
    bool IsFavorite,
    string FavoriteMarker,
    string OpenButtonText);
