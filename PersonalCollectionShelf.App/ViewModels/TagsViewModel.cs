using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record TagGroupViewModel(string Name, IReadOnlyList<MediaItemDto> Items);

public partial class TagsViewModel(IMediaItemService mediaItems, IAuthService auth, ILocalizationService localization) : BaseViewModel(localization)
{
    private IReadOnlyList<TagGroupViewModel> _allTags = [];
    private string _searchText = string.Empty;
    private bool _isSearchVisible;
    public ObservableCollection<TagGroupViewModel> Tags { get; } = [];
    public string PageTitle => T("Tags.Title");
    public string EmptyText => T("Tags.Empty");
    public string SearchPlaceholder => T("Tags.SearchPlaceholder");
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) ApplyFilter(); } }
    public bool IsSearchVisible { get => _isSearchVisible; set => SetProperty(ref _isSearchVisible, value); }

    public async Task LoadAsync()
    {
        var userId = await auth.GetCurrentUserIdAsync() ?? "local-user";
        var library = await mediaItems.GetLibraryAsync(userId);
        _allTags = library.SelectMany(item => item.TagNames.Select(tag => (tag, item)))
                     .GroupBy(value => value.tag, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key)
            .Select(group => new TagGroupViewModel(group.Key, group.Select(value => value.item).OrderBy(value => value.Title).ToList()))
            .ToList();
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
        Tags.Clear();
        foreach (var group in _allTags)
        {
            if (term.Length == 0 || group.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                Tags.Add(group);
                continue;
            }

            var matchingItems = group.Items.Where(item => item.Title.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matchingItems.Count > 0) Tags.Add(new TagGroupViewModel(group.Name, matchingItems));
        }
    }

    [RelayCommand]
    private Task OpenItemAsync(MediaItemDto item) => AppNavigation.OpenMediaDetailsAsync(item.Id);
}
