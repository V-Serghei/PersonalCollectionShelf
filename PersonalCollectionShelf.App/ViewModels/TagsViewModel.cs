using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalCollectionShelf.Application.DTOs;
using PersonalCollectionShelf.Application.Interfaces;
using PersonalCollectionShelf.App.Services;

namespace PersonalCollectionShelf.App.ViewModels;

public sealed record TagGroupViewModel(string Name, IReadOnlyList<MediaItemDto> Items);

public partial class TagsViewModel(IMediaItemService mediaItems, IAuthService auth, ILocalizationService localization) : BaseViewModel(localization)
{
    public ObservableCollection<TagGroupViewModel> Tags { get; } = [];
    public string PageTitle => T("Tags.Title");

    public async Task LoadAsync()
    {
        var userId = await auth.GetCurrentUserIdAsync() ?? "local-user";
        var library = await mediaItems.GetLibraryAsync(userId);
        Tags.Clear();
        foreach (var group in library.SelectMany(item => item.TagNames.Select(tag => (tag, item)))
                     .GroupBy(value => value.tag, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key))
        {
            Tags.Add(new TagGroupViewModel(group.Key, group.Select(value => value.item).OrderBy(value => value.Title).ToList()));
        }
    }

    [RelayCommand]
    private Task OpenItemAsync(MediaItemDto item) => AppNavigation.OpenMediaDetailsAsync(item.Id);
}
