using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class CollectionsViewModel(ICollectionExplorerService explorer, IAuthService auth, ILocalizationService localization) : BaseViewModel(localization)
{
    private IReadOnlyList<CollectionExplorerDto> _allCollections = [];
    private string _searchText = string.Empty;
    private bool _isSearchVisible;
    public ObservableCollection<CollectionExplorerDto> Collections { get; } = [];
    public string PageTitle => T("Collections.Title");
    public string EmptyText => T("Collections.Empty");
    public string SearchPlaceholder => T("Collections.SearchPlaceholder");
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) ApplyFilter(); } }
    public bool IsSearchVisible { get => _isSearchVisible; set => SetProperty(ref _isSearchVisible, value); }

    public async Task LoadAsync()
    {
        var userId = await auth.GetCurrentUserIdAsync() ?? "local-user";
        _allCollections = await explorer.GetAllAsync(userId);
        ApplyFilter();
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        if (!IsSearchVisible) SearchText = string.Empty;
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        Collections.Clear();
        foreach (var collection in _allCollections.Where(value =>
                     term.Length == 0 || value.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     value.Items.Any(item => item.Title.Contains(term, StringComparison.OrdinalIgnoreCase))))
        {
            Collections.Add(collection);
        }
    }

    [RelayCommand]
    private Task OpenItemAsync(CollectionItemDto item) => AppNavigation.OpenMediaDetailsAsync(item.MediaItemId);
}
