using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public partial class CollectionsViewModel(ICollectionExplorerService explorer, IAuthService auth, ILocalizationService localization) : BaseViewModel(localization)
{
    public ObservableCollection<CollectionExplorerDto> Collections { get; } = [];
    public string PageTitle => T("Collections.Title");
    public string EmptyText => T("Collections.Empty");

    public async Task LoadAsync()
    {
        var userId = await auth.GetCurrentUserIdAsync() ?? "local-user";
        Collections.Clear();
        foreach (var collection in await explorer.GetAllAsync(userId)) Collections.Add(collection);
    }

    [RelayCommand]
    private Task OpenItemAsync(CollectionItemDto item) => AppNavigation.OpenMediaDetailsAsync(item.MediaItemId);
}
